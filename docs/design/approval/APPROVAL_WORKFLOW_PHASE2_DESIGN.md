# 审批流第二阶段设计：有向图引擎与画布式流程设计器

> 本文是 `docs/design/approval/APPROVAL_WORKFLOW_DESIGN.md`（第一阶段）的续篇。
> 第一阶段的设计、状态机、动作语义、权限与通知链路继续有效，本文只描述**增量与替换**部分。
> 本文推进第一阶段第二十一节「明确不做」中与本阶段相关的条目，并在第十四节给出对该节的修订。

## 一、本阶段范围

### 1.1 背景

第一阶段交付了可用但**线性**的审批引擎：节点是一条有顺序的序列，条件只决定「某个节点是否激活」，推进永远是单游标前进。
同时前端把审批当作「表格数据管理」：三个用户列表页、两个管理页都是同一个「筛选框 + 表格」外壳，审批动作用弹窗完成，流程定义用有序卡片列表编辑。这与主流审批产品（钉钉、飞书）的交互相差很远。

用户已确认的四个方向：

1. 流程定义改为**画布式可视化拖拽设计**。
2. 推进第一阶段的「明确不做」条目：**并行 fork/join 分支与分支合并**、**画布式流程设计器**。
3. **不设统一的发起审批入口**：发起由各业务逻辑触发；后续建设表单引擎，表单可关联流程定义，提交表单即触发对应审批流。
4. 后端补齐：**并发原子性**、**规则与文档对齐**、**表单/字段元数据模型**、**并行/网关分支**。

### 1.2 推进的非目标（对应第一阶段第二十一节）

| 第一阶段「明确不做」 | 本阶段处理 |
| --- | --- |
| BPMN 通用流程引擎语义与画布式流程设计器 | **做**：不做全量 BPMN，但引入有向图语义与画布式设计器（第八节） |
| 并行 fork/join 分支与分支合并 | **做**：引入并行网关与 join 汇合（第三、四节） |
| 超时自动通过/自动拒绝（首期只提醒） | **仍不做**：保持「只提醒」，仅修正文档与实现的周期漂移 |
| 在途实例迁移到新定义版本 | 仍不做 |
| 电子签名、手写签批与印章 | 仍不做 |
| 第三方系统接入发起审批 | 仍不做 |
| 跨模块的流程模板市场与流程克隆分享 | 仍不做 |

### 1.3 不在本阶段范围

- 完整表单引擎（表单设计器、表单渲染器、表单数据持久化）。本阶段只定义**审批侧的表单/字段契约**，使表单引擎可对接（第九节）。
- 排他网关之外的复杂网关（包容网关 Inclusive、事件网关）。本阶段实现**排他网关 + 并行网关**两种。

## 二、现状与差距（实测口径）

以下均为代码实测结论，不是设计意图。

| 能力 | 现状 | 依据 | 本阶段处理 |
| --- | --- | --- | --- |
| 结构模型 | 节点线性序列 `SortOrder` | `ApprovalFlowEntities.cs:66` | 升级为节点 + 连线（3.1） |
| 运行时节点状态 | **查询时派生**，无持久化 | `ApprovalQueryService.cs:454` `BuildNodeProgress` | 必须持久化（3.2） |
| 分支 | 仅节点级条件，单游标 | `ApprovalEngine.AdvanceAsync` | 排他/并行网关（第四）
| 并发原子性 | `Version` 只自增不参与条件；`ConcurrentModified` 定义且已本地化但全仓零引用；`RunningKey` 非唯一索引 | `ApprovalErrorCodes.cs:84` | 原子化改造（第五节） |
| 字段元数据 | `context` 为扁平 `Dictionary<string,string>`；唯一描述符 `approval.generic` 的 `ConditionFields` 为空 | `GenericApprovalBusinessDescriptor.cs:26` | 字段 schema + 快照（3.3） |
| 附件 | 无 | — | 新增（3.4） |
| 条件字段校验 | 描述符声明字段为空时仍拒绝任意字段 → 条件分支在现网永远无法保存 | `FlowDefinitionAppService.cs:270-282` | 已修正为「未声明即不限制」，并由字段 schema 接管（3.3） |
| 时间线 | 记录无 from→to；抄送创建/已读不记流水 | `ApprovalRuntimeEntities.cs:153` | 补 from→to 与抄送流水（3.2） |
| 催办频控 / 撤回窗口 | 文档称「1 小时一次」「Running/Returned」，实现无频控、仅 Running | `ApprovalEngine.cs:348`、`EnsureCanWithdraw:44` | 规则与文档对齐（第五节） |
| 组织主管链 | 只取 `managers[0]`，链深丢失 | `ApprovalApproverResolver.cs:61` | 支持上移层级 N（第五节） |
| 角标 | `instance/counts` 后端与前端 SDK 均就绪，前端零调用 | `web/src/modules/approval/api/index.ts:111` | 接入侧边栏角标（7.4） |
| 前端信息架构 | 6 个平级表格菜单；详情页渲染裸 key→value | `web/src/modules/approval/routes.ts` | 重构（第七节） |

## 三、领域模型变更

### 3.1 流程定义从线性序列升级为有向图

保留 `ApprovalFlowDefinition` 与 `ApprovalFlowNode`，新增连线表，并把布局与语义分离。

```csharp
/// <summary>流程连线：图的边。条件仅对排他网关出边与条件出边生效。</summary>
public sealed class ApprovalFlowEdge : AuditedAggregateRoot
{
    public Guid DefinitionId { get; set; }
    public string FromNodeKey { get; set; } = string.Empty;
    public string ToNodeKey { get; set; } = string.Empty;
    /// <summary>出边条件；为空表示默认边，仅在所有条件边均不成立时选中。</summary>
    public string? ConditionJson { get; set; }
    /// <summary>连线上显示的标签，仅用于画布展示。</summary>
    public string? Label { get; set; }
    public int SortOrder { get; set; }
}
```

节点类型扩展（`ApprovalNodeType`）：

| 取值 | 含义 | 出边约束 | 运行时行为 |
| --- | --- | --- | --- |
| `Start` | 唯一入口 | 恰好 1 条 | 实例创建时激活 |
| `Approval` | 审批节点 | 1 条（无条件）或 N 条（带条件，等价分支） | 生成任务组 |
| `Cc` | 抄送节点 | 1 条 | 写抄送记录后立即推进 |
| `ExclusiveGateway` | 排他网关（条件分支） | ≥1 条，按 `SortOrder` 取首个条件成立者 | 不生成任务，决定走向 |
| `ParallelGateway` | 并行网关（fork 与 join 复用同一类型） | fork ≥2 条；join ≥1 条入边 | 不生成任务；入边全到才推进 |
| `End` | 结束节点 | 0 条 | 实例通过 |

**布局与语义分离**：画布坐标不进入引擎模型，存于定义级字段。

```csharp
// ApprovalFlowDefinition 新增
public string? LayoutJson { get; set; }   // { "node1": { "x": 120, "y": 80 }, ... }

落库时显式使用 `[SugarColumn(ColumnDataType = "text", IsNullable = true)]`；SqlSugar 在 PostgreSQL 下会把 `Length = 8000` 生成为非法 `varchar(max)`，从而中断 CodeFirst 建表。
```

这样引擎只关心拓扑，画布改版不影响运行语义。

**兼容性**：`ApprovalFlowNode.SortOrder` 保留，用于排他网关出边求值顺序与线性流程的稳定排序。

### 3.2 运行时节点状态必须持久化

现状是查询时用「任务 + 抄送」推导节点进度，无法表达「join 正在等待」与「该分支未被选中」。新增：

```csharp
/// <summary>实例内节点状态：join 等待与死分支传播需要持久化。</summary>
public sealed class ApprovalInstanceNode : AuditedAggregateRoot
{
    public Guid InstanceId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ApprovalNodeState State { get; set; }   // Pending | Active | Completed | Skipped
    public DateTime? ActivatedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public int Round { get; set; }                 // 激活轮次，退回重提后递增
}

public enum ApprovalNodeState { Pending, Active, Completed, Skipped }
```

就绪判定不依赖额外计数器：`IsReady` 直接由入边源节点的 `State` 推导「到达 / 定案」数量（§4.2），因此不设 `TokenCount` 字段，避免同一事实两处存储导致漂移。

实例创建时按已发布定义把全部节点写入 `ApprovalInstanceNode`（`Pending`），运行中只更新状态。`BuildNodeProgress` 改为直接读该表，并按 `SortOrder` 排序。

同时补齐两处观测缺口：`ApprovalRecord` 增加 `FromNodeKey` / `ToNodeKey`；抄送创建与标记已读写入 `ApprovalRecord`（启用已定义但未使用的 `ApprovalAction.Cc`）。

### 3.3 表单与字段元数据

第一阶段的 `context` 是扁平字典，详情页只能渲染裸 `key=value`，也无法支撑「业务单据 + 审批流」的观感。本阶段定义审批侧的字段契约（表单引擎可对接）：

```csharp
/// <summary>审批侧字段定义：既作为条件字段来源，也作为详情页只读渲染依据。</summary>
public sealed class ApprovalFieldDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ApprovalFieldType Type { get; set; }   // String | Number | Decimal | Date | DateTime | Bool | Select | Text | Attachment
    public IReadOnlyList<ApprovalFieldOptionDto> Options { get; set; } = [];  // Select 用
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
}

public enum ApprovalFieldType { String, Number, Decimal, Date, DateTime, Bool, Select, Text, Attachment }
```

- `IApprovalBusinessDescriptor` 的 `ConditionFields` 由 `Fields`（`IReadOnlyList<ApprovalFieldDto>`）取代；条件字段 = `Fields` 中可用于比较的子集（`Attachment` 除外）。
- 实例快照从「扁平字典」升级为**带标签的字段值数组**，使详情页不依赖业务模块即可渲染：

```csharp
public sealed class ApprovalContextSnapshot
{
    public List<ApprovalContextFieldDto> Fields { get; set; } = [];       // key/displayName/type/options/value
    public List<ApprovalAttachmentDto> Attachments { get; set; } = [];
}
```

- `ApprovalInstance.ContextJson` 存 `ApprovalContextSnapshot`。读取时若内容是旧格式扁平字典，映射为 `Fields`（`DisplayName = Key`、`Type = String`）——**旧实例照常可用**。
- PostgreSQL 下 `ContextJson` 同样显式映射为 `text`，避免 `Length = 8000` 触发同类 DDL 错误。

实现口径（与上述设计的三处收口）：

- 快照存**字段值与元数据**（`ContextJson` = `{ "fields": [ { key, displayName, type, options, value } ] }`），附件的元数据落在 `ApprovalAttachment` 表（§3.4），不进快照。2026-11-23 修正：`Options` 由「读取时按当前描述符补齐」改为随快照固化，详情读取不再回查业务类型声明，保证描述符改名、删选项后历史实例展示不变（原 `ApprovalFieldSchema.ApplyDeclaredMetadata` 已移除）。
- 线契约保持 `Type` 为字符串（枚举取值名，如 `"Decimal"`），与既有 DTO 一致（未启用 `JsonStringEnumConverter`）；无法识别的类型回落 `String`。
- 字段值解析收敛在 `ApprovalFieldSchema`：`Context`（扁平字典）与 `Fields`（字段值数组）两种形态都接受，同键以 `Fields` 为准；`Required` 校验在**服务端**执行（`EnsureRequiredFields`，缺失/空值抛 `InvalidRequest`），前端不作为唯一防线。
- 条件字段白名单由 schema 驱动：`FlowDefinitionAppService.ValidateDeclaredConditionFields` 取声明中 `IsConditionComparable()` 的字段（即 `Attachment` 之外的）作为可比较集合，声明为空的业务类型不做限制。

### 3.4 附件

```csharp
/// <summary>审批附件：挂在实例或具体任务上。</summary>
public sealed class ApprovalAttachment : AuditedAggregateRoot
{
    public Guid InstanceId { get; set; }
    public Guid? TaskId { get; set; }
    public string FileId { get; set; } = string.Empty;   // 复用既有文件存储，只存标识
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public Guid UploaderUserId { get; set; }
}
```

文件本体走框架既有存储链路，审批模块只保存标识与元数据，不引入新的存储实现。

实现口径与**对设计的偏离**：

- `ApprovalAttachmentDto` 只暴露 `Id` / `InstanceId` / `TaskId` / `FileName` / `Size` / `UploaderUserId` / `CreationTime`，**不下发存储键与提供方**：附件只能通过审批模块的鉴权下载端点获取，客户端拿不到可绕开权限的存储标识。
- **偏离：不签发下载直链，改为鉴权代理下载。** 原计划复用 `IFileManagementService.GetDownloadUrlAsync` 的预签名/HMAC 直链，实测该链路在本地/数据库提供方下不可用——生效的 `DatabaseFileManagementOptionsProvider.GetAsync()` 不回填 `PublicBaseUrl` 与 `AccessUrlSigningKey`，也未见任何配置来源，`GenerateAccessUrlAsync` 必然抛 `InvalidOperationException`（框架侧缺陷，另行记录，不在本阶段修复）。因此附件下载改为 `GET /api/approval/instance/attachment-download/{attachmentId}`：服务端判可见性（发起人 / 任一任务审批人 / 抄送人）后把字节流回吐，与既有内容文件 `blob` 消费方式一致。
- 上传权限与状态：实例级上传限发起人且实例处于 `Running` / `Returned`；任务级上传限该任务审批人且实例 `Running`，与审批动作同一套守卫错误码（`NotInitiator` / `NotTaskAssignee` / `InstanceNotRunning`）。
- 不可见或不存在统一返回 `Sim:Approval:AttachmentNotFound`，不区分「无此附件」与「无权查看」，避免探测。

## 四、运行时语义：token 激活与 join

### 4.1 节点与连线状态

- 节点状态：`Pending`（未到达）/ `Active`（已到达且未完成）/ `Completed`（已完成）/ `Skipped`（分支未选中，永不到达）。
- 连线不单独存状态，由两端节点状态推导：源 `Completed` 且目标非 `Pending` → 该边走通；源 `Skipped` → 该边死亡。

### 4.2 就绪判定与死分支传播

节点 `T` 就绪（可激活）当且仅当：

1. `T.State == Pending`；
2. `T` 至少有一条入边的源节点 `Completed`（**至少一路到达**）；
3. `T` 的所有入边，其源节点要么 `Completed`、要么 `Skipped`（**不再等待**）。

`ParallelGateway` 作为 join 时取**全部**入边都 `Completed` 才推进（等待语义）；其余节点取「至少一路 + 不再等待」。为此网关节点额外记录 `JoinPolicy`：

```csharp
public enum ApprovalJoinPolicy { AnyArrival, AllArrival }
```

- join 网关：`AllArrival`（入边全 Completed 才推进）。
- 排他网关、普通节点：`AnyArrival`。

落到实现（`ApprovalFlowGraph.IsReady`）时，两种策略统一表述为「至少一路到达 + 不再等待」的两种强度：

- `AnyArrival`：`到达入边数 > 0` 即推进；
- `AllArrival`：`到达入边数 > 0` 且 `入边已定案数（Completed + Skipped）== 入边总数` 才推进——即等全部到达，但一条分支确定死亡后不再为它等待。

**实施偏差（多入边 End）**：多入边的 `ParallelGateway` 与多入边的 `End` 的有效汇聚策略被强制为 `AllArrival`（`ApprovalFlowGraph.ResolveJoinPolicy`），即使定义里存的是 `AnyArrival` 也如此。并行 join 本就是等待语义；`End` 多入边若按 `AnyArrival`，会在一条分支先到达时把实例判为通过并静默跳过另一分支的在途任务。单入边节点两种策略等价，不参与强制；`Approval` / `Cc` / 排他网关节点的多入边仍按定义 `JoinPolicy` 判定。

**死分支传播**：排他网关选定一条出边后，其余出边指向的独占分支标记 `Skipped`，并沿图向下递归传播——把因此永远无法就绪的节点置 `Skipped`。如果未选出边直接指向的节点也能从已选路径到达（典型场景是共享汇合节点），该节点必须保留。传播在每次推进后收敛，保证 join 不会永久等待一条不可能到达的分支，也不会误剪共享下游。

### 4.3 网关语义

| 网关 | fork | join |
| --- | --- | --- |
| `ParallelGateway` | 出边全部激活，各分支并行推进 | 入边全到达（`AllArrival`）才推进一次 |
| `ExclusiveGateway` | 按 `SortOrder` 取首个条件成立的出边（无条件视为默认）激活，其余分支 `Skipped` | 按 `AnyArrival` |

### 4.4 算法

```
Advance(instance):
  loop:
    changed = false
    for node in instanceNodes where State == Pending and IsReady(node):
        Activate(node); changed = true
    if not changed: break
  PropagateSkippedBranches(instance)
  if no node is Pending or Active: CompleteInstance(instance, Approved)
```

`Activate(node)` 分派：

- `Start` / `ExclusiveGateway` / `ParallelGateway`：不生成任务，置 `Completed`，其出边目标进入下一轮 `IsReady` 判定。
- `Approval`：节点自身条件成立时生成任务组并置 `Active`；条件不成立时按免审节点置 `Completed` 并继续沿出边推进。节点条件与网关未选分支是两种语义，后者才置 `Skipped`。多人模式复用第一阶段语义（`All` / `Any` / `Sequential`）。
- `Cc`：写抄送记录与通知，置 `Completed`，立即推进。
- `End`：置 `Completed`。

节点完成（任务全部给出结论）后：置 `Completed`，回到 `Advance`。任一分支 `Reject` 时实例立即 `Rejected`（保持第一阶段「拒绝即结束」语义），其余待办 `Canceled`、其余节点 `Skipped`。当图收敛且没有待办时，引擎还会检查当前节点轮次至少生成过一条审批任务；所有审批节点均被条件绕过时返回定义错误，禁止空流程自动通过。

### 4.5 与第一阶段线性流程的等价性

线性定义映射为 `Start → n1 → n2 → … → nN → End`，每个节点单出边、`AnyArrival`。此时：

- 任意时刻最多一个节点 `Active`；
- 永远不会触发 `AllArrival` 等待；
- 死分支传播恒不触发（无分支）。

因此第一阶段的行为是第二阶段的一个特例。**必须有一条等价性测试**守住这个结论（第十二节）。

## 五、并发与一致性改造

| 项 | 改造 |
| --- | --- |
| 任务定案原子性 | 完成任务的更新带条件：`WHERE Id = @id AND Status = 'Pending'`；受影响行数为 0 → 已被他人定案，返回稳定冲突错误码 |
| 实例状态推进 | 每个审批写动作先执行 `WHERE Id = @id AND Version = @expected` 并令 `Version = Version + 1`，包括会签部分同意、转交、加签与退回；失败返回 `ConcurrentModified` |
| 在途唯一性 | 建**唯一**索引，不再依赖先查后插。落点列见下方「实施偏差」 |
| 撤回规则 | 允许 `Running` 与 `Returned`；定义级 `AllowWithdraw` 与时间窗（定义级 `WithdrawWindowHours`，为空不限）共同判定 |
| 催办频控 | 同实例同操作人 1 小时内一次，超限返回稳定错误码（与文档一致） |
| 组织主管链 | `OrganizationManager` 策略支持「起算方式 + 上移层级 N」，沿组织树向上取第 N 层 `ManagerUserId`，不再只取 `managers[0]`（2026-10-12 起被取代：`OrganizationManager` 改为解析节点所选组织的负责人，并新增 `OrganizationMembers` 解析所选组织直接成员，字段 `ApproverOrganizationId` 取代 `ManagerLevelOffset`） |
| 超时扫描周期 | 统一为 5 分钟（以文档为准，修正实现与常量的漂移） |
| 通知 Outbox | Outbox 写入与审批状态推进处于同一工作单元，写入失败向上抛出并回滚；事务提交后的实际通道投递由通知平台重试 |

SqlSugar 落点：使用 `Updateable<T>().Where(...).SetColumns(...)` 并检查返回的受影响行数，不依赖框架隐式版本校验，便于测试与移植。

**实施偏差（已落地，共 2 处）**

1. **唯一索引列为 `(TenantScope, RunningKey)` 而非 `(TenantId, RunningKey)`。** `TenantId` 在宿主（无租户）场景为 `NULL`，SQLite / PostgreSQL 的唯一索引不约束 `NULL`，等于约束失效。`ApprovalInstance` 因此新增不可空列 `TenantScope`（`host` 或租户 ID，取自 `TenantContext.CurrentTenantId`），与 `BusinessNumberDefinitionEntity.TenantScope` 的既有做法一致。`RunningKey` 终态置空仍依赖「`NULL` 不参与唯一约束」这一语义。
2. **条件更新不填充审计列。** 框架 AOP 的 `DataExecuting` 只处理 `UpdateByObject`（按实体整体更新），`SetColumns` 不会被自动填充，因此 `LastModificationTime` / `LastModifierId` 由调用方显式写入，操作人沿引擎内部推进链以 `actorUserId` 参数透传，不向仓储注入 `ICurrentUser`。

## 六、API 变更

沿用 `/api/approval` 前缀与既有动词约定（按前缀自动生成 REST），本阶段变更：

### 6.1 流程定义

| 方法 | 路径 | 变更 |
| --- | --- | --- |
| `GET` | `/api/approval/flow-definition/{id}` | 详情增加 `edges`、`layout`、节点 `joinPolicy` |
| `PUT` | `/api/approval/flow-definition/{id}` | 请求体由 `nodes` 扩展为 `nodes` + `edges` + `layout`（整体覆盖） |
| `POST` | `/api/approval/flow-definition/{id}/validate` | 新增：发布前校验（第八节规则），返回逐条问题 |
| `GET` | `/api/approval/business-type` | 返回 `fields`（`ConditionFields` 的替代） |
| `GET` | `/api/approval/business-type/users` | 新增审批专用用户搜索，只返回当前租户启用账号的 ID、用户名与显示名 |

### 6.2 用户端

| 方法 | 路径 | 变更 |
| --- | --- | --- |
| `POST` | `/api/approval/instance/submit` | **保留为唯一发起入口（API 层）**；请求体 `context` 兼容扁平字典与字段值数组两种形态 |
| `POST` | `/api/approval/instance/{id}/upload-attachment` | 新增：上传附件（挂实例，限发起人） |
| `POST` | `/api/approval/instance/upload-task-attachment/{taskId}` | 新增：审批人上传附件（挂任务） |
| `GET` | `/api/approval/instance/attachment-download/{attachmentId}` | 新增：鉴权代理下载（**替代原设计的签名直链**，理由见 §3.4） |
| `GET` | `/api/approval/instance/{id}` | 详情增加 `context.fields`（带标签）、`attachments`、节点 `state` 与记录的 from→to |

实际路径由 `AppServiceConvention` 从方法名推导（无 `id` 形参时按唯一 `*Id` 形参命名），与上表一致；不带 `/attachments` 这类自定义路径段——本项目的路由不由特性声明。

其余用户端与管理端端点不变。

## 七、前端信息架构重构

### 7.1 审批中心（用户端）

侧边栏收敛为**一个入口 + 角标**，内部用 Tab 切换，取代当前 6 个平级表格菜单：

```
审批中心  (角标 = 待办数)
├── 待办      默认 Tab
├── 已办
├── 抄送我的
└── 我发起的
```

### 7.2 实例详情 = 审批工作台

详情页是审批发生的地方，不再用弹窗：

```
┌ 标题 / 实例编号 / 状态标签 ────────────────────────┐
├ 业务单据（按字段元数据只读渲染：字段名 + 值 + 附件）  │
├ 流转时间线（竖向：发起 → 各节点 → 结束，含 from→to）  │
└ 底部固定操作条（按当前节点配置渲染）                  │
   同意 / 拒绝 / 转交 / 前加签 / 后加签 / 退回上一节点 / 退回发起人
```

- 操作条只在「我是当前待办责任人」时出现；发起人额外看到撤回/催办；退回后看到重新提交。
- 移动端单列，操作条固定底部，触控区域不小于 44px。

### 7.3 发起审批入口的处理

- **移除** `ApprovalSubmitModal` 与所有「发起审批」UI 入口（`web/src/modules/approval/components/ApprovalSubmitModal.vue` 整文件删除）。
- 发起能力只在 API 与端口层存在：业务模块通过 `IApprovalSubmitService` 触发，后续由表单引擎在「提交表单」时触发（第九节）。
- `approval.generic` 业务类型保留，作为表单引擎接入前的默认业务类型与联调入口，但其「自助发起」定位作废，描述符注释同步修订。
- 「我发起的」只读：列表 + 详情，详情里提供重新提交与撤回，不再提供新建。

### 7.4 角标

`GET /api/approval/instance/counts` 返回待办数、未读抄送数、进行中发起数，接入侧边栏「审批中心」角标与 Tab 上的计数；在审批动作成功后与路由进入审批中心时刷新。

### 7.5 管理端

流程定义与实例监控移入「审批管理」分组，仅持 `approval:definition:manage` / `approval:instance:monitor` 的角色可见。流程定义入口打开画布设计器（第八节），不再打开卡片编辑器。

## 八、画布式流程设计器

### 8.1 技术选型

**复用已有依赖，不引入新包**：`@vue-flow/core`、`@vue-flow/background`、`@vue-flow/controls`、`@vue-flow/minimap` 已在 `web/package.json`（41-44 行）且已被 `src/components/MarkdownEditor/CanvasBoard.vue` 使用。设计器位于路由级懒加载分片，不进首屏，不触碰 1.50 MiB 首屏预算与 1 MiB 异步分片预算。

**样式依赖必须显式导入**：Vue Flow 的容器、节点、连线和 Handle 依赖 `@vue-flow/core/dist/style.css` 与 `theme-default.css`。审批设计器必须与 `CanvasBoard.vue` 一样在入口 SFC 中导入这两份样式，不能仅依赖业务样式；缺失时画布会失去基础定位与交互布局，表现为节点、连线和连接点无法正常使用。

### 8.2 图 ↔ 契约映射

| 画布 | 契约 |
| --- | --- |
| 节点 | `nodes[]`：`nodeKey` / `name` / `nodeType` / `joinPolicy` / 审批人策略 / 多人模式 / 开关 / 超时 |
| 节点坐标 | `layout[nodeKey] = { x, y }`（定义级 `layout`，引擎不读） |
| 连线 | `edges[]`：`fromNodeKey` / `toNodeKey` / `conditionJson` / `label` / `sortOrder` |
| 连线上的条件编辑 | 排他网关出边与带条件出边写入 `conditionJson` |

节点属性面板替代现有 `ApprovalFlowNodeCard`：选中节点后右侧抽屉编辑，字段与现卡片一致（审批人策略、多人方式、超时、操作开关、抄送配置、条件）。现有卡片组件在画布内不再使用，可删除。

### 8.3 交互

- 左侧节点面板拖拽出节点；Delete 删除选中节点/连线。
- 连线不设固定连接点：节点四边均可作为起终点，从任一边缘拖出、接近目标节点边缘时按最近边吸附（Vue Flow `ConnectionMode.Loose` + `connectionRadius`）；已建立连线的端点取两节点中心连线与边框的交点，随节点移动动态贴边，而不是钉在某个桩位上。方向约束（`Start` 不可被指向、`End` 不可引出）由连接点 `connectableStart` / `connectableEnd` 与前后端校验共同保证；连接点方位仅在画布会话内辅助渲染与改接，不进入保存契约。
- 支持框选、缩放、适配视图、迷你地图（Vue Flow 内建）。
- 右侧抽屉编辑选中节点/连线属性；底部实时显示校验问题。
- 键盘可完成「选中 → 编辑 → 连线 → 删除」，满足可访问性要求。

### 8.4 发布前校验

由后端 `POST /validate` 与前端共用同一套规则（前端即时提示，后端权威判定）：

1. 恰好一个 `Start`，至少一个 `End`。
2. 所有节点从 `Start` 可达；无孤立节点。
3. 出边规则：`Start` 恰好 1 条；`Cc` 恰好 1 条；`Approval` 与网关允许 1 条或 N 条；其余非 `End` 节点至少一条出边；`End` 不允许有出线。
4. 连线方向必须从节点出口（source）引出、从入口（target）引入：`Start` 不能被指向，`End` 不能引出。
5. 每个 `Approval` 节点：指定人策略须有审批人；角色策略须有角色。
6. 每个 `Cc` 节点至少一个抄送对象。
7. 条件字段必须来自业务类型声明的 `Fields`（未声明字段清单时放行——与当前实现一致，避免条件分支在任何业务类型下都不可用）。
8. `ParallelGateway` 的出边不得携带条件（并行语义下条件无意义）；其余多出边节点（审批节点、排他网关）必须保留且只能保留一条无条件的默认出边，默认边仅在所有条件边均不成立时选中。
9. 不允许存在任何环（包括经过网关的环）；驳回重审使用“退回上一步”动作，不依赖图环。
10. 至少一个 `Approval` 节点（保持第一阶段发布约束）。

### 8.5 既有定义的画布还原

第一阶段与上线的线性定义（只有 `nodes` 无 `edges`）在读取时**合成** `Start → n1 → … → nN → End` 与对应连线，并在保存时落为真实连线。

画布新增节点的标识按类型命名（如 `approval1` / `cc1` / `exclusiveGateway1`），从 1 起自增并保证流程内唯一；`Start` / `End` 的合成标识保持 `__start` / `__end` 不变。这样历史草稿仍可打开与编辑，已发布定义不受影响（在途实例使用发布时快照，见第十一节）。

## 九、表单引擎与审批的绑定

表单引擎属于后续独立模块，本阶段只定义对接契约，保证「表单关联流程定义，提交表单触发审批」可实现且不需要改审批引擎。

绑定关系：

```
表单定义 FormDefinition(formCode, version, fields[])
        │  formCode ↔ 流程定义的 businessType
        ▼
流程定义 ApprovalFlowDefinition(businessType, code, version, status=Published)
        │  提交时按 businessType 解析默认流程
        ▼
审批实例 ApprovalInstance(context = 表单字段值快照)
```

对接约定：

1. **字段来源统一**：表单字段与审批字段使用同一 `ApprovalFieldType` 与 `key`，审批侧 `Fields` 直接取自表单定义的字段清单，条件字段无需二次声明。
2. **提交触发**：表单引擎在保存表单实例的同一事务/工作单元内调用 `IApprovalSubmitService.SubmitAsync`，`businessType = 表单绑定的流程业务类型`、`businessId = 表单实例 ID`、`context = 表单字段值`。审批与表单数据一致性由终态集成事件（第十节 10.4）保证。
3. **业务类型解析**：一个业务类型只能有一个已发布版本在生效；同业务类型存在多个已发布版本时，取最新发布版本并在定义列表上给出提示（沿用第一阶段「已发布定义不可变」的口径）。
4. **条件字段自动可用**：因字段来自表单定义，描述符不再需要手写 `ConditionFields`；画布上的条件编辑器直接给出该表单的字段下拉。

## 十、流程引擎使用说明

### 10.1 业务模块接入（开发者）

只引用 `Sim.Approval.Application.Contracts`，不引用 Domain/Application/SqlSugar 工程。

**第一步：登记业务类型**

```csharp
public sealed class ExpenseApprovalDescriptor : IApprovalBusinessDescriptor
{
    public string BusinessType => "qms.expense";        // 稳定标识，勿改
    public string DisplayName => "费用报销";
    public string? DefinitionCode => "qms.expense";     // 默认流程编码
    public IReadOnlyList<ApprovalFieldDto> Fields => [
        new() { Key = "amount",  DisplayName = "报销金额", Type = ApprovalFieldType.Decimal, Required = true },
        new() { Key = "dept",    DisplayName = "所属部门", Type = ApprovalFieldType.String },
        new() { Key = "invoice", DisplayName = "发票",     Type = ApprovalFieldType.Attachment },
    ];
}

// 模块注册
context.Services.AddTransient<IApprovalBusinessDescriptor, ExpenseApprovalDescriptor>();
```

**第二步：消费终态事件（可选但推荐）**

```csharp
public sealed class ExpenseApprovalCompletedHandler : IMessageHandler<ApprovalInstanceCompletedEvent>
{
    public async Task HandleAsync(
        ApprovalInstanceCompletedEvent message,
        MessageContext context,
        CancellationToken ct = default)
    {
        switch (message.Status)
        {
            case "Approved":
                await _expenseService.MarkApprovedAsync(message.BusinessId, ct);
                break;
            case "Rejected":
                await _expenseService.MarkRejectedAsync(message.BusinessId, message.Comment, ct);
                break;
            case "Withdrawn":
                await _expenseService.MarkWithdrawnAsync(message.BusinessId, ct);
                break;
            case "Terminated":
                await _expenseService.MarkTerminatedAsync(message.BusinessId, message.Comment, ct);
                break;
        }
    }
}

// 模块注册（消息类型使用稳定常量）
context.Services.AddMessageHandler<ApprovalInstanceCompletedEvent, ExpenseApprovalCompletedHandler>(
    ApprovalConsts.IntegrationEventInstanceCompleted);
```

事件在实例进入终态的**同一工作单元**内写入 Outbox；提交后由消息基础设施异步投递并重试，消费失败不回滚审批。Inbox 按 `MessageId + Consumer` 去重，消费者仍需保证业务处理幂等。

**第三步：发起与查询**

```csharp
// 发起：业务单据保存成功后调用；同一业务对象同时只允许一个在途实例
var result = await _approvalSubmitService.SubmitAsync(new ApprovalSubmitInput
{
    BusinessType = "qms.expense",
    BusinessId   = expense.Id.ToString(),
    Title        = $"费用报销 - {expense.Applicant}",
    Fields       = expense.ToApprovalFields(),   // 与描述符 Fields 的 key 对齐
});

// 展示"审批中/已通过"
var snapshot = await _approvalStatusQuery.FindAsync("qms.expense", expense.Id.ToString());
```

**不要自行发送审批通知**：待办、结果、抄送、催办、超时全部由 approval 模块经通知平台 Outbox 统一投递。

### 10.2 流程配置（管理员）

1. 进入「审批管理 → 流程设计」，新建：填编码、名称、业务类型。
2. 画布上从 `Start` 开始拖出节点并连线；审批节点配置审批人策略（指定人/角色/组织主管）、多人方式（会签/或签/顺序）、超时与操作开关；抄送节点配置抄送对象。
3. 需要分支时插入**排他网关**，在出边上写条件（字段来自业务类型的 `Fields` 下拉），并留一条无条件的默认出边。
4. 需要并行时插入**并行网关**：多条出边即 fork；多条入边汇合处再放一个并行网关即 join。
5. 点「校验」查看问题清单，全部通过后保存草稿 → 发布。发布后不可修改；需调整时用「另存新版本」。
6. 已发布版本在途实例不受新版本影响。

### 10.3 表单触发审批

1. 表单定义绑定流程业务类型（`formCode ↔ businessType`）。
2. 用户提交表单 → 表单引擎在同一工作单元内调用 `IApprovalSubmitService.SubmitAsync`。
3. 无需任何「发起审批」界面：发起入口即在表单的提交动作里。

### 10.4 查询与事件

- 用户视图：待办（`my-pending`）、已办（`my-handled`）、抄送（`my-cc`）、我发起的（`my-submitted`）、角标（`counts`）。
- 详情：`instance/{id}` 给出字段快照、附件、节点状态、时间线与可用操作（`canWithdraw` / `canResubmit` 等）。
- 业务侧联动通过 `ApprovalInstanceCompletedEvent`（Outbox，最终一致）；只读展示用 `IApprovalStatusQuery`（读库）。

## 十一、迁移与兼容

| 对象 | 处理 |
| --- | --- |
| 既有线性定义（`nodes` 无 `edges`） | 读取时合成 `Start → … → End` 与连线；再次保存时落为真实连线 |
| 既有 `ContextJson`（扁平字典） | 读取时映射为带标签字段（`DisplayName = Key`、`Type = String`） |
| 在途实例 | 使用发布时快照推进，不受定义改版影响；新增 `ApprovalInstanceNode` 由既有任务/记录**一次性回填**（推导当前状态），保证进行中实例可继续推进 |
| 前端旧路由 | `待办/已办/抄送/我发起的` 四个路由保留为 Tab 承载页；定义编辑路由改为打开画布设计器 |
| 种子菜单 | 改为「审批中心（单入口+角标）+ 审批管理分组」；种子是幂等的，**已存在菜单不会被自动删除**，需提供显式清理步骤（见第十三节） |

## 十二、测试策略

新增必须覆盖的矩阵：

| 主题 | 用例 |
| --- | --- |
| 图推进 | 线性等价性（与第一阶段任务/记录序列完全一致）；排他网关多分支互斥；默认边兜底 |
| 并行 | fork 后两分支并行激活；join 全到达才推进；分支数不等长时 join 不提前触发 |
| 死分支 | 排他网关未选中的分支向下传播 `Skipped`，join 不永久等待 |
| 校验 | 无 `Start`/多 `Start`/孤立节点/无 `End`/并行出边带条件/多出边缺少默认边/任何环 → 均拒绝 |
| 并发 | 同一任务并发同意只有一个成功；会签部分同意、转交与加签也推进实例版本；并发提交同一业务对象只创建一个实例（唯一索引） |
| 撤回/催办 | `Returned` 可撤回；时间窗外拒绝；1 小时内二次催办被拒 |
| 迁移 | 扁平 `ContextJson` 能读出；缺失 `ApprovalInstanceNode` 行的旧实例可从待办任务恢复为可操作状态；线性定义合成连线后行为不变 |
| 前端 | 画布校验规则前端与后端一致；角标计数与实际待办数一致；详情页无裸 key→value |

前端沿用现有 Vitest；画布与工作台的交互逻辑（校验、图↔契约映射）做成可独立测试的纯函数，避免重挂载整页。

## 十三、实施阶段与完成定义

### 13.1 阶段划分

| 阶段 | 内容 | 交付判定 |
| --- | --- | --- |
| P0 阻塞项 | 并发原子性（条件更新 + 唯一索引 + 启用 `ConcurrentModified`）；撤回/催办/主管链/扫描周期与文档对齐 | 并发用例与规则用例通过；文档与实现无漂移 |
| P1 图引擎 | 连线模型 + 节点状态持久化 + token 激活/join/死分支；线性等价性测试 | 等价性测试通过；旧实例可继续推进 |
| P2 字段与附件 | `ApprovalFieldDto`/`Fields`、字段值快照、附件实体与端点；条件字段校验改由 schema 驱动 | 详情页可只读渲染业务单据；附件可上传/展示 |
| P3 画布设计器 | Vue Flow 画布 + 属性抽屉 + 图↔契约映射 + 校验接口 | 可画出含排他/并行网关的流程并发布；历史定义可打开 |
| P4 前端重构 | 审批中心单入口 + Tab + 角标；详情页工作台；移除发起入口 | 无「发起审批」UI；六个表格菜单收敛；角标生效 |
| P5 表单对接 | 与表单引擎联调 `formCode ↔ businessType`；提交表单触发审批 | 表单提交可触发审批并回写业务状态 |

**P0 与 P1 必须先于 P3/P4**：否则画布上画出的分支在运行时是错的。

### 13.1.1 实施状态

| 阶段 | 状态 | 落地内容与证据 |
| --- | --- | --- |
| P0 阻塞项 | **已完成（2026-09-24）** | 见下 |
| P1 图引擎 | **已完成（2026-09-24）** | 见下 |
| P2 字段与附件 | **已完成（2026-09-24）** | 见下 |
| P3 画布设计器 | **已完成（2026-10-01）** | Vue Flow 工作台、图↔契约映射、节点/连线属性、即时与服务端校验、布局持久化 |
| P4 前端重构 | 未开始 | — |
| P5 表单对接 | 未开始（依赖表单引擎模块） | — |

P0 落地明细：

- 仓储新增条件更新：`IApprovalInstanceRepository.TryAdvanceAsync`（`WHERE Id = @id AND Version = @expected`）、`IApprovalTaskRepository.TryFinalizeAsync`（`WHERE Id = @id AND Status = 'Pending'`），受影响行数为 0 即抛 `ConcurrentModified`；`ApprovalErrorCodes.ConcurrentModified` 由「已定义零引用」转为真正启用。
- `(TenantScope, RunningKey)` 唯一索引兜底并发发起；插入冲突识别 PostgreSQL 索引名与 SQLite 唯一约束报错两种文案，统一返回 `InstanceAlreadyRunning`。
- 撤回：`Running` 与 `Returned` 均可撤回，新增定义级 `WithdrawWindowHours`（空 = 不限），超窗返回新增错误码 `WithdrawWindowExpired`；查询侧 `CanWithdraw` 与守卫 `EnsureCanWithdraw` 共用同一判定。
- 催办频控：同实例同操作人 `ApprovalConsts.UrgeIntervalHours`（1 小时）内一次，超限返回新增错误码 `UrgeTooFrequent`。
- 组织主管链：节点新增 `ManagerLevelOffset`（0 = 最近负责人），越界时回落流程兜底审批人（2026-10-12 起被 `ApproverOrganizationId` 取代：组织负责人 = 所选组织的负责人，组织人员 = 所选组织的直接成员）。
- 超时扫描周期统一为 `ApprovalConsts.TimeoutScanTaskCron`（每 5 分钟）。
- 测试：`ApprovalConcurrencyTests`（条件更新与唯一索引，含终态 `RunningKey` 为空可共存）、`ApprovalApproverResolverTests`（层级取位/越界回落）、`ApprovalStateMachineTests`（撤回窗口与 `CanWithdraw` 一致性）、`ApprovalEngineFlowTests`（并发定案、并发发起、催办频控、超窗撤回、退回后撤回）。
- 契约：`WithdrawWindowHours`、`ManagerLevelOffset` 已进入 `ApprovalFlowDefinitionDto` / `ApprovalFlowNodeDto` / `SaveApprovalFlowNodeDto` 与 `approval.json`（`ManagerLevelOffset` 后由 `ApproverOrganizationId` 取代）。
- 编辑器可达性：现有卡片式定义编辑器已开放这两个字段——定义表单新增「撤回时间窗（小时）」（关闭「允许发起人撤回」时置空），节点卡片在「审批人来源 = 组织负责人」时显示「负责人上移层级」（0–20），草稿模型与入参映射同步补齐；`web` 类型检查、Lint、单元测试（approval + i18n 共 35 项）与 `pnpm check:bundle-budget`（入口 1.49 MiB / 上限 1.50 MiB）均通过。

P1 落地明细：

- 领域模型：新增 `ApprovalFlowEdge`（`FromNodeKey` / `ToNodeKey` / `ConditionJson` / `Label` / `SortOrder`，同一起点按 `SortOrder` 求值）；`ApprovalFlowNode` 新增 `JoinPolicy`；`ApprovalFlowDefinition` 新增 `LayoutJson`（画布坐标，引擎不读取）；新增 `ApprovalInstanceNode`（`InstanceId` / `NodeKey` / `NodeName` / `NodeType` / `SortOrder` / `State` / `Round` / `ActivatedTime` / `CompletedTime`，就绪数量由入边源状态推导，不设计数器字段），把每个实例的节点状态持久化，替代「靠 `CurrentNodeSort` 推导进度」的第一阶段做法；`ApprovalRecord` 新增 `FromNodeKey` / `ToNodeKey` 支撑时间线 from→to。
- 图视图 `ApprovalFlowGraph`：`Build` / `BuildLinear` / `IsReady` / `ResolveDeadBranches` / `ResolveSelectedTargets` / `ResolvePrunedTargets` / `Snapshot`。**线性等价性**：只有节点、没有连线的定义在读取时合成 `__start → n1 → … → nN → __end`（`HasSynthesizedBoundaries`，合成的两个边界节点会落库为实例节点行），因此第一阶段行为是第二阶段的一个特例，见 §4.5。
- 引擎推进 `ApprovalEngine.AdvanceAsync`：循环「死分支传播 → 就绪节点激活」直到收敛；审批节点条件不成立时按免审置 `Completed` 并沿出边继续推进，只有网关未选分支置 `Skipped`；全部节点已定案且当前轮次确实生成过审批任务时实例才可 `Approved`。`ActivateNodeAsync` 按类型分派（`Start` / 网关 / `End` 直接完成，`Cc` 写抄送记录后完成，`Approval` 生成任务）；`CompleteNodeAsync` 在置 `Completed` 后剪枝未选中的独占分支，并保留从已选路径仍可达的共享下游。
- 可操作性判定：任务能否操作改由「该任务所属的实例节点 `State == Active` + 属于当前轮次」决定，`CurrentNodeKey` 仅作旧实例回落——并行分支下 `CurrentNodeKey` 不等于当前任务节点也能正常处理。查询侧详情/列表输出节点 `state` / `round` 与记录的 `fromNodeKey` / `toNodeKey`。
- 边界安全：`ApprovalFlowGraph.StartNode` / `EndNode` 可空，缺少开始/结束节点时构建不抛异常，改由校验器汇报可读问题（可达性校验自动跳过）。
- 发布前校验 `ApprovalFlowGraphValidator.Validate` / `EnsureValid`：实现 §8.4 的规则 1/2/3/7/8/9（唯一开始、至少一个结束、从开始可达、非结束节点有出线、开始节点恰一条出线、并行网关出线无条件、其余多出边节点恰好一条默认边、不允许任何环、至少一个审批节点），`PublishAsync` 在既有节点级校验之后调用；规则 4/5（审批人、抄送对象）仍在 `FlowDefinitionAppService.ValidateNode`，规则 6（条件字段必须来自业务类型声明）与运算符/字段类型兼容在 `ValidateDeclaredConditionFields` 与 `ApprovalFieldSchema.EnsureConditionOperatorsMatchFieldTypes`。2026-11-23 起 `PublishAsync` / `ValidateAsync` 还会校验业务类型已注册、存量条件运算符与字段类型匹配，并在发布新版时自动停用同编码旧版。
- 保存与派生：`CreateAsync` / `UpdateAsync` 由 `nodes` 扩展为 `nodes` + `edges` + `layoutJson` 整体覆盖（连线校验节点存在、禁止自环与重复 `(from,to)`、按起点自动编排 `SortOrder`、校验条件 JSON 与声明字段）；`CreateVersionAsync` / `DeleteAsync` 同步拷贝与清理连线；`edges` 为空仍按线性语义保存，历史草稿可原样打开。
- 测试：`ApprovalFlowGraphTests`（23 项纯单元：线性合成边界、缺边界不抛异常、就绪与合流等待、死分支传播、分支选择与剪枝、发布前校验规则逐条）、`ApprovalEngineFlowTests` 新增 4 项端到端（排他分支只激活命中分支且不生成被剪枝任务、并行 fork 下两条分支可任意顺序处理且合流等待全部到达、被剪枝分支不阻塞合流、线性等价性校验 `__start` / `__end` 实例节点与记录 from→to）。P1 交付时含 P0 用例共 **85 项** `FullyQualifiedName~Approval` 测试通过。
- 契约：`ApprovalFlowEdgeDto` / `SaveApprovalFlowEdgeDto`、节点 `joinPolicy`、定义 `edges` / `layoutJson`、节点进度 `state` / `round`、记录 `fromNodeKey` / `toNodeKey` 已进入 `web/src/api/definition/approval.json` 与 `web/src/api/approval` 生成 SDK；`web` 草稿模型补齐 `joinPolicy`（默认 `AnyArrival`，编辑既有图定义时不丢字段），`pnpm type-check`、approval 模块 ESLint 与前端单元测试（27 项）通过。
- P3 已于 2026-10-01 落地：流程定义编辑器可直接维护 `nodes`、`edges` 与 `layoutJson`，支持节点拖放、连线/重连、节点与分支属性、问题定位、适应画布和不改变图语义的纵向整理；界面采用固定操作栏 + 左侧节点库 + 中央画布 + 右侧属性面板 + 底部校验区，避免基础表单与画布纵向堆叠。`approval` 分组已加入 `api/Jenkinsfile` 的 OpenAPI 契约重生成列表，与其余六组走同一 `swagger tofile` + `git diff --exit-code` 流程（CI 工作区布局与流水线路径是否一致属于既有问题，不在本次范围）。

P2 落地明细：

- 字段契约：`ApprovalFieldDto`（`Key` / `DisplayName` / `Type` / `Options` / `Required` / `Placeholder`）+ `ApprovalFieldOptionDto` + `ApprovalFieldValueDto` + `ApprovalContextFieldDto` + `ApprovalContextSnapshotDto`；`IApprovalBusinessDescriptor.ConditionFields` 由 `Fields` 取代，`GenericApprovalBusinessDescriptor.Fields` 为空集，`BusinessTypeAppService` 透出 `fields`。
- 字段解析收敛在 `ApprovalFieldSchema`（application 层 internal static）：`ParseFieldType`（无法识别回落 `String`）、`MergeValues`（`Context` 与 `Fields` 合并，同键以 `Fields` 为准）、`EnsureRequiredFields`（缺失/空值抛 `InvalidRequest`）、`BuildSnapshot`（声明顺序 + 未声明键按序追加，只保留非空值，并固化 `DisplayName` / `Type` / `Options`）。发起与重新提交共用这一条链（`ApprovalEngine.StartAsync` / `ResubmitAsync`）；读取详情直接使用快照元数据，不再读取当前业务描述符。
- 快照兼容：`ApprovalRuntimeJson` 读写 `ContextJson`，新形态 `{ "fields": [...] }`，旧扁平字典映射为 `Fields`（`DisplayName = Key`、`Type = String`）；属性名大小写不敏感查找，避免历史数据落到回退分支。
- 附件：`ApprovalAttachment` 实体 + `IApprovalAttachmentRepository`；`ApprovalAttachmentService` 承担上传（实例级/任务级）、列表、鉴权下载与可见性判定（发起人 / 任一任务审批人 / 抄送人）；`FileId` = `IFileManagementService` 返回的存储键，文件名按 `ApprovalConsts.MaxAttachmentFileNameLength` 截断。新增错误码 `Sim:Approval:AttachmentNotFound`。
- 端点与契约：`POST /api/approval/instance/{id}/upload-attachment`、`POST /api/approval/instance/upload-task-attachment/{taskId}`、`GET /api/approval/instance/attachment-download/{attachmentId}` 已进入 `web/src/api/definition/approval.json` 与生成 SDK；详情新增 `context.fields`、`attachments`、`canUploadAttachment`。**偏离**：下载改为鉴权代理而非签名直链（§3.4 记录原因）。
- 条件字段校验：`ValidateDeclaredConditionFields` 改为 schema 驱动——声明为空不限制，否则只接受 `IsConditionComparable()`（非 `Attachment`）字段；前端条件编辑器同样过滤附件字段（`isFieldConditionComparable`）。
- 测试：P2 交付时 `ApprovalEngineFlowTests` 新增 6 项（字段快照带声明标签与类型、必填字段拒绝提交、附件字段不可做条件、旧扁平 `context` 仍可读、实例与任务附件上传→列表→下载字节往返、上传人与实例状态守卫），含 P0/P1 用例共 **91 项**；2026-09-30 再增加单出边条件、共享汇合节点、条件中间节点免审、旧实例节点状态恢复、前加签转交链、无效目标用户与会签部分版本推进 7 项回归，当前 **117 项**（2026-11-23）`FullyQualifiedName~Approval` 测试通过；其中本次新增或签拒绝、终止授权、字段元数据快照、发布期业务校验、环与默认边规则、旧版自动停用、Outbox 终态事件共 13 项回归。测试侧以 `TestFieldedApprovalBusinessDescriptor` 与 1×1 PNG 真实走 `IFileManagementService` 本地存储，`FileManagement:Local:RootPath` 在 fixture 内重定向到临时目录。
- 前端：详情页按声明标签只读渲染业务单据（枚举值映射为选项标签），新增附件区（发起人上传、按可见性下载，复用 `saveBlob`）；审批动作弹窗支持审批人上传任务附件；重新提交改为下发 `fields`（保证必填校验可通过），`parseContextInput` / `formatContextInput` 改为字段值 ↔ `键=值` 文本；`contextEntries` → `contextFields`。`pnpm type-check`、approval 模块 ESLint、全量单元测试（214 项，其中 approval + i18n 36 项覆盖本次改动）与 `pnpm check:bundle-budget`（入口 1.49 MiB / 上限 1.50 MiB）通过；浏览器内的上传/下载往返未做人工验证（本地开发宿主在运行，无登录凭据），仅以 Vite 开发服务器对改动模块的按需编译 200 响应作为烟测。
- 待办（不阻塞 P3）：`DatabaseFileManagementOptionsProvider` 不回填 `PublicBaseUrl` / `AccessUrlSigningKey` 导致签名直链不可用，属框架侧缺陷，已按上述偏离绕开，另行记录待修。

### 13.2 完成定义

- 画布可设计含排他网关、并行 fork/join 的流程，并在发布前被校验拦住所有非法结构。
- 线性流程行为与第一阶段完全一致（等价性测试为证）。
- 并发定案、并发提交有确定结果，不再依赖先查后插。
- 实例详情可只读渲染业务单据字段与附件，时间线含 from→to。
- 前端不存在统一「发起审批」入口；发起只由业务逻辑或表单提交触发。
- 角标计数与实际待办一致。
- 全部写接口具备原子并发、租户隔离、稳定错误码与中英文资源。
- 文档与实现无漂移（本文与第一阶段文档、`docs/core/ARCHITECTURE.md`、`docs/core/ENTERPRISE_SYSTEM_GUIDE.md` 同步）。

## 十四、对第一阶段第二十一节的修订

第一阶段第二十一节「明确不做」中，以下两条**在本阶段转为做**，其余保持不变：

- ~~BPMN 通用流程引擎语义与画布式流程设计器~~ → 本阶段实现**有向图语义 + 画布式设计器**（不做全量 BPMN 元素）。
- ~~并行 fork/join 分支与分支合并~~ → 本阶段实现**排他网关 + 并行网关（fork/join）**。

保持不变：超时仅提醒（不自动通过/拒绝）、在途实例不迁移定义版本、无电子签名/印章、不开放第三方发起、无流程模板市场。
