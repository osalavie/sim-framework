using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Approval.SqlSugar.Repositories;
using SqlSugar;

namespace Sim.Security.Tests;

/// <summary>
/// 审批并发控制原语测试：实例推进按 Version 比对、任务定稿按 Status 比对，
/// 并发调用只允许一个调用生效；在途唯一索引兜底同一业务对象的重复发起。
/// 直接建库建表验证，不依赖宿主。
/// </summary>
public class ApprovalConcurrencyTests
{
    [Fact]
    public async Task TryAdvance_should_only_apply_for_expected_version_once()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalInstance>();
        var repository = new ApprovalInstanceRepository(database);

        var instance = CreateInstance("biz-1");
        await repository.InsertAsync(instance);

        var current = await repository.GetAsync(instance.Id);
        var expectedVersion = current!.Version;
        current.Title = "第一次推进";
        current.Version = expectedVersion + 1;

        var first = await repository.TryAdvanceAsync(current, expectedVersion, Guid.NewGuid());
        var second = await repository.TryAdvanceAsync(current, expectedVersion, Guid.NewGuid());

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        // 落败的调用不得覆盖已提交的数据。
        var reloaded = await repository.GetAsync(instance.Id);
        Assert.Equal("第一次推进", reloaded!.Title);
        Assert.Equal(expectedVersion + 1, reloaded.Version);
    }

    [Fact]
    public async Task TryAdvance_should_persist_state_and_clear_running_key_on_terminal_status()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalInstance>();
        var repository = new ApprovalInstanceRepository(database);

        var instance = CreateInstance("biz-2");
        await repository.InsertAsync(instance);

        var current = await repository.GetAsync(instance.Id);
        var expectedVersion = current!.Version;
        current.Status = ApprovalInstanceStatus.Approved;
        current.RunningKey = null;
        current.CompletedTime = DateTime.Now;
        current.Version = expectedVersion + 1;

        Assert.Equal(1, await repository.TryAdvanceAsync(current, expectedVersion, null));

        var reloaded = await repository.GetAsync(instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, reloaded!.Status);
        Assert.Null(reloaded.RunningKey);
        Assert.Equal(expectedVersion + 1, reloaded.Version);
    }

    [Fact]
    public async Task TryFinalize_should_only_apply_for_expected_task_status_once()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalTask>();
        var repository = new ApprovalTaskRepository(database);

        var task = new ApprovalTask
        {
            Id = Guid.NewGuid(),
            InstanceId = Guid.NewGuid(),
            NodeKey = "n1",
            NodeName = "审批",
            NodeSort = 1,
            AssigneeUserId = Guid.NewGuid(),
            Status = ApprovalTaskStatus.Pending
        };
        await repository.InsertAsync(task);

        var handledTime = DateTime.Now;
        var first = await repository.TryFinalizeAsync(
            task.Id, ApprovalTaskStatus.Pending, ApprovalTaskStatus.Approved, "同意", handledTime, Guid.NewGuid());
        var second = await repository.TryFinalizeAsync(
            task.Id, ApprovalTaskStatus.Pending, ApprovalTaskStatus.Rejected, "拒绝", handledTime, Guid.NewGuid());

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        var reloaded = await repository.GetAsync(task.Id);
        Assert.Equal(ApprovalTaskStatus.Approved, reloaded!.Status);
        Assert.Equal("同意", reloaded.Comment);
        Assert.NotNull(reloaded.HandledTime);
    }

    [Fact]
    public async Task TryMarkTimeoutNotified_should_only_claim_pending_task_once()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalTask>();
        var repository = new ApprovalTaskRepository(database);
        var task = new ApprovalTask
        {
            Id = Guid.NewGuid(),
            InstanceId = Guid.NewGuid(),
            NodeKey = "n1",
            NodeName = "审批",
            AssigneeUserId = Guid.NewGuid(),
            Status = ApprovalTaskStatus.Pending,
            DueAt = DateTime.Now.AddHours(-1)
        };
        await repository.InsertAsync(task);

        var now = DateTime.Now;
        Assert.Equal(1, await repository.TryMarkTimeoutNotifiedAsync(task.Id, now));
        Assert.Equal(0, await repository.TryMarkTimeoutNotifiedAsync(task.Id, now.AddSeconds(1)));
        Assert.Equal(now, (await repository.GetAsync(task.Id))!.TimeoutNotifiedAt);
    }

    [Fact]
    public async Task Running_key_unique_index_should_reject_second_active_instance()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalInstance>();
        var repository = new ApprovalInstanceRepository(database);

        await repository.InsertAsync(CreateInstance("biz-3"));

        var duplicate = CreateInstance("biz-3");
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => repository.InsertAsync(duplicate));
        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Running_key_unique_index_should_allow_multiple_terminal_instances()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalInstance>();
        var repository = new ApprovalInstanceRepository(database);

        // 终态实例的 RunningKey 为空：同一业务对象可以有任意多个历史终态实例。
        var first = CreateInstance("biz-4");
        first.RunningKey = null;
        var second = CreateInstance("biz-4");
        second.RunningKey = null;

        await repository.InsertAsync(first);
        await repository.InsertAsync(second);

        var stored = await repository.GetListAsync(item => item.BusinessId == "biz-4");
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task Definition_scope_code_and_version_should_be_unique()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalFlowDefinition>();
        var repository = new ApprovalFlowDefinitionRepository(database);
        var first = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            TenantScope = "host",
            Code = "expense",
            Name = "报销",
            Version = 1
        };
        await repository.InsertAsync(first);

        var duplicate = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            TenantScope = "host",
            Code = "expense",
            Name = "报销副本",
            Version = 1
        };

        await Assert.ThrowsAnyAsync<Exception>(() => repository.InsertAsync(duplicate));
    }

    [Fact]
    public async Task Definition_version_should_be_reusable_in_another_tenant_scope()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<ApprovalFlowDefinition>();
        var repository = new ApprovalFlowDefinitionRepository(database);

        await repository.InsertAsync(new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(), TenantScope = "tenant-a", Code = "expense", Name = "A", Version = 1
        });
        await repository.InsertAsync(new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(), TenantScope = "tenant-b", Code = "expense", Name = "B", Version = 1
        });

        Assert.Equal(2, (await repository.GetAll()).Count);
    }

    private static ApprovalInstance CreateInstance(string businessId) => new()
    {
        Id = Guid.NewGuid(),
        Number = $"APV-{Guid.NewGuid():N}",
        DefinitionId = Guid.NewGuid(),
        DefinitionCode = "test-flow",
        DefinitionName = "测试流程",
        DefinitionVersion = 1,
        BusinessType = ApprovalEngineFlowTests.TestBusinessType,
        BusinessId = businessId,
        Title = "测试实例",
        InitiatorUserId = Guid.NewGuid(),
        Status = ApprovalInstanceStatus.Running,
        RunningKey = $"{ApprovalEngineFlowTests.TestBusinessType}:{businessId}",
        Version = 1
    };

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        // 使用独立 ConfigId：SqlSugar 以 ConfigId 维护全局连接配置，复用 "Default" 会干扰宿主测试库。
        ConfigId = "approval-concurrency-tests",
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
