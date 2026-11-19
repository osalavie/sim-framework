# 项目协作规则

本项目的代码更新需要同步维护文档。

修改代码时请检查：

- 根目录 `README.md`
- `docs/README.md`（文档索引与阅读顺序）
- `docs/core/ARCHITECTURE.md`
- `docs/core/DEVELOPMENT.md`
- `docs/core/DOCUMENTATION.md`
- `docs/core/AI_CODING_GUIDELINES.md`
- `docs/core/FRAMEWORK_SYNC.md`（涉及业务区、组合根或基座同步时）
- `docs/core/ENTERPRISE_SYSTEM_GUIDE.md`
- 对应子项目的 `README.md`

如果改动影响启动方式、依赖、环境变量、模块、路由、接口、认证、数据库、测试、构建或部署流程，请在同一次变更中更新相关文档。

每次功能增强或缺陷修复还必须检查 `docs/core/ENTERPRISE_SYSTEM_GUIDE.md`：判断是否影响企业级建设清单、当前基线、风险优先级或完成定义；需要时在同一次变更中更新，不需要时在交付说明中注明已检查。

编码行为请遵守 `docs/core/AI_CODING_GUIDELINES.md`：先读代码、沿用现有模式、小范围修改、完成后说明验证结果。

已完成的方案与历史记录直接删除或合并回主文档，不再保留归档目录；新增、移动或删除文档时同步更新索引。

文本文件必须保持统一行尾：普通文本使用 CRLF；`*.sh`、`Dockerfile` 和 `Jenkinsfile` 使用 LF。任何编辑完成后不得出现同一文件内 CRLF/LF 混用。
