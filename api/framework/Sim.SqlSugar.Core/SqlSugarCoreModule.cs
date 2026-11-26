using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sim.Framework.Core.UnitOfWork;
using Sim.SqlSugar.Core.Attributes;
using Sim.SqlSugar.Core.Diagnostics;
using Sim.SqlSugar.Core.Options;
using Sim.SqlSugar.Core.Repositories;
using Sim.SqlSugar.Core.UnitOfWork;
using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using SqlSugar.Extensions;
using Volo.Abp.Modularity;
using Sim.Framework.Core;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Auditing;
using Sim.Framework.Domain.Shared.Security;
using Sim.Framework.Domain.Shared.Messaging;
using Sim.Framework.Domain.Shared.Numbering;
using Sim.Framework.Domain.Shared;
using Sim.SqlSugar.Core.Messaging;
using Sim.SqlSugar.Core.Numbering;

namespace Sim.SqlSugar.Core;

[DependsOn(
    typeof(FrameworkDomainModule),
    typeof(FrameworkCoreModule)
)]
public class SqlSugarCoreModule : AbpModule
{
    private ILogger<SqlSugarCoreModule> _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SqlSugarCoreModule>.Instance;
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Configuration for SqlSugar can be added here
        var configuration = context.Services.GetConfiguration();
        var dbSection = configuration.GetSection("DataBase");
        var dbOption = dbSection.Get<DbOption>();
        Configure<DbOption>(dbSection);
        if (dbOption?.ConnectionConfigs is not { Count: > 0 })
        {
            throw new InvalidOperationException("DataBase:ConnectionConfigs must contain at least one database connection.");
        }

        if (dbOption.ConnectionConfigs.Any(config => string.IsNullOrWhiteSpace(config.ConnectionString)))
        {
            throw new InvalidOperationException(
                "Every DataBase:ConnectionConfigs entry must provide a non-empty ConnectionString. Supply production values through a secret store or environment variables.");
        }

        dbOption.ConnectionConfigs.ForEach(config =>
        {
            config.ConfigureExternalServices = new ConfigureExternalServices()
            {
                EntityService = (property, column) =>
                {
                    if (property.Name.ToLower() == "id")
                    {
                        column.IsPrimarykey = true;
                    }

                    // IEntity.IdType 是反射辅助属性，不映射为列
                    if (property.Name == nameof(Sim.Framework.Domain.Shared.Entities.IEntity.IdType))
                    {
                        column.IsIgnore = true;
                    }

                    if (column.IsPrimarykey == false && new NullabilityInfoContext().Create(property).WriteState is NullabilityState.Nullable)
                    {
                        column.IsNullable = true; // Allow null for non-primary key properties
                    }

                    // 审计基类的 CreationTime 为非空列；为已有数据的表补列时必须有默认值，
                    // 否则 CodeFirst 会因存量行无法满足 NOT NULL 而失败。
                    if (column.IsPrimarykey == false &&
                        property.Name == "CreationTime" &&
                        !column.IsNullable &&
                        string.IsNullOrWhiteSpace(column.DefaultValue))
                    {
                        column.DefaultValue = "CURRENT_TIMESTAMP";
                    }

                    // 根据配置决定是否将列名转换为下划线命名（snake_case）
                    if (dbOption.UseSnakeCaseNaming)
                    {
                        // 例如：CreationTime → creation_time
                        column.DbColumnName = ToSnakeCase(column.DbColumnName);
                    }
                },
                EntityNameService = (type, entityInfo) =>
                {
                    // 移除 "Entity" 后缀
                    if (entityInfo.DbTableName.EndsWith("Entity"))
                    {
                        entityInfo.DbTableName = entityInfo.DbTableName.Substring(0, entityInfo.DbTableName.Length - 6);
                    }
                    
                    // 根据配置决定是否将表名转换为下划线命名（snake_case）
                    if (dbOption.UseSnakeCaseNaming)
                    {
                        // 例如：ArticleTag → article_tag
                        entityInfo.DbTableName = ToSnakeCase(entityInfo.DbTableName);
                    }

                    // 统一添加全局表前缀（sim_），所有模块的表名自动加上框架前缀
                    // 最终表名格式：sim_<module>_<entity>，例如 sim_system_user、sim_approval_instance
                    if (!entityInfo.DbTableName.StartsWith(Consts.TablePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        entityInfo.DbTableName = Consts.TablePrefix + entityInfo.DbTableName;
                    }
                }
            };
        });

        // For example, you can configure the connection string or other settings
        context.Services.AddScoped<ISqlSugarClient>(sp =>
        {
            // Capture IServiceScopeFactory so the AOP callback can resolve scoped
            // services (ICurrentUser) without depending on a possibly-disposed scope.
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // 使用普通 SqlSugarClient 而非 SqlSugarScope：Scope 依赖 StackTrace 启发式
            // 在 AsyncLocal/ThreadLocal 之间切换内部客户端，ASP.NET Core 请求内
            // BeginTran 与请求结束时的 Dispose 可能落到不同上下文，导致事务连接不释放。
            // Client 由 DI 按请求作用域创建并在释放时关闭连接。
            SqlSugarClient sqlSugarClient = new SqlSugarClient(dbOption.ConnectionConfigs,
            db =>
            {
                // 设置命令超时（秒），防止远程数据库慢查询导致连接池耗尽
                db.Ado.CommandTimeOut = 30;

                // 业务指标与 EnableSqlLog 解耦：无论是否打印 SQL 日志都采集命令次数与耗时。
                // OnLogExecuting 记开始，OnLogExecuted / OnGetDataReadered 记结束，OnError 记失败。
                db.Aop.OnLogExecuting = (sql, p) =>
                {
                    DbCommandProbe.Begin(sql);

                    if (dbOption.EnableSqlLog)
                    {
                        string[] parameterString = p.Select(sp => $"Name={sp.ParameterName}, DbType={sp.DbType}").ToArray();

                        _logger.LogDebug("SQL: {Sql}; Parameters: {Parameters}", sql, string.Join(", ", parameterString));
                    }
                };
                db.Aop.OnLogExecuted = (_, _) => DbCommandProbe.Complete();
                db.Aop.OnGetDataReadered = (_, _, elapsed) => DbCommandProbe.CompleteWithElapsed(elapsed);
                db.Aop.OnError = DbCommandProbe.Fail;
                db.Aop.DataExecuting = (value, entityInfo) =>
                {
                    var columnInfo = entityInfo.EntityColumnInfo;
                    ICurrentUser? currentUser;
                    using (var scope = scopeFactory.CreateScope())
                    {
                        currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
                    }
                    // 种子、后台任务等没有登录上下文的流程通过 AuditContext 指定操作者。
                    var auditUserId = currentUser?.Id ?? AuditContext.CurrentUserId;
                    if (entityInfo.OperationType == DataFilterType.UpdateByObject)
                    {
                        if (entityInfo.PropertyName.Equals("LastModificationTime"))
                        {
                            if (DateTime.MinValue.Equals(value) || value == null)
                            {
                                entityInfo.SetValue(DateTime.Now);
                            }
                        }
                        if (entityInfo.PropertyName.Equals("LastModifierId"))
                        {
                            if (auditUserId != null)
                            {
                                entityInfo.SetValue(auditUserId);
                            }
                        }
                    }
                    if (entityInfo.OperationType == DataFilterType.InsertByObject)
                    {
                        if (entityInfo.PropertyName.Equals("CreationTime"))
                        {
                            if (DateTime.MinValue.Equals(value) || value == null)
                            {
                                entityInfo.SetValue(DateTime.Now);
                            }
                        }
                        if (entityInfo.PropertyName.Equals("CreatorId"))
                        {
                            if (auditUserId != null)
                            {
                                entityInfo.SetValue(auditUserId);
                            }
                        }
                        if (columnInfo.PropertyInfo.PropertyType == typeof(Guid) && columnInfo.DbColumnName.ToLower().Equals("id") && value.ObjToString() == "00000000-0000-0000-0000-000000000000")
                        {
                            entityInfo.SetValue(Guid.NewGuid());
                        }
                        // Auto-set TenantId on insert when current tenant is set
                        if (entityInfo.PropertyName == "TenantId" && TenantContext.CurrentTenantId.HasValue)
                        {
                            entityInfo.SetValue(TenantContext.CurrentTenantId);
                        }
                    }
                };
            });

            return sqlSugarClient;
        });

        context.Services.AddScoped(typeof(ISqlSugarRepository<>), typeof(SqlSugarRepository<>));
        context.Services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        context.Services.AddScoped<ISqlSugarUnitOfWork, SqlSugarUnitOfWork>();
        context.Services.AddScoped<IDatabaseSchemaMigrator, DatabaseSchemaMigrator>();
        context.Services.AddScoped<IMigrationRunner, MigrationRunner>();
        context.Services.AddScoped<IMessageOutboxStore, SqlSugarMessageOutboxStore>();
        context.Services.AddScoped<IMessageInboxStore, SqlSugarMessageInboxStore>();
        context.Services.AddScoped<IBusinessNumberGenerator, SqlSugarBusinessNumberGenerator>();
    }
    public override async Task OnPreApplicationInitializationAsync(Volo.Abp.ApplicationInitializationContext context)
    {
        var services = context.ServiceProvider;
        var dbOption = services.GetRequiredService<IOptions<DbOption>>().Value;
        var configuration = services.GetRequiredService<IConfiguration>();
        var isOpenApiGeneration = configuration.GetValue<bool>("App:OpenApiGeneration");

        _logger = services.GetRequiredService<ILogger<SqlSugarCoreModule>>();

        if (dbOption == null)
        {
            throw new ApplicationException("DBOption configuration is missing or invalid.");
        }

        if (dbOption.ConnectionConfigs.Any(config => config.DbType == DbType.Sqlite))
        {
            // 使用操作系统提供的 SQLite，而不是随包分发的 e_sqlite3 原生库。
            SQLitePCL.Batteries_V2.Init();
        }

        _logger.LogInformation("==========Sim-SQL配置:==========");
        _logger.LogInformation("是否开启种子数据：{EnabledDbSeed}", dbOption.EnabledDbSeed);
        _logger.LogInformation("是否开启CodeFirst：{EnabledCodeFirst}", dbOption.EnabledCodeFirst);
        _logger.LogInformation("是否开启Saas多租户：{EnabledSaasMultiTenancy}", dbOption.EnabledSaasMultiTenancy);
        _logger.LogInformation("是否使用下划线命名：{UseSnakeCaseNaming}", dbOption.UseSnakeCaseNaming);
        _logger.LogInformation("是否开启SQL日志：{EnableSqlLog}", dbOption.EnableSqlLog);
        _logger.LogInformation("===============================");
        if (!isOpenApiGeneration && dbOption.EnabledCodeFirst)
        {
            await services.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        }

        if (!isOpenApiGeneration && dbOption.EnabledDbSeed)
        {
            SeedData(context);
        }

    }

    private void SeedData(Volo.Abp.ApplicationInitializationContext context)
    {
        // Implement your seed data logic here

    }

    /// <summary>
    /// 将 PascalCase 转换为 snake_case
    /// 例如：CreationTime → creation_time, ArticleTag → article_tag
    /// </summary>
    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        // 使用正则表达式在大写字母前插入下划线
        // 例如：CreationTime → Creation_Time → creation_time
        var result = System.Text.RegularExpressions.Regex.Replace(
            str,
            "(?<!^)([A-Z])",
            "_$1"
        ).ToLower();

        return result;
    }
}
