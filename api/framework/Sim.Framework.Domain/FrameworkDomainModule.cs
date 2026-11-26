using Microsoft.Extensions.DependencyInjection;
using Sim.Framework.Domain.Security;
using Volo.Abp.Modularity;

namespace Sim.Framework.Domain
{
    public class FrameworkDomainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // 国密算法服务（SM2/SM4/SM3）
            context.Services.AddTransient<IGmCryptoService, GmCryptoService>();
            // JWT 签名密钥库（HMAC/SM2 多密钥，kid 轮换）
            context.Services.AddSingleton<JwtSigningKeyStore>();
        }
    }
}