using Sim.Framework.Core.UnitOfWork;
using Sim.SqlSugar.Core.UnitOfWork;
using SqlSugar;

namespace Sim.Security.Tests;

public sealed class SqlSugarUnitOfWorkTests
{
    static SqlSugarUnitOfWorkTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task CommitAsync_persists_changes_made_inside_the_transaction()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "committed" }).ExecuteCommandAsync();
        await unitOfWork.CommitAsync();

        var rows = await database.Queryable<UnitOfWorkTestItem>().ToListAsync();
        Assert.Single(rows);
        Assert.Equal("committed", rows[0].Name);
    }

    [Fact]
    public async Task CommitAsync_persists_all_writes_of_the_transaction()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "first" }).ExecuteCommandAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "second" }).ExecuteCommandAsync();
        await unitOfWork.CommitAsync();

        var rows = await database.Queryable<UnitOfWorkTestItem>().OrderBy(x => x.Name).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(["first", "second"], rows.Select(x => x.Name));
    }

    [Fact]
    public async Task RollbackAsync_discards_changes_made_inside_the_transaction()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "rolled-back" }).ExecuteCommandAsync();
        await unitOfWork.RollbackAsync();

        Assert.Empty(await database.Queryable<UnitOfWorkTestItem>().ToListAsync());
    }

    [Fact]
    public async Task RollbackAsync_discards_all_writes_of_the_transaction()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "first" }).ExecuteCommandAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "second" }).ExecuteCommandAsync();
        await unitOfWork.RollbackAsync();

        Assert.Empty(await database.Queryable<UnitOfWorkTestItem>().ToListAsync());
    }

    [Fact]
    public async Task UnitOfWork_can_start_a_new_transaction_after_rollback()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "discarded" }).ExecuteCommandAsync();
        await unitOfWork.RollbackAsync();

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "kept" }).ExecuteCommandAsync();
        await unitOfWork.CommitAsync();

        var rows = await database.Queryable<UnitOfWorkTestItem>().ToListAsync();
        Assert.Single(rows);
        Assert.Equal("kept", rows[0].Name);
    }

    [Fact]
    public async Task Nested_unit_of_work_should_commit_only_after_outer_commit()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "nested" }).ExecuteCommandAsync();
        await unitOfWork.CommitAsync();

        Assert.NotNull(database.Ado.Transaction);

        await unitOfWork.CommitAsync();
        Assert.Null(database.Ado.Transaction);
        Assert.Single(await database.Queryable<UnitOfWorkTestItem>().ToListAsync());
    }

    [Fact]
    public async Task Nested_rollback_should_abort_the_whole_transaction()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "discarded" }).ExecuteCommandAsync();
        await unitOfWork.RollbackAsync();
        await unitOfWork.CommitAsync();

        Assert.Empty(await database.Queryable<UnitOfWorkTestItem>().ToListAsync());
    }

    [Fact]
    public async Task CloseAsync_releases_the_connection_even_when_auto_close_is_disabled()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UnitOfWorkTestItem>();
        var unitOfWork = new SqlSugarUnitOfWork(database);

        await unitOfWork.BeginAsync();
        await database.Insertable(new UnitOfWorkTestItem { Name = "closed" }).ExecuteCommandAsync();
        await unitOfWork.CommitAsync();

        Assert.NotEqual(global::System.Data.ConnectionState.Closed, database.Ado.Connection.State);

        await unitOfWork.CloseAsync();

        Assert.Equal(global::System.Data.ConnectionState.Closed, database.Ado.Connection.State);
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
                if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
                {
                    column.IsNullable = true;
                }
            }
        }
    });

    private sealed class UnitOfWorkTestItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }
}
