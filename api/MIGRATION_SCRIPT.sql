-- ============================================
-- 数据迁移脚本：从旧的 Menu 体系迁移到 RouteConfig 体系
-- Migration Script: From old Menu system to RouteConfig system
-- ============================================

-- 注意事项 / Notes:
-- 1. 请在执行前备份数据库 / Backup database before execution
-- 2. 建议在测试环境先执行验证 / Test in staging environment first
-- 3. 根据实际的数据库类型调整语法 / Adjust syntax for your database type

-- ============================================
-- 第一步：检查是否存在旧的 Menu 表
-- Step 1: Check if old Menu tables exist
-- ============================================

-- 检查表是否存在（MySQL/MariaDB）
SELECT 
    TABLE_NAME,
    TABLE_ROWS
FROM 
    information_schema.TABLES
WHERE 
    TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME IN ('sim_menu', 'sim_role_menu');

-- ============================================
-- 第二步：创建系统管理模块（如果不存在）
-- Step 2: Create system management module (if not exists)
-- ============================================

INSERT INTO sim_module_config (
    Id, 
    Name, 
    Description, 
    Version,
    Enabled, 
    Icon, 
    `Order`, 
    Layout, 
    EmptyRedirect,
    CreationTime,
    CreatorId
)
SELECT 
    UUID() as Id,
    'system-management' as Name,
    '系统管理模块' as Description,
    '1.0.0' as Version,
    1 as Enabled,
    'carbon:settings' as Icon,
    1000 as `Order`,
    'admin' as Layout,
    NULL as EmptyRedirect,
    NOW() as CreationTime,
    NULL as CreatorId
WHERE NOT EXISTS (
    SELECT 1 FROM sim_module_config WHERE Name = 'system-management'
);

-- ============================================
-- 第三步：迁移旧的 Menu 数据到 RouteConfig
-- Step 3: Migrate old Menu data to RouteConfig
-- ============================================

-- 注意：这个脚本假设旧的 sim_menu 表存在
-- Note: This script assumes the old sim_menu table exists

-- 3.1 迁移菜单数据到 route_config 表
INSERT INTO sim_route_config (
    Id,
    ModuleId,
    ParentId,
    Name,
    Path,
    Component,
    Redirect,
    CreationTime,
    CreatorId,
    LastModificationTime,
    LastModifierId
)
SELECT 
    m.Id,
    (SELECT Id FROM sim_module_config WHERE Name = 'system-management') as ModuleId,
    m.ParentId,
    m.Name,
    COALESCE(m.Path, '') as Path,
    COALESCE(m.Component, '') as Component,
    m.Redirect,
    m.CreationTime,
    m.CreatorId,
    m.LastModificationTime,
    m.LastModifierId
FROM sim_menu m
WHERE NOT EXISTS (
    SELECT 1 FROM sim_route_config WHERE Id = m.Id
);

-- 3.2 创建对应的 route_meta 记录
INSERT INTO sim_route_meta (
    Id,
    RouteConfigId,
    Title,
    Icon,
    Menu,
    Tab,
    KeepAlive,
    Pinned,
    Anonymous,
    Disabled,
    `Order`,
    Module,
    CreationTime,
    CreatorId
)
SELECT 
    UUID() as Id,
    m.Id as RouteConfigId,
    m.Title,
    m.Icon,
    NOT m.HideMenu as Menu,  -- HideMenu 取反为 Menu
    1 as Tab,
    1 as KeepAlive,
    0 as Pinned,
    0 as Anonymous,
    0 as Disabled,
    m.`Order`,
    NULL as Module,
    NOW() as CreationTime,
    NULL as CreatorId
FROM sim_menu m
WHERE NOT EXISTS (
    SELECT 1 FROM sim_route_meta WHERE RouteConfigId = m.Id
);

-- ============================================
-- 第四步：迁移角色-菜单关联到角色-模块关联
-- Step 4: Migrate role-menu associations to role-module associations
-- ============================================

-- 4.1 为每个有菜单权限的角色分配系统管理模块
INSERT INTO sim_role_module (
    Id,
    RoleId,
    ModuleId,
    CreationTime,
    CreatorId
)
SELECT 
    UUID() as Id,
    rm.RoleId,
    (SELECT Id FROM sim_module_config WHERE Name = 'system-management') as ModuleId,
    NOW() as CreationTime,
    NULL as CreatorId
FROM sim_role_menu rm
WHERE NOT EXISTS (
    SELECT 1 FROM sim_role_module
    WHERE RoleId = rm.RoleId 
    AND ModuleId = (SELECT Id FROM sim_module_config WHERE Name = 'system-management')
)
GROUP BY rm.RoleId;

-- ============================================
-- 第五步：验证数据迁移
-- Step 5: Verify data migration
-- ============================================

-- 5.1 检查迁移的路由数量
SELECT 
    'RouteConfig Count' as CheckItem,
    COUNT(*) as Count
FROM sim_route_config
WHERE ModuleId = (SELECT Id FROM sim_module_config WHERE Name = 'system-management');

-- 5.2 检查迁移的元数据数量
SELECT 
    'RouteMeta Count' as CheckItem,
    COUNT(*) as Count
FROM sim_route_meta
WHERE RouteConfigId IN (
    SELECT Id FROM sim_route_config
    WHERE ModuleId = (SELECT Id FROM sim_module_config WHERE Name = 'system-management')
);

-- 5.3 检查角色-模块关联
SELECT 
    'RoleModule Count' as CheckItem,
    COUNT(*) as Count
FROM sim_role_module
WHERE ModuleId = (SELECT Id FROM sim_module_config WHERE Name = 'system-management');

-- 5.4 对比原始数据
SELECT 
    'Original Menu Count' as CheckItem,
    COUNT(*) as Count
FROM sim_menu;

SELECT 
    'Original RoleMenu Count' as CheckItem,
    COUNT(DISTINCT RoleId) as UniqueRoleCount
FROM sim_role_menu;

-- ============================================
-- 第六步：删除旧表（请谨慎执行！）
-- Step 6: Drop old tables (Execute with caution!)
-- ============================================

-- 警告：执行前请确保数据已正确迁移并经过充分测试
-- Warning: Ensure data is correctly migrated and thoroughly tested before execution

-- 6.1 删除角色-菜单关联表
-- DROP TABLE IF EXISTS sim_role_menu;

-- 6.2 删除旧的菜单表
-- DROP TABLE IF EXISTS sim_menu;

-- ============================================
-- 完成 / Completed
-- ============================================

-- 迁移完成后的建议：
-- Recommendations after migration:
-- 1. 重新运行应用程序并测试所有功能
-- 2. 检查日志确保没有错误
-- 3. 验证用户权限是否正常工作
-- 4. 在确认一切正常后，再删除旧表

