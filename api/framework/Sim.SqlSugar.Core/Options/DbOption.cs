using SqlSugar;

namespace Sim.SqlSugar.Core.Options
{
    public class DbOption
    {
        public bool EnabledSaasMultiTenancy { get; set; }

        public bool EnabledCodeFirst { get; set; }

        public bool EnabledDbSeed { get; set; }

        /// <summary>
        /// 是否启用下划线命名（snake_case）
        /// true: 实体属性名和表名转换为下划线命名（例如：CreationTime → creation_time）
        /// false: 保持原始命名（例如：CreationTime → CreationTime）
        /// </summary>
        public bool UseSnakeCaseNaming { get; set; } = false;

        /// <summary>
        /// 是否启用 SQL 执行日志（输出每条 SQL 语句及参数）。
        /// 默认 false。开发环境可设为 true 用于排查问题。
        /// </summary>
        public bool EnableSqlLog { get; set; } = false;

        public required List<ConnectionConfig> ConnectionConfigs { get; set; }
    }
}
