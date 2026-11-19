using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 业务类型注册表 — 汇总各业务模块注册的审批描述符与结果回调，按业务类型索引。
/// 描述符缺失时视为业务类型未接入审批，发起请求将被拒绝。
/// </summary>
public sealed class ApprovalBusinessRegistry
{
    private readonly Dictionary<string, IApprovalBusinessDescriptor> _descriptors;
    private readonly Dictionary<string, IApprovalResultCallback> _callbacks;

    public ApprovalBusinessRegistry(
        IEnumerable<IApprovalBusinessDescriptor> descriptors,
        IEnumerable<IApprovalResultCallback> callbacks)
    {
        _descriptors = descriptors
            .GroupBy(item => item.BusinessType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _callbacks = callbacks
            .GroupBy(item => item.BusinessType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>获取全部业务类型描述符（按业务类型排序）。</summary>
    public IReadOnlyList<IApprovalBusinessDescriptor> Descriptors => _descriptors.Values
        .OrderBy(item => item.BusinessType, StringComparer.Ordinal)
        .ToList();

    /// <summary>按业务类型获取描述符，未注册时抛出业务异常。</summary>
    public IApprovalBusinessDescriptor GetRequired(string businessType)
    {
        if (!_descriptors.TryGetValue(businessType, out var descriptor))
        {
            throw new BusinessException(
                ApprovalErrorCodes.BusinessTypeNotRegistered,
                $"业务类型 [{businessType}] 未注册审批描述符。");
        }

        return descriptor;
    }

    /// <summary>按业务类型获取描述符，未注册时返回 null。</summary>
    public IApprovalBusinessDescriptor? FindDescriptor(string businessType) =>
        string.IsNullOrWhiteSpace(businessType) ? null : _descriptors.GetValueOrDefault(businessType);

    /// <summary>按业务类型获取结果回调；未注册时返回 null。</summary>
    public IApprovalResultCallback? FindCallback(string businessType) =>
        _callbacks.GetValueOrDefault(businessType);
}
