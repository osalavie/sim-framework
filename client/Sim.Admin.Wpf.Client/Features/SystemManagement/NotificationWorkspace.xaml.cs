using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class NotificationWorkspace : UserControl
{
    private static readonly (string Label, int Index)[] Modules =
    [
        ("通知渠道", 0),
        ("通知模板", 1),
        ("通知规则", 2),
        ("Webhook", 3),
        ("通知投递", 4)
    ];

    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private readonly Dictionary<int, Management.ManagementPage> _pages = [];

    public NotificationWorkspace(ApiClient api, Action? sessionExpired = null)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
        ModuleList.ItemsSource = Modules.Select(module => module.Label).ToList();
        ModuleList.SelectedIndex = 0;
    }

    private void ModuleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModuleList.SelectedIndex < 0 || ModuleList.SelectedIndex >= Modules.Length) return;
        var index = Modules[ModuleList.SelectedIndex].Index;
        if (!_pages.TryGetValue(index, out var page))
        {
            page = new Management.ManagementPage(_api,
                Management.ManagementCatalog.Notifications[index], _sessionExpired);
            _pages.Add(index, page);
        }
        PageHost.Content = page;
    }
}
