using System;
using System.Threading.Tasks;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.SystemConfig;
using Sim.System.Domain.User;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 读取站点首次初始化状态。状态由 <c>system.setup.state</c> 配置项表达；
/// 没有标记但已有用户的历史站点按已完成处理，避免升级后被重新初始化。
/// </summary>
public class SetupStateReader
{
    private readonly ISqlSugarRepository<SystemConfigurationItemEntity> _configurationRepository;
    private readonly IUserRepository _userRepository;

    public SetupStateReader(
        ISqlSugarRepository<SystemConfigurationItemEntity> configurationRepository,
        IUserRepository userRepository)
    {
        _configurationRepository = configurationRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// 获取初始化状态标记，未写入时返回 null。
    /// </summary>
    public async Task<string?> GetStateAsync()
    {
        var item = await _configurationRepository.GetAsync(item =>
            item.TenantScope == "host" &&
            item.ConfigKey == DataSeedConsts.SetupStateConfigKey &&
            item.Enabled);
        return item?.Value;
    }

    /// <summary>
    /// 站点是否已完成初始化（含升级前已存在用户的历史站点）。
    /// </summary>
    public async Task<bool> IsInitializedAsync()
    {
        var state = await GetStateAsync();
        if (string.Equals(state, DataSeedConsts.SetupStateCompleted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(state, DataSeedConsts.SetupStatePending, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return await _userRepository.GetQueryable().AnyAsync();
    }
}
