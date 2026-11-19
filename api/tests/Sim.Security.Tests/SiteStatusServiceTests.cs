using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Sim.Admin.Application.Contracts.SiteStatus;
using Sim.Admin.Application.SiteStatus;
using Sim.Admin.HttpApi.Host.SiteStatus;
using Xunit;

namespace Sim.Security.Tests;

public class SiteStatusServiceTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public async Task Maintenance_mode_should_read_configuration_for_each_call(string? value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:MaintenanceMode"] = value,
                ["Site:MaintenanceMessage"] = " Planned maintenance ",
            })
            .Build();
        var service = new SiteStatusService(configuration);

        var result = await service.GetAsync();

        Assert.Equal(expected, result.MaintenanceMode);
        Assert.Equal(" Planned maintenance ", result.MaintenanceMessage);
    }

    [Fact]
    public async Task Missing_maintenance_message_should_be_omitted()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:MaintenanceMode"] = "true",
            })
            .Build();
        var service = new SiteStatusService(configuration);

        var result = await service.GetAsync();

        Assert.True(result.MaintenanceMode);
        Assert.Null(result.MaintenanceMessage);
    }

    [Fact]
    public void Realtime_snapshot_should_match_the_status_contract()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:MaintenanceMode"] = "true",
                ["Site:MaintenanceMessage"] = "  ",
            })
            .Build();

        var snapshot = SiteStatusSnapshot.Read(configuration);

        Assert.True(snapshot.MaintenanceMode);
        Assert.Null(snapshot.MaintenanceMessage);
    }
}
