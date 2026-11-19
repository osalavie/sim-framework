using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Sim.Admin.HttpApi.Host.Middleware;
using Xunit;

namespace Sim.Security.Tests;

public class MaintenanceMiddlewareTests
{
    [Fact]
    public async Task Enabled_maintenance_should_block_business_requests_without_calling_next()
    {
        var configuration = CreateConfiguration("true", "Back soon");
        var nextCalled = false;
        var middleware = new MaintenanceMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration);
        var context = CreateContext("/api/system/user/info");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.True(document.RootElement.GetProperty("maintenanceMode").GetBoolean());
        Assert.Equal("Back soon", document.RootElement.GetProperty("maintenanceMessage").GetString());
    }

    [Fact]
    public async Task Site_status_and_health_should_remain_available_while_maintenance_is_enabled()
    {
        var configuration = CreateConfiguration("true", null);
        foreach (var path in new[]
                 {
                     "/api/admin/site-status",
                     "/health/ready",
                     "/hubs/site-status",
                     "/hubs/site-status/negotiate",
                 })
        {
            var nextCalled = false;
            var middleware = new MaintenanceMiddleware(
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                },
                configuration);

            await middleware.InvokeAsync(CreateContext(path));

            Assert.True(nextCalled);
        }
    }

    [Fact]
    public async Task Other_hub_paths_are_still_blocked_during_maintenance()
    {
        var configuration = CreateConfiguration("true", null);
        var nextCalled = false;
        var middleware = new MaintenanceMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration);

        await middleware.InvokeAsync(CreateContext("/hubs/user-notifications"));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Changing_configuration_should_take_effect_for_a_new_request()
    {
        var provider = new MutableConfigurationProvider();
        provider.Set("Site:MaintenanceMode", "false");
        var configuration = new ConfigurationRoot(new[] { provider });
        var nextCalled = false;
        var middleware = new MaintenanceMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration);

        await middleware.InvokeAsync(CreateContext("/api/system/user/info"));
        Assert.True(nextCalled);

        provider.Set("Site:MaintenanceMode", "true");
        nextCalled = false;
        var context = CreateContext("/api/system/user/info");
        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(503, context.Response.StatusCode);
    }

    private static IConfiguration CreateConfiguration(string enabled, string? message)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:MaintenanceMode"] = enabled,
                ["Site:MaintenanceMessage"] = message,
            })
            .Build();
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class MutableConfigurationProvider : ConfigurationProvider
    {
    }
}


