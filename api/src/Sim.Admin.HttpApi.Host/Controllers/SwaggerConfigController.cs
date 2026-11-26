using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Sim.Admin.HttpApi.Host.Controllers;

/// <summary>
/// Swagger 配置控制器
/// </summary>
[ApiController]
[Route("api/swagger-config")]
[AllowAnonymous]
public class SwaggerConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public SwaggerConfigController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 获取 Swagger 文档配置
    /// </summary>
    [HttpGet]
    public IActionResult GetConfig()
    {
        // 从配置中读取 Swagger 文档列表
        var swaggerDocs = _configuration.GetSection("Swagger:Documents").Get<SwaggerDocumentConfig[]>() ?? [];
        foreach (var sd in swaggerDocs)
        {
            sd.Name = sd.Title;
        }
        return Ok(swaggerDocs);
    }
}

/// <summary>
/// Swagger 文档配置
/// </summary>
public class SwaggerDocumentConfig
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set;} = string.Empty;
}
