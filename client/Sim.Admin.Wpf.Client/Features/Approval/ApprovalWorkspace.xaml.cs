using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Approval;

public partial class ApprovalWorkspace : UserControl
{
    private static readonly string[] Modules =
    [
        "todo", "handled", "submitted", "cc", "definitions", "monitor"
    ];

    private readonly ApiClient _api;
    private readonly Action _sessionExpired;
    private readonly Dictionary<string, ApprovalListPage> _pages = [];

    public ApprovalWorkspace(ApiClient api, Action sessionExpired)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
    }

    public void ShowModule(string module)
    {
        if (!Modules.Contains(module)) throw new InvalidOperationException($"未知审批模块：{module}");
        if (!_pages.TryGetValue(module, out var page))
        {
            var kind = module switch
            {
                "todo" => ApprovalListKind.Pending,
                "handled" => ApprovalListKind.Handled,
                "submitted" => ApprovalListKind.Submitted,
                "cc" => ApprovalListKind.Cc,
                "definitions" => ApprovalListKind.Definitions,
                "monitor" => ApprovalListKind.Monitor,
                _ => throw new InvalidOperationException($"未知审批模块：{module}")
            };
            page = new ApprovalListPage(_api, kind, _sessionExpired);
            _pages.Add(module, page);
        }
        ModuleHost.Content = page;
    }
}
