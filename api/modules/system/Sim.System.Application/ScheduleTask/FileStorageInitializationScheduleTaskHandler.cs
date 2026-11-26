using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Application.FileStorage;

namespace Sim.System.Application.ScheduleTask;

public sealed class FileStorageInitializationScheduleTaskHandler(
    FileStorageConfigurationInitializer initializer) :
    IScheduleTaskServiceMethodHandler
{
    public string Key => "system.file-storage.ensure-initialized";
    public string DisplayName => "初始化文件存储配置";
    public string Description => "调用系统服务检查并补齐文件存储提供器配置。";

    public Task ExecuteAsync(
        string? argumentsJson,
        CancellationToken cancellationToken = default) => initializer.EnsureInitializedAsync();
}
