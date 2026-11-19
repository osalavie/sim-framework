using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
// Volo.Abp 命名空间提供 GetApplicationBuilder()/GetEnvironment() 扩展，
// 与 Program.cs 中保留的 ABP 应用初始化（InitializeApplicationAsync）互操作，暂不可移除。
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Sim.Admin.Application;
using Sim.Admin.Application.Contracts;
using Sim.Admin.Domain;
using Sim.Admin.Domain.Shared;
using Sim.Admin.HttpApi.Host.Middleware;
using Sim.Admin.HttpApi.Host.HealthChecks;
using Sim.Admin.SqlSugar;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Application.Security;
using Sim.System.Domain.Shared.Consts;
using Sim.System.SqlSugar;
using Microsoft.Extensions.Logging;
using Sim.Framework.Core.Extensions;
using Swashbuckle.AspNetCore.SwaggerUI;
using Sim.Admin.HttpApi.Host.Data;
using Sim.System.Application;
using Sim.System.Application.Contracts.Notifications;
using Sim.Admin.HttpApi.Host.Notifications;
using Sim.Admin.HttpApi.Host.SiteStatus;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.RateLimiting;
using Sim.Framework.Core.RateLimiting;
using StackExchange.Redis;
using System.Security.Claims;
using Sim.Framework.Application;
using Sim.Framework.Domain.Shared.Telemetry;
using Asp.Versioning;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sim.OpenPlatform.Application;
using Sim.OpenPlatform.Application.Auditing;
using Sim.OpenPlatform.SqlSugar;
using Sim.Approval.Application;
using Sim.Approval.SqlSugar;
using Sim.System.Domain.Security;

namespace Sim.Admin.HttpApi.Host;

[DependsOn(
    typeof(AdminApplicationModule),
    typeof(AdminDomainModule),
    typeof(AdminSqlSugarModule),
    typeof(FrameworkApplicationModule),
    typeof(SystemApplicationModule),
    typeof(SystemSqlSugarModule),
    typeof(SimOpenPlatformApplicationModule),
    typeof(SimOpenPlatformSqlSugarModule),
    typeof(ApprovalApplicationModule),
    typeof(ApprovalSqlSugarModule)
    )]
public partial class AdminHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetSingletonInstance<IWebHostEnvironment>();

        ConfigureDataProtection(context, configuration, hostingEnvironment);
        ConfigureAntiforgery(context, hostingEnvironment);

        if (hostingEnvironment.IsDevelopment() && !configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = false;
        }

        ConfigureForwardedHeaders(context, configuration);

        // ABP 模块系统初始化（InitializeApplicationAsync）需要访问应用构建器，
        // 原由 AbpAspNetCoreModule 注册，现手动注册
        context.Services.AddObjectAccessor<IApplicationBuilder>();
        context.Services.AddObjectAccessor<WebApplication>();
        context.Services.AddObjectAccessor<IHost>();
        context.Services.AddObjectAccessor<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder>();

        // 原由 AbpAspNetCoreModule 注册的授权核心服务
        context.Services.AddAuthorization();

        ConfigureAuthentication(context, configuration);
        ConfigureCurrentUser(context);

        // 内存缓存用于 JWT 会话校验结果缓存，减少每请求 DB 查询
        context.Services.AddMemoryCache();
        ConfigureSignalR(context, configuration);
        context.Services.AddScoped<
            IUserNotificationRealtimePublisher,
            SignalRUserNotificationRealtimePublisher>();
        // maintenance.json 热加载后通过匿名 Hub 通知所有 Web 客户端。
        context.Services.AddHostedService<SiteStatusRealtimeBroadcaster>();
        ConfigureConventionalControllers(context, configuration);
        ConfigureHealthChecks(context);
        ConfigureProblemDetails(context);
        ConfigureSwagger(context, configuration);
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);
        ConfigureRateLimiting(context, configuration);
        ConfigureKestrelLimits(context, configuration);
        ConfigureRequestLocalization(context);
        ConfigureOpenTelemetry(context, configuration);
        ConfigureApiVersioning(context);

        // 数据种子与跨模块升级迁移通过标准 DI 注册（原 ABP ITransientDependency 自动注册）
        context.Services.AddTransient<DataSeedRunner>();
    }

    private static void ConfigureDataProtection(
        ServiceConfigurationContext context,
        IConfiguration configuration,
        IWebHostEnvironment hostingEnvironment)
    {
        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            if (!hostingEnvironment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "DataProtection:KeysPath must point to a persistent, access-controlled directory outside Development.");
            }

            return;
        }

        var resolvedKeysPath = Path.IsPathFullyQualified(keysPath)
            ? keysPath
            : Path.GetFullPath(Path.Combine(hostingEnvironment.ContentRootPath, keysPath));
        Directory.CreateDirectory(resolvedKeysPath);

        context.Services
            .AddDataProtection()
            .SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "SimFramework")
            .PersistKeysToFileSystem(new DirectoryInfo(resolvedKeysPath));
    }

    private static void ConfigureAntiforgery(
        ServiceConfigurationContext context,
        IWebHostEnvironment hostingEnvironment)
    {
        context.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "sim_antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Vite 开发服务器通过 HTTP 反向代理到本机 HTTPS API。后端看到 HTTPS，
            // 但浏览器接收 Cookie 的外层响应仍是 HTTP；开发环境若使用
            // SameAsRequest 会错误地写入 Secure，导致浏览器拒绝保存 Cookie。
            options.Cookie.SecurePolicy = hostingEnvironment.IsDevelopment()
                ? CookieSecurePolicy.None
                : CookieSecurePolicy.Always;
        });
    }

    private static void ConfigureForwardedHeaders(
        ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        context.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            var knownProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
            foreach (var proxy in knownProxies)
            {
                if (global::System.Net.IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            var knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
            foreach (var network in knownNetworks)
            {
                var parts = network.Split('/', 2);
                if (parts.Length == 2
                    && global::System.Net.IPAddress.TryParse(parts[0], out var address)
                    && int.TryParse(parts[1], out var prefixLength))
                {
                    options.KnownIPNetworks.Add(new global::System.Net.IPNetwork(address, prefixLength));
                }
            }
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAuthentication("sim_admin_api")
            .AddJwtBearer("sim_admin_api", jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(configuration.GetValue<int>("JwtSettings:ClockSkew", 1)),
                    ValidAudiences = configuration.GetSection("JwtSettings:ValidAudiences").Get<string[]>(),
                    ValidIssuers = configuration.GetSection("JwtSettings:ValidIssuers").Get<string[]>(),
                    // IssuerSigningKeyResolver is set by PostConfigure below (kid-based key rotation)
                    RequireExpirationTime = true,
                    RequireSignedTokens = true
                };
                jwtOptions.MapInboundClaims = false;
                jwtOptions.SaveToken = false; // 不在内存中保存token
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(
                                "/hubs/user-notifications"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                            context.Response.Headers.Append("Token-Expired", "true");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var grantType = principal?.FindFirst("grant_type")?.Value;
                        if (string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
                        {
                            var clientId = principal?.FindFirst("client_id")?.Value;
                            var applicationId = principal?.FindFirst("application_id")?.Value;
                            if (string.IsNullOrWhiteSpace(clientId) ||
                                !Guid.TryParse(applicationId, out _))
                            {
                                context.Fail("The client credentials token is missing application claims.");
                            }

                            // client_credentials 令牌没有用户会话，由 ClientCredentialsAuthorizationMiddleware
                            // 限制为只能访问带 [Permission] 的端点。
                            return;
                        }

                        var userIdValue = principal?.FindFirst(TokenTypeConst.Id)?.Value ??
                                          principal?.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var sessionIdValue = principal?.FindFirst(TokenTypeConst.SessionId)?.Value;
                        if (!Guid.TryParse(userIdValue, out var userId) ||
                            !Guid.TryParse(sessionIdValue, out var sessionId))
                        {
                            context.Fail("The access token is not bound to a login session.");
                            return;
                        }

                        var tenantClaim = principal?.FindFirst(TokenTypeConst.TenantId)?.Value;
                        if (!string.IsNullOrWhiteSpace(tenantClaim))
                        {
                            if (!Guid.TryParse(tenantClaim, out var tenantId) ||
                                !await context.HttpContext.RequestServices
                                    .GetRequiredService<ITenantStatusValidator>()
                                    .IsActiveAsync(tenantId))
                            {
                                context.Fail("The tenant is no longer active.");
                                return;
                            }
                        }

                        // 缓存会话校验结果 30 秒，避免每请求 2 次 DB 查询导致连接池耗尽
                        var cache = context.HttpContext.RequestServices
                            .GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        var cacheKey = SessionCacheKeys.ForSession(userId, sessionId);
                        if (cache != null && cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is true)
                        {
                            return; // 缓存命中，跳过 DB 查询
                        }

                        var utcNow = DateTime.UtcNow;
                        var repository = context.HttpContext.RequestServices
                            .GetRequiredService<IRefreshTokenRepository>();
                        if (!await repository.IsSessionActiveAsync(userId, sessionId, utcNow))
                        {
                            context.Fail("The login session is no longer active.");
                            return;
                        }

                        await repository.TouchSessionAsync(
                            userId,
                            sessionId,
                            utcNow,
                            utcNow.AddMinutes(-1));

                        // 缓存会话有效状态
                        if (cache != null)
                        {
                            var entry = cache.CreateEntry(cacheKey);
                            entry.Value = true;
                            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                            entry.Dispose();
                        }
                    },
                    OnChallenge = context =>
                    {
                        context.Response.Headers.Append("Authenticate", "Bearer");
                        return Task.CompletedTask;
                    }
                };
            });

        // Kid-based key resolution for seamless rotation:
        // When kid is present in token header, the matching key is used.
        // All active + previous keys are available for validation.
        context.Services.AddTransient<IConfigureOptions<JwtBearerOptions>, Security.JwtBearerKeyResolverConfigure>();
    }

    private void ConfigureCurrentUser(ServiceConfigurationContext context)
    {
        // 注册 Sim 框架的当前用户与当前租户实现
        context.Services.AddHttpContextAccessor();
        context.Services.AddScoped<ICurrentUser, CurrentUser>();
        context.Services.AddScoped<ICurrentTenant, Security.CurrentTenant>();
        context.Services.AddScoped<ITenantStatusValidator, Security.TenantStatusValidator>();
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetSingletonInstance<IWebHostEnvironment>();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<AdminDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Sim.Admin.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<AdminDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Sim.Admin.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<AdminApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Sim.Admin.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<AdminApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Sim.Admin.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers(ServiceConfigurationContext context, IConfiguration configuration)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: false);
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<AdminHttpApiHostModule>();

        // 使用框架提供的扩展方法，自动从入口程序集引用链发现 Sim.* 模块，
        // 不再需要 appsettings.json 中的 Modules:Modules 配置节点。
        context.Services.ConfigureConventionalControllers(logger);
    }

    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddSwaggerGen(options =>
        {
            // 使用框架提供的扩展方法，从配置文件动态生成Swagger文档
            options.ConfigureSwaggerFromConfiguration(configuration);

            // 添加JWT Bearer认证支持
            options.AddSecurityDefinition("JWT Auth", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("JWT Auth", null, null),
                    []
                }
            });
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private void ConfigureSignalR(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var signalRBuilder = context.Services.AddSignalR();
        var hostingEnvironment = context.Services.GetSingletonInstance<IWebHostEnvironment>();

        // Development is single-instance by default; avoid requiring Redis only for SignalR.
        // Multi-instance deployments can enable the backplane explicitly.
        var useRedisBackplane = configuration.GetValue(
            "Framework:SignalR:UseRedisBackplane", !hostingEnvironment.IsDevelopment());

        // Configure Redis backplane for multi-instance scale-out
        var cachingProvider = configuration["Framework:Caching:Provider"];
        var redisConnString = configuration["Framework:Caching:RedisConnectionString"];
        if (useRedisBackplane &&
            !string.IsNullOrWhiteSpace(redisConnString) &&
            (cachingProvider?.Equals("Redis", StringComparison.OrdinalIgnoreCase) == true ||
             cachingProvider?.Equals("Hybrid", StringComparison.OrdinalIgnoreCase) == true))
        {
            signalRBuilder.AddStackExchangeRedis(redisConnString, o =>
            {
                o.Configuration.ChannelPrefix = RedisChannel.Literal("sim-signalr");
            });
        }
    }

    private static void ConfigureRateLimiting(
        ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        var defaults = configuration.GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();
        if (defaults.DefaultPermitLimit <= 0 || defaults.DefaultWindowSeconds <= 0 || defaults.QueueLimit < 0)
        {
            throw new InvalidOperationException(
                "RateLimiting defaults must use positive permit/window values and a non-negative queue limit.");
        }

        context.Services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName));

        // Determine if Redis-backed distributed rate limiting is available
        var cachingProvider = configuration["Framework:Caching:Provider"];
        var useRedisLimiter =
            (cachingProvider?.Equals("Redis", StringComparison.OrdinalIgnoreCase) == true ||
             cachingProvider?.Equals("Hybrid", StringComparison.OrdinalIgnoreCase) == true) &&
            !string.IsNullOrWhiteSpace(configuration["Framework:Caching:RedisConnectionString"]);
        var redisInstanceName = configuration["Framework:Caching:InstanceName"] ?? "sim:";

        context.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (rejectionContext, _) =>
            {
                if (rejectionContext.Lease.TryGetMetadata(
                        MetadataName.RetryAfter, out var retryAfter))
                {
                    rejectionContext.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                return ValueTask.CompletedTask;
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var endpoint = httpContext.GetEndpoint();
                var attribute = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();
                if (!defaults.Enabled || attribute == null)
                {
                    return RateLimitPartition.GetNoLimiter("unlimited");
                }

                var permitLimit = attribute.PermitLimit > 0
                    ? attribute.PermitLimit
                    : defaults.DefaultPermitLimit;
                var windowSeconds = attribute.WindowSeconds > 0
                    ? attribute.WindowSeconds
                    : defaults.DefaultWindowSeconds;
                var endpointPartition = string.IsNullOrWhiteSpace(attribute.PartitionName)
                    ? endpoint?.DisplayName ?? httpContext.Request.Path.Value ?? "endpoint"
                    : attribute.PartitionName;
                // JWT 的用户声明是自定义 "Id" 而非标准 NameIdentifier；
                // 优先取自定义声明，保证已认证请求按用户分区，与匿名请求的 IP 分区隔离。
                var clientIdentity = httpContext.User.FindFirstValue(TokenTypeConst.Id)
                                     ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                     ?? httpContext.Connection.RemoteIpAddress?.ToString()
                                     ?? "unknown";
                var partitionKey = $"{endpointPartition}:{clientIdentity}";

                // Use Redis-backed limiter for multi-instance; fall back to in-memory
                if (useRedisLimiter)
                {
                    var redis = httpContext.RequestServices.GetService<IConnectionMultiplexer>();
                    if (redis != null)
                    {
                        return RateLimitPartition.Get(
                            partitionKey,
                            _ => new RedisFixedWindowRateLimiter(
                                redis, redisInstanceName, partitionKey, permitLimit, windowSeconds));
                    }
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = defaults.QueueLimit,
                        AutoReplenishment = true
                    });
            });
        });
    }

    private static void ConfigureKestrelLimits(
        ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        // Align Kestrel MaxRequestBodySize with FileManagement:MaxUploadBytes
        // to prevent Kestrel's default 30MB from returning 413 before our app-level check
        var maxUploadBytes = configuration.GetValue<long?>("FileManagement:MaxUploadBytes");
        if (maxUploadBytes.HasValue && maxUploadBytes.Value > 0)
        {
            context.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = maxUploadBytes.Value;
            });
        }
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddHostHealthChecks();
    }

    private static void ConfigureProblemDetails(ServiceConfigurationContext context)
    {
        context.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                // Don't leak internal details in production
                if (!ctx.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                {
                    ctx.ProblemDetails.Detail = null;
                }
            };
        });
    }

    private static void ConfigureRequestLocalization(ServiceConfigurationContext context)
    {
        // 与 AdminDomainSharedModule 中配置的语言列表保持一致
        var supportedCultures = new[]
        {
            "zh-Hans", "en", "en-GB", "es", "ar", "hi", "pt-BR", "fr", "ru", "de-DE",
            "tr", "it", "cs", "hu", "ro-RO", "sv", "fi", "sk", "is", "zh-Hant"
        }
        .Select(culture => new CultureInfo(culture))
        .ToList();

        context.Services.AddRequestLocalization(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("zh-Hans");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
        });
    }

    private static void ConfigureOpenTelemetry(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var otelSection = configuration.GetSection("OpenTelemetry");
        var serviceName = otelSection["ServiceName"] ?? "simframework-api";
        var serviceVersion = otelSection["ServiceVersion"] ?? "1.0.0";

        var builder = context.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes([
                    new("deployment.environment", otelSection["Environment"] ?? "development"),
                    new("host.name", Environment.MachineName),
                ]));

        // Tracing: ASP.NET Core incoming requests + outgoing HTTP calls
        if (otelSection.GetValue("Tracing:Enabled", true))
        {
            builder.WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        opts.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.header.user-agent",
                                request.Headers.UserAgent.ToString());
                        };
                        opts.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", response.StatusCode);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        // Outbound query strings can contain provider credentials or tokens.
                        // Keep host/path telemetry but never export the query component.
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            if (request.RequestUri is not { } requestUri)
                            {
                                return;
                            }

                            var safeUrl = requestUri.GetLeftPart(UriPartial.Path);
                            activity.SetTag("http.url", safeUrl);
                            activity.SetTag("url.full", safeUrl);
                        };
                    });

                // Redis instrumentation when Redis is configured
                var redisConn = configuration["Framework:Caching:RedisConnectionString"];
                if (!string.IsNullOrWhiteSpace(redisConn))
                {
                    tracing.AddRedisInstrumentation();
                }

                // OTLP exporter (collectors like Jaeger, Tempo, Grafana Alloy)
                var otlpEndpoint = otelSection["Otlp:Endpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });
        }

        // Metrics: ASP.NET Core + HTTP + runtime metrics
        if (otelSection.GetValue("Metrics:Enabled", true))
        {
            builder.WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                // 业务指标（数据库、定时任务）与框架 instrumentation 走同一管道，
                // 因此它们同时出现在 /metrics 和 OTLP 导出中。
                metrics.AddMeter(Metrics.MeterName);

                // Prometheus scrape endpoint at /metrics
                metrics.AddPrometheusExporter();

                // OTLP exporter (collectors like Prometheus OTLP receiver)
                var otlpEndpoint = otelSection["Otlp:Endpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });
        }
    }

    private static void ConfigureApiVersioning(ServiceConfigurationContext context)
    {
        context.Services.AddApiVersioning(options =>
        {
            // Default version for unversioned endpoints (backward compatibility)
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;

            // Read version from URL segment: api/v1/..., api/v2/...
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddApiExplorer(options =>
        {
            // Format: "v{version}" for Swagger group naming
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
    }

    private async Task SeedDataAsync(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminHttpApiHostModule>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // 检查是否启用数据种子
        var enabledDbSeed = configuration.GetValue<bool>("DataBase:EnabledDbSeed");
        if (!enabledDbSeed)
        {
            logger.LogInformation("数据种子已禁用（EnabledDbSeed=false），跳过执行");
            return;
        }

        try
        {
            logger.LogInformation("开始检查并执行数据种子...");

            var seeded = await scope.ServiceProvider
                .GetRequiredService<DataSeedRunner>()
                .RunIfInitializedAsync();

            logger.LogInformation(
                seeded ? "数据种子检查完成" : "数据种子已跳过：系统尚未完成首次初始化");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据种子执行失败: {Message}", ex.Message);
            // 不抛出异常，允许应用继续启动
        }
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var app = context.ServiceProvider.GetRequiredService<IObjectAccessor<WebApplication>>().Value!;
        var env = context.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var isOpenApiGeneration = configuration.GetValue<bool>("App:OpenApiGeneration");

        app.UseForwardedHeaders();
        app.UseHttpsRedirection();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
        }

        // 契约导出必须保持只读，不执行迁移或数据种子。
        if (!isOpenApiGeneration)
        {
            await SeedDataAsync(context);
        }

        app.UseRequestLocalization();

        // 客户端本地化资源随语言变化：声明 Vary 并禁止缓存，避免浏览器或
        // 反向代理按同一 URL 复用另一种语言的菜单与站点文案。
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(
                    "/api/admin/client-localization",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.OnStarting(() =>
                {
                    if (!context.Response.Headers["Vary"].ToString()
                            .Contains("Accept-Language", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.Headers.Append("Vary", "Accept-Language");
                    }

                    context.Response.Headers["Cache-Control"] = "no-store";
                    return Task.CompletedTask;
                });
            }

            await next();
        });

        // Swagger 默认只在非生产环境开放；Production 必须显式配置
        // Swagger:AllowInProduction=true。Docker/nginx 部署会开启该开关，并
        // 在反向代理层用 Basic Auth 保护文档面；业务接口仍由各自授权策略保护。
        var swaggerEnabled = !env.IsProduction() ||
            configuration.GetValue("Swagger:AllowInProduction", false);
        if (swaggerEnabled)
        {
            app.UseStaticFiles();
        }

        app.UseRouting();
        app.UseSecurityHeaders();
        app.UseCors();

        // Maintenance can be toggled by editing reloadable JSON configuration; status and health endpoints stay available.
        app.UseMaintenance();

        app.UseAuthentication();
        // 租户上下文依赖 JWT 中的 tenant_id 声明，必须在认证完成之后同步。
        app.UseTenantContextSync();
        app.UseMiddleware<OpenPlatformCallAuditMiddleware>();
        app.UseMiddleware<ClientCredentialsAuthorizationMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();

        if (swaggerEnabled)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                // 从配置文件动态添加Swagger端点
                var swaggerDocs = SwaggerConfigurationExtensions.GetSwaggerDocuments(configuration);
                foreach (var doc in swaggerDocs)
                {
                    options.SwaggerEndpoint(doc.Url, doc.Title);
                }

                // 设置默认展开的文档
                options.DefaultModelsExpandDepth(0);
                options.DocExpansion(DocExpansion.None);

                // 使用自定义的 index.html
                options.IndexStream = () =>
                {
                    // 从文件系统读取自定义 index.html
                    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
                    var filePath = Path.Combine(webRoot, "swagger", "index.html");

                    if (File.Exists(filePath))
                    {
                        return File.OpenRead(filePath);
                    }

                    throw new FileNotFoundException($"Custom Swagger index.html not found at {filePath}");
                };
            });
        }

        if (!isOpenApiGeneration && swaggerEnabled)
        {
            app.MapStaticAssets();
        }

        app.MapControllers();
        app.MapHub<UserNotificationHub>("/hubs/user-notifications");
        app.MapHub<SiteStatusHub>("/hubs/site-status");
        app.MapHealthChecks();

        // OpenTelemetry Prometheus scraping endpoint
        if (configuration.GetValue("OpenTelemetry:Metrics:Enabled", true))
        {
            app.UseOpenTelemetryPrometheusScrapingEndpoint();
        }

        // 根路径仅在 Swagger UI 可用时跳转到接口文档，避免环境
        // 进入一个缺少 Swagger 静态资源的残页。
        if (swaggerEnabled)
        {
            app.MapGet("/", httpContext =>
            {
                httpContext.Response.Redirect("swagger/index.html");
                return Task.CompletedTask;
            });
        }
    }
}
