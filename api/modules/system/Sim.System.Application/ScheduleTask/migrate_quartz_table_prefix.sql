-- ============================================
-- Quartz 表前缀迁移脚本
-- 将已有的 qrtz_* 表重命名为 sim_qrtz_* 表
-- Migration: Rename Quartz tables from qrtz_* to sim_qrtz_*
-- ============================================

-- 适用数据库: PostgreSQL
-- Database: PostgreSQL

-- 注意事项 / Notes:
-- 1. 请在执行前备份数据库 / Backup database before execution
-- 2. 执行后需重启应用程序 / Restart the application after execution
-- 3. 仅需执行一次 / Execute only once
-- 4. 如果 Quartz 表尚未创建（全新部署），无需执行此脚本
--    If Quartz tables don't exist (fresh deployment), skip this script.
-- 5. 目标前缀由 appsettings.json -> Quartz:JobStore:TablePrefix 控制
--    Target prefix is controlled by appsettings.json -> Quartz:JobStore:TablePrefix
--    本脚本假设目标前缀为默认值 sim_qrtz_
--    This script assumes the default target prefix sim_qrtz_

-- 先删除依赖外键约束上的索引（如果存在）
-- Drop indexes that reference old table names first
DROP INDEX IF EXISTS idx_qrtz_j_req_recovery;
DROP INDEX IF EXISTS idx_qrtz_j_grp;
DROP INDEX IF EXISTS idx_qrtz_t_j;
DROP INDEX IF EXISTS idx_qrtz_t_jg;
DROP INDEX IF EXISTS idx_qrtz_t_c;
DROP INDEX IF EXISTS idx_qrtz_t_n_g_state;
DROP INDEX IF EXISTS idx_qrtz_t_state;
DROP INDEX IF EXISTS idx_qrtz_t_n_state;
DROP INDEX IF EXISTS idx_qrtz_t_nft_st;
DROP INDEX IF EXISTS idx_qrtz_ft_inst_job_req_rcvry;
DROP INDEX IF EXISTS idx_qrtz_ft_t_g;
DROP INDEX IF EXISTS idx_qrtz_ft_trig_inst_name;
DROP INDEX IF EXISTS idx_qrtz_ft_j_g;
DROP INDEX IF EXISTS idx_qrtz_ft_j_name;

-- 重命名表（注意外键依赖顺序，先重命名被引用的表）
-- Rename tables (respect foreign key dependency order)

-- 重命名 qrtz_fired_triggers (无外键依赖，可先重命名)
ALTER TABLE IF EXISTS qrtz_fired_triggers RENAME TO sim_qrtz_fired_triggers;

-- 重命名 qrtz_simple_triggers (依赖 qrtz_triggers)
-- 外键约束会在 qrtz_triggers 重命名后自动更新，但为安全起见先删除旧约束
ALTER TABLE IF EXISTS qrtz_simple_triggers RENAME TO sim_qrtz_simple_triggers;

-- 重命名 qrtz_simprop_triggers (依赖 qrtz_triggers)
ALTER TABLE IF EXISTS qrtz_simprop_triggers RENAME TO sim_qrtz_simprop_triggers;

-- 重命名 qrtz_cron_triggers (依赖 qrtz_triggers)
ALTER TABLE IF EXISTS qrtz_cron_triggers RENAME TO sim_qrtz_cron_triggers;

-- 重命名 qrtz_blob_triggers (依赖 qrtz_triggers)
ALTER TABLE IF EXISTS qrtz_blob_triggers RENAME TO sim_qrtz_blob_triggers;

-- 重命名 qrtz_triggers (依赖 qrtz_job_details)
ALTER TABLE IF EXISTS qrtz_triggers RENAME TO sim_qrtz_triggers;

-- 重命名 qrtz_job_details
ALTER TABLE IF EXISTS qrtz_job_details RENAME TO sim_qrtz_job_details;

-- 重命名其余独立表
ALTER TABLE IF EXISTS qrtz_calendars RENAME TO sim_qrtz_calendars;
ALTER TABLE IF EXISTS qrtz_paused_trigger_grps RENAME TO sim_qrtz_paused_trigger_grps;
ALTER TABLE IF EXISTS qrtz_scheduler_state RENAME TO sim_qrtz_scheduler_state;
ALTER TABLE IF EXISTS qrtz_locks RENAME TO sim_qrtz_locks;

-- 重建外键约束（PostgreSQL 在 RENAME TABLE 时会自动更新约束中的表引用，
-- 但如果约束名仍引用旧表名，需要手动检查）
-- PostgreSQL automatically updates foreign key references when tables are renamed.

-- 重建索引（使用新表名和新索引名）
-- Recreate indexes with new table names and index names
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_j_req_recovery ON sim_qrtz_job_details(sched_name, requests_recovery);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_j_grp ON sim_qrtz_job_details(sched_name, job_group);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_j ON sim_qrtz_triggers(sched_name, job_name, job_group);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_jg ON sim_qrtz_triggers(sched_name, trigger_group);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_c ON sim_qrtz_triggers(sched_name, calendar_name);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_n_g_state ON sim_qrtz_triggers(sched_name, trigger_group, trigger_state);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_state ON sim_qrtz_triggers(sched_name, trigger_state);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_n_state ON sim_qrtz_triggers(sched_name, trigger_name, trigger_group, trigger_state);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_t_nft_st ON sim_qrtz_triggers(sched_name, trigger_state, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_ft_inst_job_req_rcvry ON sim_qrtz_fired_triggers(sched_name, instance_name, requests_recovery);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_ft_t_g ON sim_qrtz_fired_triggers(sched_name, trigger_group);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_ft_trig_inst_name ON sim_qrtz_fired_triggers(sched_name, trigger_name, trigger_group, instance_name);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_ft_j_g ON sim_qrtz_fired_triggers(sched_name, job_group);
CREATE INDEX IF NOT EXISTS idx_sim_qrtz_ft_j_name ON sim_qrtz_fired_triggers(sched_name, job_name, job_group);

-- 验证: 确认新表已存在
-- Verification: Confirm new tables exist
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public' AND table_name LIKE 'sim_qrtz_%'
ORDER BY table_name;
