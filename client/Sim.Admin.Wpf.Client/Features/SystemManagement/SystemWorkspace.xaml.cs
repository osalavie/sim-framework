using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Features.Management;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class SystemWorkspace : UserControl
{
    private static readonly string[] Modules =
    [
        "storage", "schedule", "notifications", "numbering", "config", "dictionary", "organization", "tenant"
    ];

    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private readonly Dictionary<string, UserControl> _pages = [];

    public SystemWorkspace(ApiClient api, Action? sessionExpired = null)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
    }

    public void ShowModule(string module)
    {
        if (!Modules.Contains(module)) throw new InvalidOperationException($"未知系统模块：{module}");
        if (!_pages.TryGetValue(module, out var page))
        {
            page = module switch
            {
                "config" => new ManagementPage(_api, ManagementCatalog.System[0], _sessionExpired),
                "dictionary" => new ManagementPage(_api, ManagementCatalog.System[1], _sessionExpired),
                "numbering" => new ManagementPage(_api, ManagementCatalog.System[2], _sessionExpired),
                "schedule" => new ScheduleTaskPage(_api, _sessionExpired),
                "storage" => new StoragePage(_api, _sessionExpired),
                "notifications" => new NotificationWorkspace(_api, _sessionExpired),
                "organization" => new OrganizationPage(_api, _sessionExpired),
                "tenant" => new TenantPage(_api, _sessionExpired),
                _ => throw new InvalidOperationException($"未知系统模块：{module}")
            };
            _pages.Add(module, page);
        }
        ModuleHost.Content = page;
    }
}
