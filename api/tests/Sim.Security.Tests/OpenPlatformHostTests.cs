using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Contracts.Dtos;
using Sim.OpenPlatform.Domain.Entities;
using Sim.SqlSugar.Core.Repositories;
using Xunit;

namespace Sim.Security.Tests;

[Collection(HostCollection.Name)]
public sealed class OpenPlatformHostTests(HostFixture fixture) : IClassFixture<HostFixture>
{
    [Fact]
    public async Task TokenEndpointIssuesAndAuditsClientCredentials()
    {
        var appId = "op_test_" + Guid.NewGuid().ToString("N")[..8];
        var secret = "ops_test_" + Guid.NewGuid().ToString("N");
        using (var scope = fixture.Services.CreateScope())
        {
            var protector = scope.ServiceProvider.GetRequiredService<IOpenPlatformSecretProtector>();
            var applications = scope.ServiceProvider
                .GetRequiredService<ISqlSugarRepository<OpenPlatformApplication>>();
            await applications.InsertAsync(new OpenPlatformApplication
            {
                Name = "Integration test application",
                AppId = appId,
                ProtectedAppSecret = protector.Protect(secret),
                IsActive = true
            });
        }

        using var client = fixture.CreateHttpsClient();
        using var rejected = await client.PostAsJsonAsync("/api/openplatform/token",
            new OpenPlatformTokenRequest { AppId = appId, AppSecret = "wrong-secret" });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var response = await client.PostAsJsonAsync("/api/openplatform/token",
            new OpenPlatformTokenRequest { AppId = appId, AppSecret = secret });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<OpenPlatformTokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(token?.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        using var forbidden = await client.GetAsync("/api/admin/setup/status");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var scopeAfter = fixture.Services.CreateScope();
        var logs = await scopeAfter.ServiceProvider
            .GetRequiredService<ISqlSugarRepository<OpenPlatformAccessLog>>()
            .GetListAsync(log => log.AppId == appId);
        Assert.Contains(logs, log => log.Succeeded);
        Assert.Contains(logs, log => !log.Succeeded && log.Reason == "SecretMismatch");

        var calls = await scopeAfter.ServiceProvider
            .GetRequiredService<ISqlSugarRepository<OpenPlatformApiCallLog>>()
            .GetListAsync(call => call.AppId == appId);
        Assert.Contains(calls, call => call.StatusCode == 403);
    }
}
