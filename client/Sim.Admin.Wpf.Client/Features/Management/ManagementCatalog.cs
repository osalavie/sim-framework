namespace Sim.Admin.Wpf.Client.Features.Management;

public enum FieldKind { Text, LongText, Number, Boolean, Choice, Json, List }

public sealed record FieldOption(int Value, string Label);

public sealed record ManagementField(
    string Key, string Label, FieldKind Kind = FieldKind.Text,
    bool Required = false, bool CreateOnly = false, bool ShowInTable = false,
    IReadOnlyList<FieldOption>? Options = null, string? DefaultValue = null);

public enum ManagementHttpMethod { Post, Put, Delete }

public enum ManagementActionKind
{
    ResetPassword,
    AssignRoles,
    DictionaryItems,
    TestProvider,
    AssignRoutes
}

public sealed record ManagementAction(string Label, ManagementActionKind Kind, bool Danger = false);

public sealed record ManagementDefinition(
    string Title, string Path, bool Paged, IReadOnlyList<ManagementField> Fields,
    bool CanCreate = true, bool CanUpdate = true, bool CanDelete = true,
    bool UpdateIdInBody = false, IReadOnlyList<ManagementRelation>? Relations = null,
    string? ItemsProperty = null, string? ItemBasePath = null,
    IReadOnlyDictionary<string, object?>? FixedValues = null,
    IReadOnlyList<ManagementAction>? Actions = null,
    string? CreatePath = null, string? UpdatePath = null, string? DeletePath = null,
    ManagementHttpMethod CreateMethod = ManagementHttpMethod.Post,
    ManagementHttpMethod UpdateMethod = ManagementHttpMethod.Put,
    ManagementHttpMethod DeleteMethod = ManagementHttpMethod.Delete,
    bool ActionsFixed = true);

public sealed record ManagementRelation(
    string Label, string OptionsPath, string AssignedPath, string SavePath,
    string OptionLabelKey, bool AssignedAsObjects = false, string? OptionSearchKey = null);

public static class ManagementCatalog
{
    private static ManagementField Text(string key, string label, bool required = false,
        bool table = false, bool createOnly = false, string? defaultValue = null) =>
        new(key, label, FieldKind.Text, required, createOnly, table, DefaultValue: defaultValue);

    private static ManagementField Number(string key, string label, bool required = false,
        bool table = false) => new(key, label, FieldKind.Number, required, false, table);

    private static ManagementField Boolean(string key, string label, bool table = false,
        bool defaultValue = true) =>
        new(key, label, FieldKind.Boolean, false, false, table,
            DefaultValue: defaultValue ? "true" : "false");

    private static ManagementField LongText(string key, string label, bool required = false) =>
        new(key, label, FieldKind.LongText, required);

    private static ManagementField Json(string key, string label) =>
        new(key, label, FieldKind.Json);

    private static ManagementField List(string key, string label, bool required = false) =>
        new(key, label, FieldKind.List, required);

    private static ManagementField Choice(string key, string label, bool table,
        params FieldOption[] options) =>
        new(key, label, FieldKind.Choice, true, false, table, options);

    public static readonly IReadOnlyList<ManagementDefinition> Authorization =
    [
        new("用户", "api/system/user", true,
        [Text("userName", "账号", true, true), Text("nickName", "昵称", true, true),
            Text("name", "姓名", table: true), Text("phone", "手机", table: true),
            Choice("sex", "性别", true, new(0, "未知"), new(1, "男"), new(2, "女")),
            Text("email", "邮箱", table: true), Text("address", "地址"),
            Text("description", "说明")], UpdateIdInBody: true,
            Actions:
            [
                new("重置密码", ManagementActionKind.ResetPassword, true),
                new("分配角色", ManagementActionKind.AssignRoles)
            ]),
        new("角色", "api/system/role", true,
        [Text("name", "名称", true, true), Text("code", "代码", true, true),
            Text("description", "说明", table: true)], UpdateIdInBody: true,
            Relations:
            [
                new("分配模块", "api/system/module-config?maxResultCount=500",
                    "api/system/role-module/role-modules/{id}",
                    "api/system/role-module/assign-modules-to-role/{id}", "name"),
                new("分配权限", "api/system/permission?maxResultCount=500",
                    "api/system/permission/permissions-by-role/{id}",
                    "api/system/role/assign-role-permissions/{id}", "name", true, "code")
            ]),
        new("权限", "api/system/permission", true,
        [Text("name", "名称", true, true), Text("code", "代码", true, true),
            Choice("permissionType", "类型", true,
                new(0, "未分类"), new(1, "页面"), new(2, "菜单"), new(3, "按钮")),
            Text("description", "说明")],
            UpdateIdInBody: true),
        new("模块", "api/system/module-config", true,
        [Text("name", "名称", true, true), Text("layout", "布局", true, true),
            Text("description", "说明"), Text("version", "版本"), Text("icon", "图标"),
            Number("order", "顺序", table: true), Boolean("enabled", "启用", true),
            Text("emptyRedirect", "默认跳转")],
            Actions: [new("分配路由", ManagementActionKind.AssignRoutes)]),
        new("路由", "api/system/route-config", true,
        [Text("name", "名称", true, true), Text("path", "路径", true, true),
            Text("component", "组件", true, true), Text("parentId", "父路由 ID"),
            Text("redirect", "重定向"), Json("meta", "元数据 JSON")], UpdateIdInBody: true),
    ];

    public static readonly IReadOnlyList<ManagementDefinition> System =
    [
        new("系统配置", "api/system/configuration-item", false,
        [Text("groupKey", "分组代码", true, true, true),
            Text("groupName", "分组名称", true, true),
            Text("configKey", "配置代码", true, true, true),
            Text("configName", "配置名称", true, true),
            Text("value", "值", true, true), Text("description", "说明"),
            Number("sort", "排序"), Boolean("enabled", "启用", true)]),
        new("数据字典", "api/system/data-dictionary", false,
        [Text("dictionaryKey", "字典代码", true, true, true),
            Text("displayName", "显示名称", true, true),
            Choice("valueType", "值类型", true,
                new(0, "文本"), new(1, "数字"), new(2, "日期")),
            Text("valuePrefix", "值前缀"), Text("valueSuffix", "值后缀"),
            Text("description", "说明"), Boolean("enabled", "启用", true)],
            Actions: [new("字典项", ManagementActionKind.DictionaryItems)]),
        new("业务编号", "api/system/business-number-definition", false,
        [Text("businessKey", "业务代码", true, true, true),
            Text("displayName", "显示名称", true, true),
            Text("format", "编号格式", true, true),
            Number("initialSequence", "初始序号"), Text("description", "说明"),
            Boolean("enabled", "启用", true)], CanDelete: false),
    ];

    public static readonly IReadOnlyList<ManagementDefinition> Notifications =
    [
        new("通知渠道", "api/system/notification-administration", false,
        [Text("channel", "渠道", true, true), Text("provider", "提供方", true, true),
            Text("displayName", "名称", true, true), Number("priority", "优先级", table: true),
            Boolean("enabled", "启用", true), Json("settings", "设置 JSON"),
            Json("secrets", "密钥 JSON"), Boolean("clearSecrets", "清除已保存密钥", defaultValue: false)],
            CanDelete: false, ItemsProperty: "providers",
            CreatePath: "api/system/notification-administration/save-provider",
            UpdatePath: "api/system/notification-administration/save-provider",
            CreateMethod: ManagementHttpMethod.Post, UpdateMethod: ManagementHttpMethod.Post,
            UpdateIdInBody: true,
            Actions: [new("测试渠道", ManagementActionKind.TestProvider)]),
        new("通知模板", "api/system/notification-administration", false,
        [Text("key", "键", true, true), Text("channel", "渠道", true, true),
            Text("locale", "语言", true, true, defaultValue: "zh-Hans"),
            Text("subject", "主题", table: true), LongText("body", "正文", true),
            Text("providerTemplateCode", "提供方模板代码"), Boolean("enabled", "启用", true)],
            CanDelete: false, ItemsProperty: "templates",
            CreatePath: "api/system/notification-administration/save-template",
            UpdatePath: "api/system/notification-administration/save-template",
            CreateMethod: ManagementHttpMethod.Post, UpdateMethod: ManagementHttpMethod.Post,
            UpdateIdInBody: true),
        new("通知规则", "api/system/notification-administration", false,
        [Text("eventName", "事件", true, true), Text("channel", "渠道", true, true),
            Text("provider", "提供方"), Text("templateKey", "模板", true, true),
            Boolean("enabled", "启用", true)],
            CanDelete: false, ItemsProperty: "rules",
            CreatePath: "api/system/notification-administration/save-rule",
            UpdatePath: "api/system/notification-administration/save-rule",
            CreateMethod: ManagementHttpMethod.Post, UpdateMethod: ManagementHttpMethod.Post,
            UpdateIdInBody: true),
        new("Webhook", "api/system/notification-administration", false,
        [Text("site", "站点", true, true), Text("name", "名称", true, true),
            Text("url", "地址", true, true), List("eventNames", "事件列表"),
            Text("signingSecret", "签名密钥"),
            Boolean("clearSigningSecret", "清除签名密钥", defaultValue: false),
            Boolean("enabled", "启用", true)],
            CanDelete: false, ItemsProperty: "webhooks",
            CreatePath: "api/system/notification-administration/save-webhook",
            UpdatePath: "api/system/notification-administration/save-webhook",
            CreateMethod: ManagementHttpMethod.Post, UpdateMethod: ManagementHttpMethod.Post,
            UpdateIdInBody: true),
        new("通知投递", "api/system/notification-administration/deliveries", false,
        [Text("eventName", "事件", table: true), Text("channel", "渠道", table: true),
            Text("provider", "提供方", table: true), Text("destinationMasked", "目标", table: true),
            Text("status", "状态", table: true), Number("attempts", "尝试次数", table: true),
            Text("errorCode", "错误码", table: true)],
            CanCreate: false, CanUpdate: false, CanDelete: false),
    ];

    public static ManagementDefinition DictionaryItems(Guid dictionaryId, string title) => new(
        $"{title} · 字典项",
        $"api/system/data-dictionary-item?dictionaryId={dictionaryId:D}",
        false,
        [Text("itemKey", "项目代码", true, true, createOnly: true),
            Text("itemName", "项目名称", true, true),
            Text("value", "值", true, true),
            Number("sort", "排序", table: true),
            Boolean("enabled", "启用", true),
            Text("description", "说明")],
        ItemBasePath: "api/system/data-dictionary-item",
        FixedValues: new Dictionary<string, object?> { ["dictionaryId"] = dictionaryId });
}
