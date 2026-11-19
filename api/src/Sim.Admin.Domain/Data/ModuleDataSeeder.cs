using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Domain.ModuleConfig;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 模块数据种子
/// 创建平台管理模块，并汇总各功能模块通过 <see cref="IModuleSeedContributor"/> 自描述的模块。
/// </summary>
public class ModuleDataSeeder
{
    private readonly IModuleConfigRepository _moduleConfigRepository;
    private readonly IEnumerable<IModuleSeedContributor> _moduleSeedContributors;
    private readonly ILogger<ModuleDataSeeder> _logger;

    public ModuleDataSeeder(
        IModuleConfigRepository moduleConfigRepository,
        IEnumerable<IModuleSeedContributor> moduleSeedContributors,
        ILogger<ModuleDataSeeder> logger)
    {
        _moduleConfigRepository = moduleConfigRepository;
        _moduleSeedContributors = moduleSeedContributors;
        _logger = logger;
    }

    /// <summary>
    /// 执行模块数据种子
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始创建基础模块 ==========");

        // 创建系统管理模块
        await CreateModuleIfNotExistsAsync(
            name: "system-management",
            description: "系统管理模块，包含系统配置相关功能",
            icon: "carbon:Settings",
            order: 1000,
            layout: "admin"
        );

        // 创建授权管理模块
        await CreateModuleIfNotExistsAsync(
            name: "authorization-management",
            description: "授权管理模块，包含用户、角色、权限等管理功能",
            icon: "carbon:Security",
            order: 2000,
            layout: "admin"
        );

        // 各功能模块自描述的模块清单
        foreach (var moduleSeed in _moduleSeedContributors
                     .SelectMany(contributor => contributor.Modules)
                     .OrderBy(seed => seed.Order)
                     .ThenBy(seed => seed.Name, StringComparer.Ordinal))
        {
            await CreateModuleIfNotExistsAsync(
                name: moduleSeed.Name,
                description: moduleSeed.Description,
                icon: moduleSeed.Icon,
                order: moduleSeed.Order,
                layout: moduleSeed.Layout,
                emptyRedirect: moduleSeed.EmptyRedirect
            );
        }

        _logger.LogInformation("========== 基础模块创建完成 ==========");
    }

    /// <summary>
    /// 创建模块（如果不存在）
    /// 支持幂等性：如果模块已存在，则跳过创建并返回已存在的模块
    /// </summary>
    /// <param name="name">模块名称</param>
    /// <param name="description">模块描述</param>
    /// <param name="icon">模块图标</param>
    /// <param name="order">排序值</param>
    /// <param name="layout">布局类型</param>
    /// <returns>创建或已存在的模块实体</returns>
    private async Task<ModuleConfigEntity> CreateModuleIfNotExistsAsync(
        string name,
        string? description,
        string? icon,
        int order,
        string layout,
        string? emptyRedirect = null)
    {
        // 检查模块是否已存在
        var existingModule = await _moduleConfigRepository.GetAsync(m => m.Name == name);
        if (existingModule != null)
        {
            _logger.LogDebug("模块已存在，跳过创建: {ModuleName}, ID: {ModuleId}", name, existingModule.Id);
            return existingModule;
        }

        // 创建新模块
        var module = new ModuleConfigEntity
        {
            Name = name,
            Description = description,
            Enabled = true,  // 确保模块状态设置为启用
            Icon = icon,
            Order = order,
            Layout = layout,
            EmptyRedirect = emptyRedirect
        };

        await _moduleConfigRepository.InsertAsync(module);
        _logger.LogInformation("模块创建成功: {ModuleName}, ID: {ModuleId}", name, module.Id);
        
        return module;
    }
}
