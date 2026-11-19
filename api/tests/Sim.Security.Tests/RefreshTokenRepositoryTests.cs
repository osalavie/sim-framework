using Sim.System.SqlSugar.Repositories;
using SqlSugar;
using Sim.System.Domain.Security;
using Sim.System.SqlSugar.Repositories.Security;

namespace Sim.Security.Tests;

public class RefreshTokenRepositoryTests
{
    [Fact]
    public async Task TokenCanOnlyBeRotatedOnce()
    {
        SQLitePCL.Batteries_V2.Init();
        using var database = new SqlSugarScope(new ConnectionConfig
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
                    // IEntity.IdType 是反射辅助属性，不映射为列
                    if (property.Name == "IdType") column.IsIgnore = true;
                    if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
                    {
                        column.IsNullable = true;
                    }
                }
            }
        });
        database.CodeFirst.InitTables<RefreshTokenEntity>();

        var token = new RefreshTokenEntity
        {
            UserId = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            Token = "old-token-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        await database.Insertable(token).ExecuteCommandAsync();

        var repository = new RefreshTokenRepository(database);
        var revokedAt = DateTime.UtcNow;
        var first = await repository.TryRotateAsync(token.Id, "new-token-hash", revokedAt);
        var second = await repository.TryRotateAsync(token.Id, "another-token-hash", revokedAt);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task SessionCanBeMonitoredTouchedAndRevoked()
    {
        SQLitePCL.Batteries_V2.Init();
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RefreshTokenEntity>();
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await database.Insertable(new RefreshTokenEntity
        {
            UserId = userId,
            FamilyId = sessionId,
            Token = "session-token-hash",
            ExpiresAt = utcNow.AddDays(1),
            DeviceType = "PC",
            LastSeenAt = utcNow.AddMinutes(-5)
        }).ExecuteCommandAsync();

        var repository = new RefreshTokenRepository(database);
        Assert.True(await repository.IsSessionActiveAsync(userId, sessionId, utcNow));

        await repository.TouchSessionAsync(userId, sessionId, utcNow, utcNow.AddMinutes(-1));
        var sessions = await repository.GetActiveSessionTokensAsync(userId, utcNow);
        Assert.Single(sessions);
        Assert.Equal(utcNow, sessions[0].LastSeenAt);

        Assert.Equal(1, await repository.RevokeFamilyAsync(userId, sessionId, utcNow));
        Assert.False(await repository.IsSessionActiveAsync(userId, sessionId, utcNow));
    }

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
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
}
