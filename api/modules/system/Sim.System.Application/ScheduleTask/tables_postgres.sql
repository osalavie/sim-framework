-- Quartz.NET PostgreSQL JobStore Schema
-- Based on official tables_postgres.sql from Quartz 3.x
--
-- Table prefix is configurable via appsettings.json -> Quartz:JobStore:TablePrefix
-- Default for PostgreSQL: "sim_qrtz_" (Consts.TablePrefix + "qrtz_")
-- If you change the prefix, update all table names and index names below accordingly.
-- The QuartzDatabaseInitializer also reads this config and auto-creates tables.
--
-- Execute this script manually for environments where auto-creation is disabled.

DROP TABLE IF EXISTS sim_qrtz_fired_triggers;
DROP TABLE IF EXISTS sim_qrtz_paused_trigger_grps;
DROP TABLE IF EXISTS sim_qrtz_scheduler_state;
DROP TABLE IF EXISTS sim_qrtz_locks;
DROP TABLE IF EXISTS sim_qrtz_simple_triggers;
DROP TABLE IF EXISTS sim_qrtz_simprop_triggers;
DROP TABLE IF EXISTS sim_qrtz_cron_triggers;
DROP TABLE IF EXISTS sim_qrtz_blob_triggers;
DROP TABLE IF EXISTS sim_qrtz_triggers;
DROP TABLE IF EXISTS sim_qrtz_job_details;
DROP TABLE IF EXISTS sim_qrtz_calendars;

CREATE TABLE sim_qrtz_job_details (
    sched_name        VARCHAR(120) NOT NULL,
    job_name          VARCHAR(200) NOT NULL,
    job_group         VARCHAR(200) NOT NULL,
    description       VARCHAR(250) NULL,
    job_class_name    VARCHAR(250) NOT NULL,
    is_durable        BOOL         NOT NULL,
    is_nonconcurrent  BOOL         NOT NULL,
    is_update_data    BOOL         NOT NULL,
    requests_recovery BOOL         NOT NULL,
    job_data          BYTEA        NULL,
    PRIMARY KEY (sched_name, job_name, job_group)
);

CREATE TABLE sim_qrtz_triggers (
    sched_name     VARCHAR(120) NOT NULL,
    trigger_name   VARCHAR(200) NOT NULL,
    trigger_group  VARCHAR(200) NOT NULL,
    job_name       VARCHAR(200) NOT NULL,
    job_group      VARCHAR(200) NOT NULL,
    description    VARCHAR(250) NULL,
    next_fire_time BIGINT       NULL,
    prev_fire_time BIGINT       NULL,
    priority       INTEGER      NOT NULL,
    trigger_state  VARCHAR(16)  NOT NULL,
    trigger_type   VARCHAR(8)   NOT NULL,
    start_time     BIGINT       NOT NULL,
    end_time       BIGINT       NULL,
    calendar_name  VARCHAR(200) NULL,
    misfire_instr  SMALLINT     NULL,
    job_data       BYTEA        NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, job_name, job_group)
        REFERENCES sim_qrtz_job_details(sched_name, job_name, job_group)
);

CREATE TABLE sim_qrtz_simple_triggers (
    sched_name      VARCHAR(120) NOT NULL,
    trigger_name    VARCHAR(200) NOT NULL,
    trigger_group   VARCHAR(200) NOT NULL,
    repeat_count    BIGINT       NOT NULL,
    repeat_interval BIGINT       NOT NULL,
    times_triggered BIGINT       NOT NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES sim_qrtz_triggers(sched_name, trigger_name, trigger_group)
);

CREATE TABLE sim_qrtz_cron_triggers (
    sched_name      VARCHAR(120) NOT NULL,
    trigger_name    VARCHAR(200) NOT NULL,
    trigger_group   VARCHAR(200) NOT NULL,
    cron_expression VARCHAR(120) NOT NULL,
    time_zone_id    VARCHAR(80)  NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES sim_qrtz_triggers(sched_name, trigger_name, trigger_group)
);

CREATE TABLE sim_qrtz_simprop_triggers (
    sched_name    VARCHAR(120)   NOT NULL,
    trigger_name  VARCHAR(200)   NOT NULL,
    trigger_group VARCHAR(200)   NOT NULL,
    str_prop_1    VARCHAR(512)   NULL,
    str_prop_2    VARCHAR(512)   NULL,
    str_prop_3    VARCHAR(512)   NULL,
    int_prop_1    INTEGER        NULL,
    int_prop_2    INTEGER        NULL,
    long_prop_1   BIGINT         NULL,
    long_prop_2   BIGINT         NULL,
    dec_prop_1    NUMERIC(13,4)  NULL,
    dec_prop_2    NUMERIC(13,4)  NULL,
    bool_prop_1   BOOL           NULL,
    bool_prop_2   BOOL           NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES sim_qrtz_triggers(sched_name, trigger_name, trigger_group)
);

CREATE TABLE sim_qrtz_blob_triggers (
    sched_name    VARCHAR(120) NOT NULL,
    trigger_name  VARCHAR(200) NOT NULL,
    trigger_group VARCHAR(200) NOT NULL,
    blob_data     BYTEA        NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES sim_qrtz_triggers(sched_name, trigger_name, trigger_group)
);

CREATE TABLE sim_qrtz_calendars (
    sched_name    VARCHAR(120) NOT NULL,
    calendar_name VARCHAR(200) NOT NULL,
    calendar      BYTEA        NOT NULL,
    PRIMARY KEY (sched_name, calendar_name)
);

CREATE TABLE sim_qrtz_paused_trigger_grps (
    sched_name    VARCHAR(120) NOT NULL,
    trigger_group VARCHAR(200) NOT NULL,
    PRIMARY KEY (sched_name, trigger_group)
);

CREATE TABLE sim_qrtz_fired_triggers (
    sched_name        VARCHAR(120) NOT NULL,
    entry_id          VARCHAR(95)  NOT NULL,
    trigger_name      VARCHAR(200) NOT NULL,
    trigger_group     VARCHAR(200) NOT NULL,
    instance_name     VARCHAR(200) NOT NULL,
    fired_time        BIGINT       NOT NULL,
    sched_time        BIGINT       NOT NULL,
    priority          INTEGER      NOT NULL,
    state             VARCHAR(16)  NOT NULL,
    job_name          VARCHAR(200) NULL,
    job_group         VARCHAR(200) NULL,
    is_nonconcurrent  BOOL         NULL,
    requests_recovery BOOL         NULL,
    PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE sim_qrtz_scheduler_state (
    sched_name        VARCHAR(120) NOT NULL,
    instance_name     VARCHAR(200) NOT NULL,
    last_checkin_time BIGINT       NOT NULL,
    checkin_interval  BIGINT       NOT NULL,
    PRIMARY KEY (sched_name, instance_name)
);

CREATE TABLE sim_qrtz_locks (
    sched_name VARCHAR(120) NOT NULL,
    lock_name  VARCHAR(40)  NOT NULL,
    PRIMARY KEY (sched_name, lock_name)
);

CREATE INDEX idx_sim_qrtz_j_req_recovery ON sim_qrtz_job_details(sched_name, requests_recovery);
CREATE INDEX idx_sim_qrtz_j_grp ON sim_qrtz_job_details(sched_name, job_group);
CREATE INDEX idx_sim_qrtz_t_j ON sim_qrtz_triggers(sched_name, job_name, job_group);
CREATE INDEX idx_sim_qrtz_t_jg ON sim_qrtz_triggers(sched_name, job_group);
CREATE INDEX idx_sim_qrtz_t_c ON sim_qrtz_triggers(sched_name, calendar_name);
CREATE INDEX idx_sim_qrtz_t_n_g_state ON sim_qrtz_triggers(sched_name, trigger_group, trigger_state);
CREATE INDEX idx_sim_qrtz_t_state ON sim_qrtz_triggers(sched_name, trigger_state);
CREATE INDEX idx_sim_qrtz_t_n_state ON sim_qrtz_triggers(sched_name, trigger_name, trigger_group, trigger_state);
CREATE INDEX idx_sim_qrtz_t_nft_st ON sim_qrtz_triggers(sched_name, trigger_state, next_fire_time);
CREATE INDEX idx_sim_qrtz_ft_inst_job_req_rcvry ON sim_qrtz_fired_triggers(sched_name, instance_name, requests_recovery);
CREATE INDEX idx_sim_qrtz_ft_t_g ON sim_qrtz_fired_triggers(sched_name, trigger_group);
CREATE INDEX idx_sim_qrtz_ft_trig_inst_name ON sim_qrtz_fired_triggers(sched_name, trigger_name, trigger_group, instance_name);
CREATE INDEX idx_sim_qrtz_ft_j_g ON sim_qrtz_fired_triggers(sched_name, job_group);
CREATE INDEX idx_sim_qrtz_ft_j_name ON sim_qrtz_fired_triggers(sched_name, job_name, job_group);
