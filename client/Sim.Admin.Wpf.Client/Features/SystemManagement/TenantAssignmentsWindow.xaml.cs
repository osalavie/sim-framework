using System.Windows;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class TenantAssignmentsWindow : Window
{
    public TenantAssignmentsWindow(TenantAssignments assignments)
    {
        InitializeComponent();
        Title = $"{assignments.TenantName} · 租户分配";
        ModulesGrid.ItemsSource = assignments.Modules;
        RoutesGrid.ItemsSource = assignments.Routes;
        PermissionsGrid.ItemsSource = assignments.Permissions;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
