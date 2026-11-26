using System.Security.Claims;
using Sim.Framework.Domain.Shared.Security;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;
using Sim.System.Application.User;
using Sim.System.Domain.User;

namespace Sim.Security.Tests;

public class UserPreferenceServiceTests
{
    [Fact]
    public async Task ReturnsNullUntilTheUserSavesAPreference()
    {
        using var database = CreateDatabase();
        var repository = new SqlSugarRepository<UserPreferenceEntity>(database);
        var service = CreateService(repository, Guid.NewGuid());

        Assert.Null(await service.GetMyPreferencesAsync());
        Assert.Empty(await repository.GetAll());
    }

    [Fact]
    public async Task PersistsAndNormalizesThePreference()
    {
        using var database = CreateDatabase();
        var repository = new SqlSugarRepository<UserPreferenceEntity>(database);
        var userId = Guid.NewGuid();
        var service = CreateService(repository, userId);

        var saved = await service.UpdateMyPreferencesAsync(new UpdateUserPreferenceDto
        {
            Language = UserPreferenceDefaults.LanguageEnUs,
            ThemeMode = "DARK",
            ThemeStyle = "zhuyue"
        });

        Assert.Equal(UserPreferenceDefaults.LanguageEnUs, saved.Language);
        Assert.Equal(UserPreferenceDefaults.ThemeModeDark, saved.ThemeMode);
        Assert.Equal("zhuyue", saved.ThemeStyle);

        var loaded = await service.GetMyPreferencesAsync();
        Assert.NotNull(loaded);
        Assert.Equal(saved.Language, loaded!.Language);
        Assert.Equal(saved.ThemeMode, loaded.ThemeMode);
        Assert.Equal(saved.ThemeStyle, loaded.ThemeStyle);

        var entity = await repository.GetAsync(item => item.UserId == userId);
        Assert.NotNull(entity);
        Assert.Equal(UserPreferenceDefaults.ThemeModeDark, entity!.ThemeMode);
    }

    [Fact]
    public async Task IgnoresUnknownValuesAndKeepsExistingFieldsOnPartialUpdate()
    {
        using var database = CreateDatabase();
        var repository = new SqlSugarRepository<UserPreferenceEntity>(database);
        var service = CreateService(repository, Guid.NewGuid());

        var initial = await service.UpdateMyPreferencesAsync(new UpdateUserPreferenceDto
        {
            Language = UserPreferenceDefaults.LanguageEnUs,
            ThemeMode = UserPreferenceDefaults.ThemeModeDark,
            ThemeStyle = "xuanqing"
        });

        var updated = await service.UpdateMyPreferencesAsync(new UpdateUserPreferenceDto
        {
            Language = "fr-FR",
            ThemeMode = "sepia",
            ThemeStyle = "not a style!",
            // 其余字段为空时保持原值。
        });

        Assert.Equal(initial.Language, updated.Language);
        Assert.Equal(initial.ThemeMode, updated.ThemeMode);
        Assert.Equal(initial.ThemeStyle, updated.ThemeStyle);
    }

    private static UserPreferenceService CreateService(
        ISqlSugarRepository<UserPreferenceEntity> repository,
        Guid userId)
    {
        return new UserPreferenceService(repository)
        {
            CurrentUser = new TestCurrentUser(userId)
        };
    }

    private static SqlSugarScope CreateDatabase()
    {
        SQLitePCL.Batteries_V2.Init();
        var database = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Default",
            DbType = DbType.Sqlite,
            ConnectionString = "DataSource=:memory:",
            IsAutoCloseConnection = false,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == "Id") column.IsPrimarykey = true;
                    if (property.Name == "IdType") column.IsIgnore = true;
                    if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
                    {
                        column.IsNullable = true;
                    }
                }
            }
        });
        database.CodeFirst.InitTables<UserPreferenceEntity>();
        return database;
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "tester";
        public string? Name => null;
        public string? SurName => null;
        public string? Email => null;
        public bool EmailVerified => false;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => [];
        public Claim? FindClaim(string claimType) => null;
        public string? FindClaimValue(string claimType) => null;
        public Claim[] FindClaims(string claimType) => [];
        public Claim[] GetAllClaims() => [];
        public bool IsInRole(string roleName) => false;
    }
}
