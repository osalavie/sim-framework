using System.Collections.Generic;

namespace Sim.Framework.Application.Contracts.Localization;

/// <summary>
/// 业务模块向客户端本地化字典贡献文本。键与值由模块自行维护，
/// 平台只负责合并，不感知具体业务。
/// </summary>
public interface IClientLocalizationContributor
{
    /// <summary>
    /// 返回当前文化下要下发给客户端的文本。
    /// </summary>
    IReadOnlyDictionary<string, string> GetTexts();
}
