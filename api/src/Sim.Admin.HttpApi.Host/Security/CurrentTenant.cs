using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Domain.Shared.Consts;

namespace Sim.Admin.HttpApi.Host.Security;

/// <summary>
/// 基于 HTTP 上下文的当前租户实现。替代 ABP 的租户解析链，
/// 仅从 JWT tenant_id 声明解析租户，确保租户归属由服务端令牌控制。
/// </summary>
public sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public Guid? Id
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            // 仅从 JWT 声明解析，不允许前端通过请求头或查询参数指定租户
            var claimValue = httpContext.User.FindFirstValue(TokenTypeConst.TenantId);
            if (Guid.TryParse(claimValue, out var claimTenantId))
            {
                return claimTenantId;
            }

            return null;
        }
    }

    public string? Name => null;
}
