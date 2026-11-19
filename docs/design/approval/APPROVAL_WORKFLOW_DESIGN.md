# 审批流总体设计

> 状态：核心能力已实施（2026-09-23 交付）。已落地：独立 `approval` 模块五项目分层与宿主接线、审批定义与版本（草稿/发布/停用/另存新版本/删除草稿）、运行时实例/任务/流转记录状态机、审批人解析（指定用户、角色、组织负责人、组织人员、发起人本人）、串行多级节点、节点条件、会签/或签/顺序审批、转交、前/后加签、退回上一节点、退回发起人、重新提交、撤回、催办、超时扫描 Job、管理员终止与抄送，以及 Web 端审批中心四视图、流程定义列表/编辑器与实例监控。未实施：`instance/counts` 顶栏角标、按 `RoleDataScope` 的数据范围过滤、第三方发起审批。第二阶段进度：P0（并发原子性与规则对齐）、P1（有向图引擎与节点状态持久化）和 P2（字段与附件）已完成（2026-09-24），P3–P5 待实施，见 [`APPROVAL_WORKFLOW_PHASE2_DESIGN.md`](APPROVAL_WORKFLOW_PHASE2_DESIGN.md) 的 13.1.1。
> 依赖：组织基线遵循 [`ORGANIZATION_CHART_DESIGN.md`](../organization/ORGANIZATION_CHART_DESIGN.md) 第一阶段（已随本次交付落地组织单位树、单位编码与成员归属，以及 `IOrganizationDirectory` 读取端口）；通知投递复用 [`NOTIFICATION_PLATFORM_DESIGN.md`](../notification/NOTIFICATION_PLATFORM_DESIGN.md) 已实施基线。
> 结论：不引入开源工作流引擎，在独立 `approval` 模块中基于现有 SqlSugar + Sim 分层 + Quartz + Outbox + 通知平台自研轻量审批引擎。

## 一、设计结论

审批流是"组织与人事关系"之上的第一类消费方。整体系统由四层组成：

1. **组织基线**（System 模块）：组织树、主/兼职成员关系、组织负责人。审批人解析只读取组织数据，不反向修改。
2. **审批定义**（approval 模块）：按业务类型声明节点序列、审批人策略、条件、多人审批方式，发布后版本化、不可变。
3. **审批运行时**（approval 模块）：实例、任务、流转记录的状态机；任务在生成时固化审批人快照，不随人事变动漂移。
4. **业务接入**（各业务模块）：通过 Contracts 端口提交审批、接收结果回调、查询审批状态；业务实体不依赖审批表结构。

最终语义必须满足：

```text
审批动作生效 = 操作人是当前待办任务的责任人
            AND 实例与任务处于可操作状态
            AND 乐观并发版本匹配
            AND 操作在节点配置允许范围内
```

待办列表、催办、红点都只是查询视图；唯一权威是任务表中的待办记录。菜单隐藏和前端按钮禁用不能作为安全边界。

## 二、目标与非目标

### 2.1 目标（首期全部覆盖）

- 串行多级审批：按节点顺序逐级推进。
- 条件分支：节点携带条件，按业务上下文决定节点是否激活。
- 会签/或签/顺序会签：一个节点多人审批，支持全员同意、一人同意、按序依次三种完成方式。
- 转交、前/后加签、退回（上一节点或发起人）、发起人撤回、管理员终止。
- 抄送：节点完成或实例结束时抄送指定人员。
- 待办/已办/我发起的/抄送我的四个用户视图 + 管理端定义维护与实例监控。
- 通知接入：待办、结果、退回、抄送、催办、超时全部走系统通知平台。

### 2.2 非目标

见第二十一节"明确不做"。首期核心是不做 BPMN 通用引擎、不做并行 fork/join 分支、不做画布式流程设计器；
其中后两项已在第二阶段翻开（`docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md`），仍不做全量 BPMN 元素。

## 三、技术选型

调研结论：没有主流开源审批引擎原生使用 SqlSugar 存储。

| 候选 | 审批语义 | 存储层 | 结论 |
| --- | --- | --- | --- |
| AntFlow.NET（Apache-2.0，钉钉风格） | 最全：条件分支、顺序会签、会签/或签、加批、转办、打回 | FreeSql | 嵌入即引入第二 ORM 与引擎自有表结构/身份模型；复用 SqlSugar 等于 fork 持久层，失去上游升级。仅作为节点能力设计的语义参考 |
| Workflow Core | 通用长流程，人工任务靠 User 扩展 | 可插拔 `IPersistenceProvider`，官方无 SqlSugar | 表结构归引擎所有，"我的待办"等业务查询别扭；无多租户概念；审批语义仍需自建 |
| Elsa 3 | 通用工作流 + 可视化设计器 | EF Core | 引入第二 ORM 与独立端点/身份体系，与 Sim 模块、权限链路融合成本高 |

结合框架"去重型依赖、稳定端口自研"的方向（ABP 基础设施已由 Sim 等价实现替代），自研的工作量集中在状态机与审批人解析两处，可控且与租户、权限、通知、业务编号、审计体系天然一致。

## 四、总体架构与模块边界

可编辑图源：[`diagrams/approval-architecture.mmd`](../../diagrams/approval-architecture.mmd)。

```text
api/modules/approval/
├── Sim.Approval.Domain.Shared        # 错误码、权限常量、枚举、业务描述符契约
├── Sim.Approval.Domain               # 实体、审批人解析器、条件求值器、状态机领域服务
├── Sim.Approval.Application.Contracts# 业务提交/回调/查询端口、管理端与用户端 AppService 接口、DTO
├── Sim.Approval.Application          # 应用服务、Mapperly 映射、通知事件发布、内置 Job 定义
└── Sim.Approval.SqlSugar             # 仓储与表映射

web/src/modules/approval/             # 审批中心（用户端）+ 审批管理（管理端）
```

依赖方向遵守现有架构测试约束：

- 业务模块只引用 `Sim.Approval.Application.Contracts`，通过提交端口发起审批、注册结果回调；approval 不引用任何业务模块。
- approval 通过 `Sim.System.Application.Contracts` 的稳定端口消费账号目录（`INotificationAccountDirectory`）、组织树查询和通知事件发布（`INotificationEventPublisher`），不直接读 System 表。
- approval 复用 Framework 的 `IBusinessNumberGenerator`（实例编号）、`IMessageBus` Outbox（通知事件）、`IScheduleTaskDefinitionProvider`（超时扫描内置 Job）。
- Host 组合根按 `ARCHITECTURE.md` 约定添加 `ProjectReference` 并在 `[DependsOn]` 声明 Application/SqlSugar 模块，Swagger 新增 `approval` 分组并纳入 `all` 聚合文档。

## 五、实施顺序

审批流依赖组织基线，按以下顺序交付：

1. **阶段 0：组织基线**。按 ORGANIZATION_CHART_DESIGN.md 第一阶段实现组织树、成员关系、组织负责人（`system:organization:manage/view` 权限码已预留）。审批人策略中的"组织负责人/逐级上级"依赖此能力。
2. **阶段 1：审批核心闭环**。定义 + 串行节点 + 指定人/角色审批人 + 提交/同意/拒绝/撤回 + 四个用户视图 + 通知。
3. **阶段 2：高级节点能力**。条件分支、会签/或签/顺序会签、退回、转交、加签、抄送、组织负责人审批人。
4. **阶段 3：运营能力**。超时扫描与催办、实例监控与终止、业务模块提交审批示例、越权与并发测试矩阵。

各阶段均可独立上线：阶段 1 没有组织数据也能运行（审批人策略退化为指定人/角色）。

## 六、领域模型

可编辑图源：[`diagrams/approval-domain-er.mmd`](../../diagrams/approval-domain-er.mmd)。表名统一 `approval_` 前缀，全部携带 `TenantId` 并参与租户隔离。

### 6.1 ApprovalFlowDefinition（流程定义）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 定义 ID | `Guid` |
| `TenantId` | 租户 | 参与全部唯一索引 |
| `Code` | 业务键 | 租户内与 `Version` 联合唯一；对应一个业务类型，如 `qms.ocap.submit` |
| `Version` | 发布版本号 | 从 1 递增；发布后整行不可变 |
| `Name` / `Description` | 展示信息 | 本地化由管理端文案负责 |
| `Status` | `Draft` / `Published` / `Disabled` | 同一 `Code` 最多一个 `Published` |
| `BusinessType` | 绑定的业务类型 | 可空；用于按业务类型解析默认定义 |
| `AllowWithdraw` | 是否允许发起人撤回 | 默认允许 |
| `WithdrawWindowHours` | 撤回时间窗（自发起时起算） | 空表示不限；超窗返回 `WithdrawWindowExpired` |
| `AutoPassWhenInitiator` | 审批人等于发起人时自动通过 | 默认关闭，按定义开启 |
| `FallbackApproverUserIds` | 审批人解析兜底 | JSON 数组；见第九节 |
| `PublishedTime` / `BasedOnDefinitionId` | 发布时间与来源版本 | 草稿追溯用 |

修改已发布定义 = 基于当前版本复制出新草稿（`CreateVersion` 拷贝含 `WithdrawWindowHours` 在内的全部字段与节点），发布时 `Version + 1` 并停用旧发布版本。运行中的实例永远绑定提交时的 `DefinitionId + Version`，不受新版本影响。

### 6.2 ApprovalFlowNode（节点定义）

| 字段 | 说明 |
| --- | --- |
| `Id` / `DefinitionId` | 归属定义版本 |
| `NodeKey` / `Name` | 版本内稳定键与展示名 |
| `NodeType` | 当前仅 `Approval` / `Cc`（图引擎阶段扩展为 `Start` / 网关 / `End`，见 `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md` 3.1） |
| `SortOrder` | 节点顺序，运行时的推进依据 |
| `ApproverType` | `SpecifiedUsers` / `Role` / `OrganizationManager` / `OrganizationMembers` / `Initiator` |
| `ApproverUserIds` | 指定审批人（JSON 数组；`SpecifiedUsers` 生效） |
| `ApproverRoleName` | 审批角色名（`Role` 生效） |
| `ApproverOrganizationId` | 审批组织（`OrganizationManager` / `OrganizationMembers` 生效） |
| `MultiApproveMode` | `All`（会签）/ `Any`（或签）/ `Sequential`（顺序会签） |
| `ConditionJson` | 激活条件；为空表示无条件激活 |
| `AllowTransfer` / `AllowAddSign` / `AllowReturnToInitiator` | 节点级操作开关 |
| `TimeoutHours` | 任务截止；空表示不限时 |
| `CcConfigJson` | 抄送人配置（仅 `Cc` 节点或审批节点完成后抄送） |

### 6.3 ApprovalInstance（审批实例）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 实例 ID | `Guid` |
| `TenantId` | 租户 | 宿主场景为空 |
| `TenantScope` | 租户范围（`host` 或租户 ID） | 不可空；与 `RunningKey` 组成在途唯一索引 |
| `Number` | 实例编号 | `IBusinessNumberGenerator`，业务键 `approval.instance` |
| `DefinitionId` / `DefinitionCode` / `DefinitionName` / `DefinitionVersion` | 定义快照 | 提交时固化 |
| `BusinessType` / `BusinessId` | 业务关联 | 业务类型 + 业务主键 |
| `Title` | 标题快照 | 列表展示不依赖业务表 |
| `InitiatorUserId` / `InitiatorOrganizationUnitId` | 发起人与归属组织 | 提交时固化，不随调岗漂移 |
| `Status` | `Running` / `Approved` / `Rejected` / `Returned` / `Withdrawn` / `Terminated` | 状态机见第七节 |
| `CurrentNodeSort` / `CurrentNodeKey` / `CurrentNodeName` | 当前节点游标与快照 | 无活动节点时 `CurrentNodeKey` 为空 |
| `ContextJson` | 条件求值上下文快照 | 提交与重提时写入 |
| `RunningKey` | 运行态唯一键 | 运行中 = `{BusinessType}:{BusinessId}`；进入终态置空。唯一索引 `(TenantScope, RunningKey)` 保证同一业务对象最多一个在途实例（空值不参与唯一约束，故历史终态实例可共存） |
| `CompletedTime` | 结束时间 | 终态或退回发起人时记录 |
| `Version` | 实例版本 | 全部状态推进按 `WHERE Id AND Version = @expected` 条件更新，失败返回 `ConcurrentModified` |

### 6.4 ApprovalTask（审批任务）

| 字段 | 说明 |
| --- | --- |
| `Id` / `InstanceId` | 归属实例 |
| `NodeId` / `NodeKey` / `NodeName` / `NodeSort` | 节点快照 |
| `TaskType` | `Approval` / `AddSignBefore` / `AddSignAfter` / `Transfer` |
| `AssigneeUserId` | 责任人快照，任务生成时解析固化 |
| `OriginAssigneeUserId` | 转交/加签来源人，可空 |
| `SourceTaskId` | 由哪条任务派生（转交、加签链） |
| `Status` | `Pending` / `Approved` / `Rejected` / `Skipped` / `Canceled` |
| `SortOrder` | 顺序会签内的先后 |
| `Comment` | 审批意见 |
| `DueAt` | 截止时间（节点 `TimeoutHours` 推导） |
| `CompletedAt` | 完成时间 |

### 6.5 ApprovalRecord（流转记录）

实例的全部事实时间线，同时充当业务审计：`Action` 枚举覆盖 `Submit` / `Approve` / `Reject` / `ReturnToPrevious` / `ReturnToInitiator` / `Resubmit` / `Withdraw` / `Terminate` / `Transfer` / `AddSign` / `Urge` / `TimeoutNotify` / `AutoPass` / `Cc`，携带操作人、节点快照、意见与 `DetailJson`。

### 6.6 ApprovalCc（抄送）

| 字段 | 说明 |
| --- | --- |
| `InstanceId` / `NodeKey` | 来源 |
| `UserId` | 接收人快照 |
| `ReadAt` | 已读时间，空为未读 |

## 七、实例状态机与操作语义

可编辑图源：[`diagrams/approval-state-machine.mmd`](../../diagrams/approval-state-machine.mmd)。

```text
        Submit                                  Approve(末节点)
  (none) ──────► Running ──────────────────────────────► Approved
                  │  │  │
     Reject(任一) │  │  └────────── Withdraw ────────────► Withdrawn
                  ▼  │
              Rejected │ ReturnToInitiator
                  │    ▼
                  │  Returned ──Resubmit──► Running（回到第一节点）
                  │    └──── Withdraw ────► Withdrawn
                  └──── Terminate(管理员) ─► Terminated
```

操作语义：

- **提交 Submit**：业务端口调用；校验定义已发布、同一业务对象无在途实例；写入上下文快照；从第一节点起按条件激活节点并生成任务；全部节点被条件跳过时报稳定错误码拒绝提交。
- **同意 Approve**：仅任务责任人可操作 `Pending` 任务；按 `MultiApproveMode` 判定节点完成：`All` 需全部同意；`Any` 一人同意即完成，其余待办置 `Skipped`；`Sequential` 按 `SortOrder` 依次激活。节点完成后激活下一节点；无后续节点则实例 `Approved` 并触发结果回调与通知。
- **拒绝 Reject**：实例直接 `Rejected`，其余待办全部 `Canceled`，触发结果回调与通知。首期"拒绝即结束"，不做拒绝后可选走向。
- **退回 ReturnToPrevious**：当前节点整组任务作废，重新激活上一 `Approval` 节点（重新生成任务）。
- **退回发起人 ReturnToInitiator**：实例进入 `Returned`，待办全部 `Canceled`；发起人修改业务数据后 `Resubmit`（重写上下文快照、回到第一节点、复用原实例与编号）或 `Withdraw`。
- **撤回 Withdraw**：仅发起人、仅 `Running`/`Returned` 状态、定义允许撤回且未超出 `WithdrawWindowHours` 时间窗（为空不限）；撤回后业务对象可重新发起。
- **终止 Terminate**：仅持 `approval:instance:terminate` 权限的管理员；必须填写原因，写审计。
- **转交 Transfer**：责任人把本人 `Pending` 任务转给当前租户内存在且启用的另一用户；原任务派生 `Transfer` 类型新任务并置 `Skipped`，记录来源人；节点配置关闭时禁止。转交发生在前加签链中时，派生任务完成后沿 `SourceTaskId` 回溯并恢复原审批任务。
- **加签 AddSign**：目标必须是当前租户内存在且启用的用户。前加签在本人任务前插入 `AddSignBefore` 任务，加签人完成后原任务才激活；后加签在本人同意后追加 `AddSignAfter` 任务，其完成才判定节点完成。两种加签都记 `SourceTaskId` 形成链。
- **催办 Urge**：仅发起人，对当前全部待办责任人发通知；同一实例同一操作人 1 小时内只允许一次，超限返回 `Sim:Approval:UrgeTooFrequent`。
- **自动通过 AutoPass**：节点激活时按定义开关处理"审批人=发起人"与"与上一节点审批人相同"，记录 `AutoPass` 流转记录，不生成任务。

同一业务对象的重复提交由 `(TenantScope, RunningKey)` 唯一索引兜底（并发发起时先查后插会漏，唯一索引是最终保证）；流程定义版本由 `(TenantScope, Code, Version)` 唯一索引保证同一租户范围不重复，并发创建冲突返回 `Sim:Approval:DefinitionVersionDuplicated`。状态推进与任务完成在同一工作单元内完成，每次同意、拒绝、转交、加签和退回操作都会先以实例 `Version` 条件更新抢占推进权，包括会签尚未结束的部分同意；版本条件更新失败返回 `Sim:Approval:ConcurrentModified`，任务状态条件更新失败同样视为已被他人定案。HTTP 应用服务与供业务模块调用的 `IApprovalSubmitService` 均建立事务边界；工作单元支持嵌套，内层提交不会提前提交外层事务，任一层回滚会终止整个事务。

## 八、节点类型与多人审批

- **审批节点**：生成一组任务，按 `MultiApproveMode` 完成。
- **抄送节点**：不生成任务，激活时写 `ApprovalCc` 并通知，立即推进下一节点。
- **会签 All**：任一拒绝即节点失败（实例 `Rejected`），其余待办 `Canceled`。
- **或签 Any**：第一人给出结论即节点定案，其余待办 `Skipped`；拒绝同样即刻定案。
- **顺序会签 Sequential**：同节点任务按 `SortOrder` 逐条激活，任一拒绝即刻定案。

首期不做并行分支：条件只决定"节点是否激活"，推进永远是单游标顺序前进。

## 九、审批人解析

解析器 `IApproverResolver` 在任务生成时把策略解析为具体用户集合并固化到任务，解析结果不随之后的人事变动改变：

| 策略 | 参数 | 解析规则 |
| --- | --- | --- |
| `SpecifiedUsers` | 用户 ID 数组 | 过滤停用账号 |
| `Role` | 角色名/角色代码 | 当前有效且启用账号的角色成员 |
| `OrganizationManager` | 审批组织 `ApproverOrganizationId` | 取所选组织的负责人（`ManagerUserId`）；组织不存在、未设负责人或负责人已停用时不解析，交由兜底审批人 |
| `OrganizationMembers` | 审批组织 `ApproverOrganizationId` | 取所选组织的直接成员（仅启用账号，不含下级组织）；未配置组织时不解析，交由兜底审批人 |
| `Initiator` | 无 | 发起人本人 |

兜底规则按序执行：

1. 解析结果为空 → 使用定义级 `FallbackApproverUserIds`。
2. 仍为空 → 提交或节点激活失败，实例停留并报稳定错误码，通知定义维护人。
3. 解析结果含发起人且定义开启 `AutoPassWhenInitiator` → 该人任务自动通过，其余人正常审批。

组织负责人身份只用于审批人定位，不授予任何功能权限，与组织设计文档的边界一致。

## 十、条件分支

条件作用于节点激活，求值对象是提交时固化的 `ContextJson`（扁平字典）：

- 规则结构：`Field` + `Operator`（`Eq` / `Ne` / `Gt` / `Ge` / `Lt` / `Le` / `In` / `Contains`）+ `Value`；同节点多条规则按 AND 组合。
- 类型化比较：按字段声明类型（字符串 / 十进制数字 / 日期 `yyyy-MM-dd`）解析后比较，不做字符串隐式强转。
- 字段清单由业务模块通过 `IApprovalBusinessDescriptor`（见第十一节）声明，定义维护界面据此提供下拉与校验，不允许手填未知字段。**当前实现补充**：业务类型未声明字段清单时（内建 `approval.generic` 即如此）不限制字段名，否则条件分支在任何业务类型下都无法保存；字段清单改由表单/字段元数据接管后恢复严格校验，见 `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md` 3.3。
- 节点按 `SortOrder` 依次求值；无条件节点恒激活。允许同一业务字段驱动多个互斥条件节点（如金额分段），等价于分支效果。

## 十一、业务模块集成端口

`Sim.Approval.Application.Contracts` 对外提供三类稳定端口，业务 Application 只引用 Contracts：

```csharp
public interface IApprovalSubmitService
{
    Task<ApprovalSubmitResult> SubmitAsync(ApprovalSubmitInput input, CancellationToken ct = default);
    Task<ApprovalSubmitResult> ResubmitAsync(Guid instanceId, ApprovalSubmitInput input, CancellationToken ct = default);
    Task WithdrawAsync(Guid instanceId, CancellationToken ct = default);
}

public interface IApprovalResultCallback          // 业务模块按需实现并登记
{
    string BusinessType { get; }
    Task OnApprovedAsync(ApprovalResultContext context, CancellationToken ct = default);
    Task OnRejectedAsync(ApprovalResultContext context, CancellationToken ct = default);
    Task OnWithdrawnAsync(ApprovalResultContext context, CancellationToken ct = default);
    Task OnTerminatedAsync(ApprovalResultContext context, CancellationToken ct = default);
}

public interface IApprovalBusinessDescriptor      // 业务模块登记元数据
{
    string BusinessType { get; }
    string DisplayName { get; }                    // 显示名，前端本地化 key 优先
    string? DefinitionCode { get; }                // 默认流程编码
    IReadOnlyList<ApprovalConditionFieldDto> ConditionFields { get; }
}

public interface IApprovalStatusQuery             // 业务侧展示"审批中/已通过"
{
    Task<ApprovalStatusSnapshot?> FindAsync(string businessType, string businessId, CancellationToken ct = default);
}
```

约定：

- 框架内建一个通用业务类型描述符 `approval.generic`（显示名「通用审批」，默认流程编码同名、无条件字段），供自助发起与联调；业务模块接入时声明自己的业务类型。
- 结果回调在实例进入终态的同一工作单元内按 `BusinessType` 同步派发；回调实现必须只执行同库、同工作单元内可回滚的数据变更，不得在回调中直接调用远程系统或产生不可回滚副作用。回调失败会回滚终态推进；跨进程副作用应通过 Outbox 集成事件实现最终一致。
- 用户可见通知（待办、结果、抄送、催办、超时）由 approval 模块统一经 `INotificationEventPublisher` 走 Outbox，业务模块不直接发通知。
- 需要跨进程消费审批事件的外部系统，后续经 Framework `IMessageBus` 增加 `approval.completed` 集成事件，首期不开放。

## 十二、通知、催办与超时

- 通知事件：`approval.todo`（新待办）、`approval.result`（通过/拒绝/终止，发发起人）、`approval.returned`（退回发起人）、`approval.cc`、`approval.urge`、`approval.timeout`。
- 全部走通知平台既有链路：审批事务内先写通知 Outbox，写入失败则审批状态整体回滚；Outbox 提交后的实际投递由通知平台重试。收件人只给 `UserId`，由账号目录解析联系方式。
- 超时扫描由 approval 模块通过 `IScheduleTaskDefinitionProvider` 登记 Worker 内置 Job（不可删除，可改 Cron/启停），默认每 5 分钟扫描 `DueAt` 到期且未通知过的待办；每条任务先以 `Status = Pending AND TimeoutNotifiedAt IS NULL` 条件原子抢占，再在同一短事务中写 `approval.timeout` Outbox、超时标记与审计记录，失败整体回滚供下一轮重试。首期超时只提醒，不自动通过/拒绝。
- 催办频控：同一实例同一操作人 1 小时（`ApprovalConsts.UrgeIntervalHours`）一次，超限返回 `Sim:Approval:UrgeTooFrequent`。

## 十三、API 设计

路由前缀 `/api/approval`，Swagger 分组 `approval`。普通用户操作不设功能权限码，授权完全由任务/实例归属判定；管理端操作使用权限码。

### 13.1 流程定义（`approval:definition:manage`）

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/approval/flow-definition` | 定义列表（含草稿、已发布与已停用，按编码与版本倒序） |
| `GET` | `/api/approval/flow-definition/{id}` | 定义详情含节点 |
| `POST` | `/api/approval/flow-definition` | 新建草稿（同编码取最新版本号 +1） |
| `PUT` | `/api/approval/flow-definition/{id}` | 更新草稿（节点整体覆盖） |
| `POST` | `/api/approval/flow-definition/{id}/publish` | 发布（发布后不可修改） |
| `POST` | `/api/approval/flow-definition/{id}/disable` | 停用（存在进行中实例时拒绝） |
| `POST` | `/api/approval/flow-definition/{id}/version` | 基于已发布版本另存新草稿版本（版本号 +1） |
| `DELETE` | `/api/approval/flow-definition/{id}` | 删除草稿（已发布或已停用版本不可删除） |
| `GET` | `/api/approval/business-type` | 已登记业务类型与条件字段（编辑器数据源） |
| `GET` | `/api/approval/business-type/users` | 当前租户启用用户的最小投影，支持关键词搜索，供审批人、转交和加签选择 |

### 13.2 用户端

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `POST` | `/api/approval/instance/submit` | 发起审批（同一业务对象同时只允许一个进行中实例） |
| `POST` | `/api/approval/instance/{id}/resubmit` | 被退回后重新提交（复用原实例，从首个节点重走） |
| `GET` | `/api/approval/instance/{id}` | 详情：定义快照 + 任务 + 流转时间线 |
| `GET` | `/api/approval/instance/my-pending` | 我的待办（分页） |
| `GET` | `/api/approval/instance/my-handled` | 我的已办（已通过、已拒绝与已取消） |
| `GET` | `/api/approval/instance/my-submitted` | 我发起的 |
| `GET` | `/api/approval/instance/my-cc` | 抄送我的 |
| `GET` | `/api/approval/instance/counts` | 待办、未读抄送与进行中发起的数量（角标；前端尚未接入） |
| `POST` | `/api/approval/instance/approve/{taskId}` | 同意（意见必填与否由节点配置） |
| `POST` | `/api/approval/instance/reject/{taskId}` | 拒绝（实例立即结束） |
| `POST` | `/api/approval/instance/transfer/{taskId}` | 转交 |
| `POST` | `/api/approval/instance/sign/{taskId}` | 前/后加签（前加签先于本人，后加签在本人通过后） |
| `POST` | `/api/approval/instance/return-to-previous/{taskId}` | 退回上一步（回到最近一个已通过的节点） |
| `POST` | `/api/approval/instance/return-to-initiator/{taskId}` | 退回发起人（等待重新提交） |
| `POST` | `/api/approval/instance/{id}/withdraw` | 发起人撤回（仅流程允许且实例进行中） |
| `POST` | `/api/approval/instance/{id}/urge` | 催办当前处理人 |
| `POST` | `/api/approval/instance/mark-cc-read/{ccId}` | 标记抄送记录已读 |

### 13.3 实例监控（`approval:instance:monitor` / `approval:instance:terminate`）

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/approval/monitor` | 全量实例分页（状态/业务类型/流程编码/发起人/关键字） |
| `GET` | `/api/approval/monitor/{id}` | 任意实例详情（含任务与审批记录） |
| `POST` | `/api/approval/monitor/{id}/terminate` | 强制终止进行中实例（原因必填） |

以上路径以 `web/src/api/definition/approval.json` 契约为唯一来源，前端通过 `web/src/api/approval` 的 SDK 调用，不手写路径。

## 十四、权限与数据范围

- 权限常量位于 `Sim.Approval.Domain.Shared`：`approval:definition:manage`、`approval:instance:monitor`、`approval:instance:terminate`，由种子幂等创建并挂到授权管理。
- 用户端操作不依赖功能权限码：待办任务的责任人、实例的发起人即授权依据；越权操作他人任务返回统一 `403` 与稳定错误码。
- 数据范围（组织设计落地后）：监控视图接入 `RoleDataScope`，资源编码 `approval.instance`，默认 `Self`（仅自己发起/审批的实例），监控角色可配组织范围；实例的 `InitiatorOrganizationUnitId` 即归属字段。
- 第三方系统接入（后续）：如需第三方系统发起审批，新增 `approval:instance:submit` Scope 走 client_credentials，首期不开放。

## 十五、前端信息架构

`web/src/modules/approval` 一个模块承载两个功能区，菜单由种子幂等创建、文案走后端本地化。

### 15.1 审批中心（用户端）

- 四个列表页：待办 / 已办 / 我发起的 / 抄送我的，各自以独立标签页打开并支持服务端分页。`instance/counts` 已提供待办、未读抄送与进行中数量，顶栏角标尚未接入。
- **不设统一发起入口**（已修正）：发起审批由各业务逻辑触发，表单引擎落地后由「提交表单」触发，审批中心不提供「发起审批」按钮；「我发起的」为只读视图，仅提供重新提交与撤回。原提交弹窗 `ApprovalSubmitModal` 已删除。参见 `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md` 7.3。
- 待办列表突出业务类型、标题、发起人、当前节点、到达时间与截止状态（临期/超时用文本标签，不只用颜色）。
- 详情页自上而下：业务摘要（业务模块提供的标题与上下文关键字段）→ 流转时间线 → 操作区（同意/拒绝/退回/转交/加签，按节点配置渲染）。
- 移动端收敛为单列卡片流，操作区固定底部，触控区域不小于 44 px。

### 15.2 审批管理（管理端）

- 流程定义列表：按编码与版本倒序展示草稿、已发布与已停用版本，业务类型以列与筛选呈现，不做分组折叠。
- 定义编辑器：首期采用"节点卡片序列"表单而非画布——节点按顺序排列，每张卡片配置名称、审批人策略（按策略类型切换字段）、多人方式、条件（字段下拉 + 操作符 + 值）、操作开关与超时；支持拖拽排序与插入。发布前做完整性校验（至少一个审批节点、兜底审批人、条件字段合法）。**第二阶段改为画布式拖拽设计器**，见 `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md` 第八节。
- 实例监控：状态/业务类型/时间范围过滤，详情只读时间线 + 终止操作。

## 十六、并发、缓存与一致性

- 实例状态推进使用 `Version` 条件更新（`WHERE Id = @id AND Version = @expected`，成功即 `Version + 1`）；定义使用 `Code + Version` 不可变快照，草稿编辑不做乐观并发。所有写接口冲突返回稳定错误码，前端提示后刷新。
- 或签/会签的并发完成：任务完成条件更新带 `Status == Pending` 谓词，受影响行数为 0 即视为已被他人定案。
- 在途唯一性由 `(TenantScope, RunningKey)` 唯一索引保证，不依赖先查后插；宿主场景 `TenantId` 为空，故索引列使用恒有值的 `TenantScope`（见 `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md` 第五节实施偏差）。
- 已发布定义按 `TenantId + Code` 缓存，发布/停用时主动失效；实例与任务不缓存，待办列表始终读库。
- 审批人解析是一次性快照：组织调整、角色回收不影响已生成任务，只影响新任务；这与组织设计文档"归属字段创建时写入"的口径一致。
- 通知事件写入 Outbox 与审批状态推进处于同一工作单元，Outbox 写入失败会回滚审批；事务提交后的通道投递失败由通知平台重试（结果回调仍按第十一节保持强一致）。

## 十七、审计

- `ApprovalRecord` 记录实例全量流转事实，含操作人、节点、意见与时间。
- 定义的新建、修改、发布、停用记录变更前后摘要（节点数、审批人策略哈希），沿用框架审计约定。
- 管理员终止、审批人解析兜底命中、解析失败必须落审计。
- 审计不保存业务上下文全文明文，`ContextJson` 中由业务描述符标记为敏感的字段在记录前脱敏。

## 十八、测试策略

- 单元测试：状态机推进矩阵（串行/会签/或签/顺序会签 × 同意/拒绝）、单出边与多出边条件求值、共享汇合节点剪枝、审批人解析（含兜底与自动通过）、加签/转交来源链、无有效审批节点拒绝自动通过。
- TestHost 集成测试：提交→多级→通过全链路；退回发起人→重提；撤回与审批并发（一方必失败）；越权操作他人任务；租户隔离；同一业务对象重复提交被唯一索引拒绝；超时 Job 只通知一次；回调失败导致终态回滚。
- 前端：定义编辑器校验、审批中心四视图、详情操作区按节点配置渲染。

## 十九、实施阶段

| 阶段 | 内容 | 前置 |
| --- | --- | --- |
| 0 | 组织树、成员关系、组织负责人（组织设计第一阶段） | 无 |
| 1 | 定义/节点/实例/任务/记录表与状态机；指定人与角色审批人；提交/同意/拒绝/撤回；审批中心四视图；通知接入；定义管理列表与编辑器 | 无（可与阶段 0 并行） |
| 2 | 条件分支、会签/或签/顺序会签、退回/重提、转交、加签、抄送、组织负责人审批人 | 阶段 0、1 |
| 3 | 超时扫描与催办、实例监控与终止、业务模块提交审批接入示例、完整测试矩阵 | 阶段 2 |

每阶段交付都按 `AGENTS.md` 要求同步根 `README.md`、`ARCHITECTURE.md`、模块 README 与本指南基线。

## 二十、完成定义

- 同一业务对象任意时刻最多一个在途实例，由数据库唯一约束保证。
- 审批动作只对当前待办任务责任人生效，并发定案有确定结果。
- 任务审批人是生成时快照，人事变动不改变在途任务归属。
- 已发布定义不可变，新版本不影响在途实例。
- 条件字段在业务描述符声明字段清单时只能来自该清单，未知字段在保存时被拒绝；未声明清单的业务类型不做字段名限制（原因见第十节）。
- 退回-重提复用原实例与编号，时间线完整保留。
- 审批结果回调与实例终态强一致；通知至少一次且可重试。
- 全部写接口具备乐观并发、租户隔离、稳定错误码与中英文资源。
- 管理端编辑器无法保存"无审批节点、无兜底审批人、条件字段非法"的定义。
- Web 在 375、768、1024、1440 px 可用，键盘可完成审批操作。
- 后端具备会签/或签/顺序、退回重提、撤回竞态、越权、租户、重复提交、超时幂等的测试矩阵。

## 二十一、明确不做

首期不包含（其中画布式流程设计器与并行 fork/join 分支已在**第二阶段**推进，见 `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md`）：

- ~~BPMN 通用流程引擎语义与画布式流程设计器。~~（第二阶段做：有向图语义 + 画布式设计器）
- ~~并行 fork/join 分支与分支合并。~~（第二阶段做：排他网关 + 并行网关）
- 超时自动通过/自动拒绝（首期只提醒）。
- 在途实例迁移到新定义版本。
- 电子签名、手写签批与印章。
- 第三方系统接入发起审批（后续以独立 Scope 开放）。
- 跨模块的流程模板市场与流程克隆分享。
