using System.Net;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using Sim.Framework.Domain.Shared.Telemetry;
using Sim.System.Domain.Shared;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Quartz;
using System.Globalization;
using Sim.Admin.Domain.Data;
using Sim.Admin.HttpApi.Host.Data;
using Sim.Admin.Domain.Shared.Localization;
using Sim.Framework.Core.UnitOfWork;
using Sim.System.Application.Contracts.E2ee;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Migrations;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.FileStorage;
using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Application.Notifications;
using Sim.System.Application.Jobs;
using Sim.System.Domain.Notifications;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.Filters;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.Framework.Domain.Shared.Numbering;
using Sim.SqlSugar.Core.Numbering;
using Sim.System.Application.Contracts.User;
using Sim.System.Application.Contracts.Dictionary;
using Sim.System.Application.Contracts.SystemConfiguration;
using Sim.System.Application.Contracts.Numbering;
using Sim.System.Notifications;
using SqlSugar;
using Volo.Abp.Localization.ExceptionHandling;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using HostProgram = Sim.Admin.HttpApi.Host.Program;
using Sim.System.Application.FileStorage;
using Sim.System.Application.ScheduleTask;
using Sim.System.Application.User;
using Sim.System.Domain.FileManagement;
using Sim.System.Domain.FileStorage;
using Sim.System.Domain.ScheduleTask;

namespace Sim.Security.Tests;

[Collection(HostCollection.Name)]
public sealed class HostIntegrationTests(HostFixture fixture)
    : IClassFixture<HostFixture>
{
    [Fact]
    public async Task Liveness_and_readiness_endpoints_should_report_healthy()
    {
        using var client = fixture.CreateHttpsClient();

        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal("Healthy", await ReadStatusAsync(liveResponse));
        Assert.Equal("Healthy", await ReadStatusAsync(readyResponse));
    }

    [Fact]
    public async Task SqlSugar_should_report_database_command_metrics()
    {
        var recordedCommands = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == Metrics.MeterName
                    && instrument.Name == "sim.db.command.count")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            string? command = null;
            string? status = null;
            foreach (var tag in tags)
            {
                if (tag.Key == Metrics.Tags.DbCommand && tag.Value is string value)
                {
                    command = value;
                }
                else if (tag.Key == Metrics.Tags.Status && tag.Value is string commandStatus)
                {
                    status = commandStatus;
                }
            }

            if (command is null)
            {
                return;
            }

            lock (recordedCommands)
            {
                recordedCommands.Add($"{command}/{status}");
            }
        });
        listener.Start();

        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        await database.Ado.GetScalarAsync("SELECT 1");

        // Aop.OnError 只用于上报失败指标，必须保留 SqlSugar 原有的异常传播行为。
        await Assert.ThrowsAnyAsync<Exception>(() =>
            database.Ado.GetScalarAsync("SELECT * FROM sim_missing_table_for_telemetry_test"));

        lock (recordedCommands)
        {
            Assert.Contains($"{Metrics.DbCommand.Select}/{Metrics.Status.Success}", recordedCommands);
            Assert.Contains($"{Metrics.DbCommand.Select}/{Metrics.Status.Failure}", recordedCommands);
        }
    }

    [Fact]
    public async Task Quartz_job_execution_should_report_schedule_task_metrics()
    {
        var recorded = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == Metrics.MeterName
                    && instrument.Name == "sim.schedule_task.count")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var values = tags.ToArray();
            var group = values.FirstOrDefault(t => t.Key == Metrics.Tags.TaskGroup).Value as string;
            var key = values.FirstOrDefault(t => t.Key == Metrics.Tags.TaskKey).Value as string;
            var triggerType = values.FirstOrDefault(t => t.Key == Metrics.Tags.TriggerType).Value as string;
            var status = values.FirstOrDefault(t => t.Key == Metrics.Tags.Status).Value as string;

            lock (recorded)
            {
                recorded.Add($"{group}/{key}/{triggerType}/{status}");
            }
        });
        listener.Start();

        var scheduler = await fixture.Services
            .GetRequiredService<ISchedulerFactory>()
            .GetScheduler();
        var jobKey = new JobKey("telemetry-probe-job", "Telemetry");
        var job = JobBuilder.Create<CleanupExpiredRefreshTokensJob>()
            .WithIdentity(jobKey)
            .Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity("MT_telemetry_probe", "Telemetry")
            .ForJob(job)
            .StartNow()
            .Build();

        // Manual 触发（MT_ 前缀）与执行历史解耦：该 Job 没有对应的任务配置，
        // 历史记录会被跳过，但指标仍应反映执行结果。
        string? matched = null;
        await scheduler.ScheduleJob(job, trigger);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (matched is null && DateTime.UtcNow < deadline)
            {
                lock (recorded)
                {
                    matched = recorded.FirstOrDefault(entry =>
                        entry.StartsWith("Telemetry/telemetry-probe-job/Manual/", StringComparison.Ordinal));
                }

                if (matched is null)
                {
                    await Task.Delay(100);
                }
            }
        }
        finally
        {
            await scheduler.DeleteJob(jobKey);
        }

        Assert.True(matched is not null, $"未记录到定时任务指标，实际记录：{string.Join(", ", recorded)}");
    }

    [Fact]
    public async Task Realtime_notification_hub_should_be_mapped_and_require_authentication()
    {
        using var client = fixture.CreateHttpsClient();

        using var response = await client.PostAsync(
            "/hubs/user-notifications/negotiate?negotiateVersion=1",
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("en", "Users", "Welcome to SIM")]
    [InlineData("zh-Hans", "用户管理", "欢迎使用SIM")]
    public async Task Client_localization_should_expose_backend_owned_menu_site_and_brand_data(
        string culture,
        string expectedUsersLabel,
        string expectedBrandHeadline)
    {
        using var client = fixture.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);

        using var response = await client.GetAsync("/api/admin/client-localization");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Accept-Language", response.Headers.Vary);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            expectedUsersLabel,
            document.RootElement
                .GetProperty("texts")
                .GetProperty("Menu:UserManagement")
                .GetString());
        Assert.Equal(
            expectedBrandHeadline,
            document.RootElement
                .GetProperty("texts")
                .GetProperty("Auth:BrandHeadline")
                .GetString());
        Assert.Equal(
            "SimFramework",
            document.RootElement.GetProperty("site").GetProperty("name").GetString());
        Assert.All(
            document.RootElement.GetProperty("texts").EnumerateObject(),
            text => Assert.NotEqual(text.Name, text.Value.GetString()));
    }

    [Fact]
    public async Task Refresh_token_endpoint_should_require_antiforgery_cookie_and_header()
    {
        using var client = fixture.CreateHttpsClient();

        using var missingTokenResponse = await client.PostAsync("/api/system/user/refresh-token", null);
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        using var tokenResponse = await client.GetAsync("/api/system/user/antiforgery-token");
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(token?.RequestToken));

        var antiforgeryCookie = tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("sim_antiforgery=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Add("Cookie", antiforgeryCookie.Split(';', 2)[0]);

        using var missingHeaderResponse = await client.PostAsync("/api/system/user/refresh-token", null);
        Assert.Equal(HttpStatusCode.BadRequest, missingHeaderResponse.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/system/user/refresh-token");
        request.Headers.Add("X-XSRF-TOKEN", token!.RequestToken);
        using var missingRefreshCookieResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, missingRefreshCookieResponse.StatusCode);
    }

    [Fact]
    public async Task Schema_migrator_should_create_application_tables_in_a_real_database()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>();
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        await migrator.MigrateAsync();

        var tables = database.DbMaintenance.GetTableInfoList(false);
        Assert.NotEmpty(tables);
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_user", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_stored_file", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_file_storage_settings", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_file_storage_provider", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_schedule_task_config", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_framework_message_outbox", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_framework_message_inbox", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_framework_number_sequence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_business_number_definition", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_configuration_item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_data_dictionary", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_data_dictionary_item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_notification_provider", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_notification_delivery", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_user_notification", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_resource_access_policy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_resource_file_reference", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, table =>
            string.Equals(table.Name, "sim_system_resource_share_link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Business_number_generator_should_scope_sequences_by_business_and_rendered_date()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        var generator = scope.ServiceProvider.GetRequiredService<IBusinessNumberGenerator>();
        var definitions = scope.ServiceProvider.GetRequiredService<IBusinessNumberDefinitionService>();
        var businessKey = $"test-order-{Guid.NewGuid():N}";
        var format = "{business}-{date:yyyy/MM/dd}-{sequence:0000}";
        var firstDay = new DateTimeOffset(2026, 7, 30, 8, 0, 0, TimeSpan.Zero);
        await definitions.CreateAsync(new CreateBusinessNumberDefinitionDto
        {
            BusinessKey = businessKey,
            DisplayName = "Test order",
            Format = format
        });
        var invoiceBusinessKey = $"test-invoice-{Guid.NewGuid():N}";
        await definitions.CreateAsync(new CreateBusinessNumberDefinitionDto
        {
            BusinessKey = invoiceBusinessKey,
            DisplayName = "Test invoice",
            Format = format
        });

        var first = await generator.GenerateAsync(new NumberGenerationRequest(businessKey, firstDay));
        var second = await generator.GenerateAsync(new NumberGenerationRequest(businessKey, firstDay));
        var nextDay = await generator.GenerateAsync(new NumberGenerationRequest(
            businessKey,
            firstDay.AddDays(1)));
        var otherBusiness = await generator.GenerateAsync(new NumberGenerationRequest(
            invoiceBusinessKey,
            firstDay));

        Assert.Equal($"{businessKey}-2026/07/30-0001", first);
        Assert.Equal($"{businessKey}-2026/07/30-0002", second);
        Assert.Equal($"{businessKey}-2026/07/31-0001", nextDay);
        Assert.EndsWith("-2026/07/30-0001", otherBusiness, StringComparison.Ordinal);
    }

    [Fact]
    public async Task System_configuration_should_seed_and_support_crud()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SystemConfigurationDataSeeder>().SeedAsync();
        var configuration = scope.ServiceProvider.GetRequiredService<ISystemConfigurationItemService>();

        var seeded = await configuration.GetListAsync("system.security");
        Assert.Contains(seeded, item =>
            item.ConfigKey == "security.password-min-length" &&
            item.Value == "12" &&
            item.Enabled);
        Assert.Contains(seeded, item =>
            item.ConfigKey == "security.registration-role-codes" &&
            item.Value == "User" &&
            item.Enabled);

        var created = await configuration.CreateAsync(new CreateSystemConfigurationItemDto
        {
            GroupKey = "system.configuration-test",
            GroupName = "Configuration test",
            ConfigKey = "sample.enabled",
            ConfigName = "Sample enabled",
            Value = "true",
            Description = "Integration test item",
            Sort = 5
        });
        Assert.Equal("system.configuration-test", created.GroupKey);
        Assert.Equal("sample.enabled", created.ConfigKey);

        var updated = await configuration.UpdateAsync(created.Id, new UpdateSystemConfigurationItemDto
        {
            GroupName = "Configuration test",
            ConfigName = "Sample updated",
            Value = "false",
            Description = null,
            Sort = 2,
            Enabled = false
        });
        Assert.Equal("false", updated.Value);
        Assert.False(updated.Enabled);

        var filtered = await configuration.GetListAsync("system.configuration-test");
        var item = Assert.Single(filtered);
        Assert.Equal("Sample updated", item.ConfigName);

        Assert.True(await configuration.DeleteAsync(created.Id));
        Assert.Empty(await configuration.GetListAsync("system.configuration-test"));
    }

    [Fact]
    public async Task Data_dictionary_should_validate_value_types_and_support_crud()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        var dictionaries = scope.ServiceProvider.GetRequiredService<IDataDictionaryService>();
        var items = scope.ServiceProvider.GetRequiredService<IDataDictionaryItemService>();

        var dictionary = await dictionaries.CreateAsync(new CreateDataDictionaryDto
        {
            DictionaryKey = "test.order-status",
            DisplayName = "Order status",
            ValueType = DataDictionaryValueType.Number,
            ValuePrefix = "$",
            ValueSuffix = " USD",
            Description = "Integration test dictionary"
        });

        var item = await items.CreateAsync(new CreateDataDictionaryItemDto
        {
            DictionaryId = dictionary.Id,
            ItemKey = "amount",
            ItemName = "Amount",
            Value = "12.30",
            Sort = 1
        });
        Assert.Equal("12.3", item.Value);

        var invalidNumber = await Assert.ThrowsAsync<BusinessException>(() =>
            items.UpdateAsync(item.Id, new UpdateDataDictionaryItemDto
            {
                ItemName = "Amount",
                Value = "not-a-number"
            }));
        Assert.Equal(SystemErrorCodes.InvalidDataDictionaryItem, invalidNumber.Code);

        var locked = await Assert.ThrowsAsync<BusinessException>(() =>
            dictionaries.UpdateAsync(dictionary.Id, new UpdateDataDictionaryDto
            {
                DisplayName = "Order status",
                ValueType = DataDictionaryValueType.Date
            }));
        Assert.Equal(SystemErrorCodes.DataDictionaryValueTypeLocked, locked.Code);

        var updatedDictionary = await dictionaries.UpdateAsync(dictionary.Id, new UpdateDataDictionaryDto
        {
            DisplayName = "Order status updated",
            ValueType = DataDictionaryValueType.Number,
            ValuePrefix = "$",
            ValueSuffix = " USD"
        });
        Assert.Equal("Order status updated", updatedDictionary.DisplayName);
        Assert.Equal(DataDictionaryValueType.Number, updatedDictionary.ValueType);
        Assert.Equal(1, updatedDictionary.ItemCount);

        var updated = await items.UpdateAsync(item.Id, new UpdateDataDictionaryItemDto
        {
            ItemName = "Amount updated",
            Value = "20",
            Description = null,
            Sort = 2,
            Enabled = false
        });
        Assert.Equal("20", updated.Value);
        Assert.False(updated.Enabled);

        Assert.True(await dictionaries.DeleteAsync(dictionary.Id));
        Assert.Empty(await items.GetListAsync(dictionary.Id));

        var emptyDictionary = await dictionaries.CreateAsync(new CreateDataDictionaryDto
        {
            DictionaryKey = "test.empty-dictionary",
            DisplayName = "Empty dictionary",
            ValueType = DataDictionaryValueType.String
        });
        var retyped = await dictionaries.UpdateAsync(emptyDictionary.Id, new UpdateDataDictionaryDto
        {
            DisplayName = "Empty dictionary",
            ValueType = DataDictionaryValueType.Date
        });
        Assert.Equal(DataDictionaryValueType.Date, retyped.ValueType);
        Assert.Equal(0, retyped.ItemCount);
        Assert.True(await dictionaries.DeleteAsync(emptyDictionary.Id));
    }

    [Fact]
    public async Task Resource_access_should_protect_files_and_accept_only_valid_owner_or_share()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var access = scope.ServiceProvider.GetRequiredService<IResourceAccessService>();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var file = new FileMetadataEntity(Guid.NewGuid())
        {
            CreatorId = ownerId,
            StorageKey = $"tests/{Guid.NewGuid():N}.png",
            Provider = FileStorageProviderNames.Local,
            FileName = "private.png",
            ContentType = "image/png",
            Length = 8,
            ScanStatus = QuarantineStatus.Clean
        };
        await database.Insertable(file).ExecuteCommandAsync();

        await access.ConfigureAsync(
            "tests.resource",
            resourceId,
            ownerId,
            ResourceVisibility.Private,
            [file.Id]);

        Assert.True(await access.CanAccessAsync(
            "tests.resource", resourceId, ownerId, null));
        Assert.False(await access.CanAccessAsync(
            "tests.resource", resourceId, strangerId, null));
        Assert.False(await access.CanAccessFileAsync(file.Id, strangerId, null));

        var share = await access.CreateShareLinkAsync(
            "tests.resource",
            resourceId,
            ownerId,
            TimeSpan.FromHours(1),
            maxViews: 2);
        var firstContext = new ResourceAccessContext(
            new string('a', 64),
            "203.0.113.10",
            "share-test-agent");
        var secondContext = new ResourceAccessContext(
            new string('b', 64),
            "203.0.113.11",
            "share-test-agent");
        Assert.True(await access.CanAccessAsync(
            "tests.resource", resourceId, strangerId, share.Token, firstContext));
        Assert.True(await access.CanAccessAsync(
            "tests.resource", resourceId, strangerId, share.Token, firstContext));
        Assert.True(await access.CanAccessAsync(
            "tests.resource", resourceId, strangerId, share.Token, secondContext));
        var thirdContext = new ResourceAccessContext(
            new string('c', 64),
            "203.0.113.12",
            "share-test-agent");
        Assert.False(await access.CanAccessAsync(
            "tests.resource", resourceId, strangerId, share.Token, thirdContext));
        Assert.True(await access.CanAccessFileAsync(
            file.Id, strangerId, share.Token, firstContext));

        await access.ConfigureAsync(
            "tests.resource",
            resourceId,
            ownerId,
            ResourceVisibility.Public,
            [file.Id]);
        Assert.True(await access.CanAccessFileAsync(file.Id, null, null));
    }

    [Fact]
    public async Task File_storage_connection_test_should_probe_local_and_database_providers()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IFileStorageConfigurationService>();
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "sim-host-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var local = await service.TestAsync(new TestFileStorageProviderConnectionDto
            {
                Provider = FileStorageProviderNames.Local,
                RootPath = rootPath
            });
            var database = await service.TestAsync(new TestFileStorageProviderConnectionDto
            {
                Provider = FileStorageProviderNames.Database
            });
            var unsupported = await service.TestAsync(new TestFileStorageProviderConnectionDto
            {
                Provider = "unknown"
            });

            Assert.True(local.Success);
            Assert.True(database.Success);
            Assert.False(unsupported.Success);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task System_management_should_persist_storage_defaults_and_schedule_definitions()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        await scope.ServiceProvider
            .GetRequiredService<FileStorageConfigurationInitializer>()
            .EnsureInitializedAsync();
        await scope.ServiceProvider
            .GetRequiredService<ScheduleTaskRuntime>()
            .EnsureInitializedAsync();

        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        Assert.Equal(1, await database.Queryable<FileStorageSettingsEntity>().CountAsync());
        Assert.Equal(5, await database.Queryable<FileStorageProviderConfigurationEntity>().CountAsync());
        Assert.Contains(
            await database.Queryable<ScheduleTaskConfigurationEntity>().ToListAsync(),
            task => task.Key == "CleanupExpiredRefreshTokensJob" && task.Enabled);
        Assert.Contains(
            await database.Queryable<ScheduleTaskConfigurationEntity>().ToListAsync(),
            task => task.Key == "SystemHeartbeatJob" &&
                    task.TaskType == ScheduleTaskTypes.Worker &&
                    task.IsBuiltIn);

        var options = await scope.ServiceProvider
            .GetRequiredService<IFileManagementOptionsProvider>()
            .GetAsync();
        Assert.Equal(FileStorageProviderNames.Local, options.DefaultStorageProvider);
    }

    [Fact]
    public async Task Notification_configuration_should_seed_official_provider_settings_without_secrets()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        await scope.ServiceProvider
            .GetRequiredService<NotificationConfigurationInitializer>()
            .EnsureInitializedAsync();

        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var providers = await database.Queryable<NotificationProviderConfigurationEntity>()
            .ToListAsync();
        var tencentSms = Assert.Single(
            providers,
            item => item.Provider == NotificationProviders.TencentSms);
        var aliyunVoice = Assert.Single(
            providers,
            item => item.Provider == NotificationProviders.AliyunVoice);

        using var tencentSettings = JsonDocument.Parse(tencentSms.SettingsJson);
        using var aliyunSettings = JsonDocument.Parse(aliyunVoice.SettingsJson);
        Assert.Equal(
            "https://sms.tencentcloudapi.com",
            tencentSettings.RootElement.GetProperty("Endpoint").GetString());
        Assert.True(tencentSettings.RootElement.TryGetProperty("SmsSdkAppId", out _));
        Assert.False(tencentSettings.RootElement.TryGetProperty("SecretId", out _));
        Assert.Equal(
            "https://dyvmsapi.aliyuncs.com",
            aliyunSettings.RootElement.GetProperty("Endpoint").GetString());
        Assert.True(aliyunSettings.RootElement.TryGetProperty("AccessKeyId", out _));
        Assert.False(aliyunSettings.RootElement.TryGetProperty("AccessKeySecret", out _));
        Assert.Null(tencentSms.ProtectedSecrets);
        Assert.Null(aliyunVoice.ProtectedSecrets);
    }

    [Fact]
    public async Task Dynamic_schedule_runtime_should_register_and_remove_quartz_resources()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var runtime = scope.ServiceProvider.GetRequiredService<ScheduleTaskRuntime>();
        var protection = scope.ServiceProvider.GetRequiredService<ScheduleTaskConfigurationProtection>();
        var scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var id = Guid.NewGuid();
        var entity = new ScheduleTaskConfigurationEntity(id)
        {
            Key = $"dynamic-{id:N}",
            Group = "Custom",
            DisplayName = "Integration service task",
            CronExpression = "0 0/10 * * * ?",
            Enabled = true,
            TaskType = ScheduleTaskTypes.ServiceMethod,
            IsBuiltIn = false,
            ProtectedConfiguration = protection.Protect(new ScheduleTaskExecutionConfiguration
            {
                ServiceMethodKey = "system.file-storage.ensure-initialized",
                ServiceArgumentsJson = "{}"
            })
        };
        await database.Insertable(entity).ExecuteCommandAsync();

        await runtime.ApplyAsync(entity);
        var jobKey = new JobKey(entity.Key, entity.Group);
        Assert.True(await scheduler.CheckExists(jobKey));
        Assert.True(await scheduler.CheckExists(ScheduleTaskRuntime.TriggerKeyFor(jobKey)));

        await runtime.RemoveAsync(entity);
        await database.Deleteable<ScheduleTaskConfigurationEntity>(entity.Id).ExecuteCommandAsync();
        Assert.False(await scheduler.CheckExists(jobKey));
    }

    [Fact]
    public async Task Database_storage_provider_should_round_trip_file_content()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        await migrator.MigrateAsync();

        var key = $"tests/{Guid.NewGuid():N}.txt";
        var bytes = global::System.Text.Encoding.UTF8.GetBytes("database file storage");
        await using (var source = new MemoryStream(bytes, writable: false))
        {
            var saved = await storage.SaveAsync(
                new FileStorageWriteRequest(
                    key,
                    "database.txt",
                    "text/plain",
                    source,
                    bytes.LongLength,
                    Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(bytes))),
                FileStorageProviderNames.Database);
            Assert.Equal(FileStorageProviderNames.Database, saved.Provider);
        }

        var opened = await storage.OpenReadAsync(key, FileStorageProviderNames.Database);
        Assert.NotNull(opened);
        await using (opened!.Content)
        using (var reader = new StreamReader(opened.Content))
        {
            Assert.Equal("database file storage", await reader.ReadToEndAsync());
        }

        await storage.DeleteAsync(key, FileStorageProviderNames.Database);
        Assert.False(await storage.ExistsAsync(key, FileStorageProviderNames.Database));
    }

    [Fact]
    public async Task Attribute_rate_limit_should_use_configured_defaults()
    {
        using var client = fixture.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.210");
        var path = "/api/system/public-file/avatar?key=avatars/users/00000000000000000000000000000000.webp&provider=Local";

        using var first = await client.GetAsync(path);
        using var second = await client.GetAsync(path);
        using var rejected = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public void User_deletion_handlers_should_be_registered()
    {
        using var scope = fixture.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IUserDeletedHandler>().ToList();

        Assert.Contains(handlers, handler => handler is SystemUserAccountDataCleanupHandler);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserService>());
    }

    [Fact]
    public void Business_error_resources_should_follow_the_current_ui_culture()
    {
        var localizer = fixture.Services.GetRequiredService<IStringLocalizer<AdminResource>>();
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal(
                "The username or password is incorrect.",
                localizer["Sim:System:InvalidCredentials"].Value);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
            Assert.Equal(
                "用户名或密码错误",
                localizer["Sim:System:InvalidCredentials"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public async Task Exception_filter_should_localize_business_error_codes()
    {
        var filter = new ExceptionFilter(
            fixture.Services.GetRequiredService<IStringLocalizerFactory>(),
            fixture.Services.GetRequiredService<IOptions<AbpExceptionLocalizationOptions>>());
        var exception = new BusinessException(
                SystemErrorCodes.PasswordTooShort,
                "The password does not satisfy the password policy.")
            .WithData("MinLength", 8);
        var context = new ExceptionContext(
            new ActionContext(
                new DefaultHttpContext { RequestServices = fixture.Services },
                new RouteData(),
                new ControllerActionDescriptor()),
            Array.Empty<IFilterMetadata>())
        {
            Exception = exception
        };
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
            await filter.OnExceptionAsync(context);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);

        var payload = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(
            "Sim:System:PasswordTooShort",
            payload.GetProperty("Error").GetProperty("Code").GetString());
        Assert.Equal(
            "密码长度不能少于 8 位",
            payload.GetProperty("Error").GetProperty("Message").GetString());
    }

    [Fact]
    public async Task Exception_filter_should_localize_error_code_passed_to_single_argument_constructor()
    {
        var filter = new ExceptionFilter(
            fixture.Services.GetRequiredService<IStringLocalizerFactory>(),
            fixture.Services.GetRequiredService<IOptions<AbpExceptionLocalizationOptions>>());
        var exception = new BusinessException(SystemErrorCodes.PermissionNotFound);
        var context = new ExceptionContext(
            new ActionContext(
                new DefaultHttpContext { RequestServices = fixture.Services },
                new RouteData(),
                new ControllerActionDescriptor()),
            Array.Empty<IFilterMetadata>())
        {
            Exception = exception
        };
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
            await filter.OnExceptionAsync(context);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);

        var payload = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(
            SystemErrorCodes.PermissionNotFound,
            payload.GetProperty("Error").GetProperty("Code").GetString());
        Assert.Equal(
            "权限不存在",
            payload.GetProperty("Error").GetProperty("Message").GetString());
    }

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response)
    {
        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);
        return document.RootElement.GetProperty("status").GetString();
    }

    [Fact]
    public async Task Swagger_documents_should_stay_anonymous_in_non_production()
    {
        using var client = fixture.CreateHttpsClient();

        using var pageResponse = await client.GetAsync("/swagger/index.html");
        using var aggregateResponse = await client.GetAsync("/swagger/all/swagger.json");
        using var moduleResponse = await client.GetAsync("/swagger/admin/swagger.json");

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Null(pageResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, aggregateResponse.StatusCode);
        Assert.Equal("application/json", aggregateResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, moduleResponse.StatusCode);
    }

    [Fact]
    public async Task Swagger_login_page_should_no_longer_exist()
    {
        using var client = fixture.CreateHttpsClient();

        using var pageResponse = await client.GetAsync("/swagger/login.html");
        using var scriptResponse = await client.GetAsync("/swagger/login.js");
        using var legacyResponse = await client.GetAsync("/swagger-login.html");

        Assert.Equal(HttpStatusCode.NotFound, pageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scriptResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, legacyResponse.StatusCode);
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "import-tester";
        public string? Name => "Import";
        public string? SurName => "Tester";
        public string? Email => "import-tester@example.test";
        public bool EmailVerified => true;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => ["admin"];
        public Claim? FindClaim(string claimType) => null;
        public string? FindClaimValue(string claimType) => null;
        public Claim[] FindClaims(string claimType) => [];
        public Claim[] GetAllClaims() => [];
        public bool IsInRole(string roleName) => roleName == "admin";
    }
    private sealed record AntiforgeryTokenResponse(string RequestToken);
}

public sealed class HostFixture : IAsyncLifetime
{
    private readonly string _workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "sim-host-tests",
        Guid.NewGuid().ToString("N"));

    // 宿主启动会改写 Serilog 的静态 Log.Logger；xUnit 并行创建各类测试夹具，
    // 并发启动多个宿主会因日志器被重复冻结而失败，因此串行化宿主创建与启动。
    private static readonly SemaphoreSlim HostStartupLock = new(1, 1);

    private WebApplication? _application;

    public IServiceProvider Services =>
        _application?.Services ?? throw new InvalidOperationException("测试 Host 尚未初始化");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_workingDirectory);
        var databasePath = Path.Combine(_workingDirectory, "integration.db");
        var dataProtectionPath = Path.Combine(_workingDirectory, "data-protection");
        var hostProjectDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Sim.Admin.HttpApi.Host");

        await HostStartupLock.WaitAsync();
        try
        {
            await StartHostAsync(hostProjectDirectory, databasePath, dataProtectionPath);
        }
        finally
        {
            HostStartupLock.Release();
        }
    }

    private async Task StartHostAsync(
        string hostProjectDirectory,
        string databasePath,
        string dataProtectionPath)
    {
        _application = await HostProgram.CreateApplicationAsync(
            ["--contentRoot", hostProjectDirectory, "--environment", Environments.Development],
            builder =>
        {
            builder.Environment.EnvironmentName = "Development";
            builder.Environment.ApplicationName = typeof(HostProgram).Assembly.GetName().Name!;
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:SelfUrl"] = "https://localhost",
                ["JwtSettings:IssuerSigningKey"] = "integration-test-signing-key-with-at-least-32-bytes",
                ["DataProtection:KeysPath"] = dataProtectionPath,
                ["Logging:File:Enabled"] = "false",
                ["Framework:Caching:Provider"] = "Local",
                ["Framework:Messaging:Enabled"] = "false",
                ["Framework:Messaging:Provider"] = "Local",
                ["Framework:SignalR:UseRedisBackplane"] = "false",
                ["System:Notifications:DeliveryEnabled"] = "false",
                ["RateLimiting:DefaultPermitLimit"] = "2",
                ["RateLimiting:DefaultWindowSeconds"] = "60",
                ["RateLimiting:QueueLimit"] = "0",
                ["FileManagement:TemporaryPath"] = Path.Combine(_workingDirectory, "processing"),
                ["FileManagement:Local:RootPath"] = Path.Combine(_workingDirectory, "files"),
                ["DataBase:EnabledCodeFirst"] = "false",
                ["DataBase:EnabledDbSeed"] = "false",
                ["DataBase:ConnectionConfigs:0:ConfigId"] = "Default",
                ["DataBase:ConnectionConfigs:0:DbType"] = "Sqlite",
                ["DataBase:ConnectionConfigs:0:ConnectionString"] = $"DataSource={databasePath};Pooling=False",
                ["DataBase:ConnectionConfigs:0:IsAutoCloseConnection"] = "true",
                ["Quartz:JobStore:Provider"] = ""
            });
            // 审批引擎要求业务类型已注册描述符；测试用业务类型在宿主模块加载前注册。
            builder.Services.AddSingleton<
                Sim.Approval.Application.Contracts.Ports.IApprovalBusinessDescriptor,
                TestApprovalBusinessDescriptor>();
            builder.Services.AddSingleton<
                Sim.Approval.Application.Contracts.Ports.IApprovalBusinessDescriptor,
                TestFieldedApprovalBusinessDescriptor>();
        });

        using (var scope = _application.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IDatabaseSchemaMigrator>()
                .MigrateAsync();
        }
        await _application.StartAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sim.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位包含 Sim.slnx 的仓库根目录。");
    }

    public HttpClient CreateHttpsClient()
    {
        var client = _application?.GetTestClient() ??
                     throw new InvalidOperationException("测试 Host 尚未初始化");
        client.BaseAddress = new Uri("https://localhost");
        return client;
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }

        if (Directory.Exists(_workingDirectory))
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Directory.Delete(_workingDirectory, recursive: true);
                    break;
                }
                catch (IOException) when (attempt < 2)
                {
                    await Task.Delay(50);
                }
                catch (IOException)
                {
                    // Windows 上的 SQLite 原生句柄可能延迟到测试进程退出才释放；
                    // 测试数据位于系统临时目录，清理失败不应掩盖测试结果。
                }
            }
        }
    }
}
