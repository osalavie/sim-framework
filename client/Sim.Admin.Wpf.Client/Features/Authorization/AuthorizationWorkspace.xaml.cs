using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Features.Management;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Authorization;

public partial class AuthorizationWorkspace : UserControl
{
    private static readonly string[] Modules =
    [
        "user", "role", "module", "route", "permission"
    ];

    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private readonly Dictionary<string, UserControl> _pages = [];

    public AuthorizationWorkspace(ApiClient api, Action? sessionExpired = null)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
    }

    public void ShowModule(string module)
    {
        if (!Modules.Contains(module)) throw new InvalidOperationException($"未知授权模块：{module}");
        if (!_pages.TryGetValue(module, out var page))
        {
            page = module switch
            {
                "user" => new ManagementPage(_api, ManagementCatalog.Authorization[0], _sessionExpired),
                "role" => new ManagementPage(_api, ManagementCatalog.Authorization[1], _sessionExpired),
                "module" => new ManagementPage(_api, ManagementCatalog.Authorization[3], _sessionExpired),
                "route" => new ManagementPage(_api, ManagementCatalog.Authorization[4], _sessionExpired),
                "permission" => new ManagementPage(_api, ManagementCatalog.Authorization[2], _sessionExpired),
                _ => throw new InvalidOperationException($"未知授权模块：{module}")
            };
            _pages.Add(module, page);
        }
        ModuleHost.Content = page;
    }
}
