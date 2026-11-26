using System.Text.RegularExpressions;
using Sim.Framework.Domain.Shared.Exceptions;
using SqlSugar;

namespace Sim.Framework.Application.Extensions;

/// <summary>
/// 查询扩展方法
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 应用排序，自动将实体属性名转换为数据库列名
    /// 
    /// 支持格式：
    /// - 单列: "Name asc" 或 "Name desc"
    /// - 多列: "Name asc, CreationTime desc"
    /// 
    /// 注意：
    /// 1. 输入使用实体属性名（PascalCase），如 "CreationTime"
    /// 2. 自动转换为数据库列名（根据 SqlSugar 配置，可能是 PascalCase 或 snake_case）
    /// 3. 支持 [SugarColumn(ColumnName="xxx")] 特性自定义映射
    /// 4. 验证字段名是否存在，防止 SQL 注入
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="sorting">排序字符串，使用实体属性名（不是数据库列名）</param>
    /// <returns>应用排序后的查询对象</returns>
    public static ISugarQueryable<T> ApplySorting<T>(this ISugarQueryable<T> queryable, string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return queryable;
        }

        // 获取实体信息
        var entityInfo = queryable.Context.EntityMaintenance.GetEntityInfo<T>();
        
        // 转换排序字符串：将实体属性名替换为数据库列名
        var dbSorting = ConvertPropertyNamesToColumnNames(sorting, entityInfo);
        
        // 使用转换后的列名进行排序
        return queryable.OrderBy(dbSorting);
    }

    /// <summary>
    /// 将排序字符串中的实体属性名转换为数据库列名
    /// </summary>
    private static string ConvertPropertyNamesToColumnNames(string sorting, EntityInfo entityInfo)
    {
        // 解析排序表达式：字段名 asc/desc
        // 支持格式：
        // - "Name asc"
        // - "Name asc, CreationTime desc"
        // - "Name asc,CreationTime desc" (无空格)
        
        var sortExpressions = sorting.Split(',');
        var convertedExpressions = new List<string>();

        foreach (var expression in sortExpressions)
        {
            var trimmed = expression.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // 使用正则表达式提取字段名和排序方向
            // 匹配格式：字段名 + 可选的空格 + 可选的 asc/desc
            var match = Regex.Match(trimmed, @"^(\w+)\s*(asc|desc)?$", RegexOptions.IgnoreCase);
            
            if (!match.Success)
            {
                throw new ValidationException($"Invalid sorting expression: {trimmed}");
            }

            var propertyName = match.Groups[1].Value;
            var direction = match.Groups[2].Success ? match.Groups[2].Value : "";

            // 查找对应的列信息
            var column = entityInfo.Columns.FirstOrDefault(c => 
                c.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

            if (column != null)
            {
                // 使用数据库列名
                var dbColumnName = column.DbColumnName;
                var convertedExpression = string.IsNullOrEmpty(direction) 
                    ? dbColumnName 
                    : $"{dbColumnName} {direction}";
                convertedExpressions.Add(convertedExpression);
            }
            else
            {
                throw new ValidationException($"Unknown sorting property: {propertyName}");
            }
        }

        return string.Join(", ", convertedExpressions);
    }
}
