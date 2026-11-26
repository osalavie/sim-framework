using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared;
using Sim.SqlSugar.Core.Options;
using SqlSugar;

namespace Sim.System.Application.ScheduleTask;

/// <summary>
/// Creates Quartz ADO.NET JobStore tables if they do not exist.
/// Uses the SqlSugar client to avoid an extra ADO.NET dependency.
/// Supports SQLite (development) and PostgreSQL (production) auto-creation.
/// </summary>
public sealed class QuartzDatabaseInitializer(
    IConfiguration configuration,
    ISqlSugarClient sqlSugarClient,
    ILogger<QuartzDatabaseInitializer> logger)
{
    public async Task EnsureTablesAsync(CancellationToken cancellationToken = default)
    {
        var jobStore = configuration.GetSection("Quartz:JobStore");
        if (!jobStore.Exists())
        {
            return; // RAMJobStore, no tables needed
        }

        var provider = jobStore.GetValue<string>("Provider")?.ToLowerInvariant();

        var connectionString = DatabaseConnectionStringResolver.Resolve(
            configuration,
            jobStore.GetValue<string>("ConnectionStringName") ?? "Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("Quartz JobStore connection string is empty, skipping table creation");
            return;
        }

        // Resolve the table prefix from config, defaulting to the global sim_ prefix
        // combined with Quartz's provider-conventional base prefix.
        var tablePrefix = QuartzTablePrefixResolver.Resolve(jobStore, provider);

        string[] schemaScripts = provider switch
        {
            "sqlite" or "sqlite-microsoft" => QuartzSqliteSchema.GetTables(tablePrefix),
            "postgres" => QuartzPostgresSchema.GetTables(tablePrefix),
            _ => [] // sqlserver/mysql: use external migration scripts
        };

        if (schemaScripts.Length == 0)
        {
            logger.LogInformation(
                "Quartz JobStore provider '{Provider}' requires manual migration scripts (table prefix: {Prefix})",
                provider, tablePrefix);
            return;
        }

        try
        {
            foreach (var sql in schemaScripts)
            {
                await sqlSugarClient.Ado.ExecuteCommandAsync(sql);
            }

            logger.LogInformation(
                "Quartz {Provider} JobStore tables verified (prefix: {Prefix})",
                provider, tablePrefix);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Quartz JobStore tables for provider {Provider}", provider);
            // Don't throw for PostgreSQL - schema validation will catch missing tables
            // with a clearer error message from Quartz itself
            if (provider is "sqlite" or "sqlite-microsoft")
            {
                throw;
            }
        }
    }
}

/// <summary>
/// Resolves the Quartz table prefix from configuration with provider-aware defaults.
/// The prefix can be set explicitly via <c>Quartz:JobStore:TablePrefix</c>.
/// If not set, it defaults to <c>Consts.TablePrefix</c> + provider-conventional base:
/// <list type="bullet">
/// <item>PostgreSQL / MySQL: <c>sim_qrtz_</c> (lowercase)</item>
/// <item>SQL Server / SQLite: <c>sim_QRTZ_</c> (uppercase, Quartz default)</item>
/// </list>
/// </summary>
internal static class QuartzTablePrefixResolver
{
    public static string Resolve(IConfigurationSection jobStoreSection, string? provider)
    {
        var configured = jobStoreSection.GetValue<string>("TablePrefix");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        // Default: global prefix + provider-conventional Quartz base prefix
        return provider switch
        {
            "postgres" or "mysql" or "mysqlconnector" => Consts.TablePrefix + "qrtz_",
            _ => Consts.TablePrefix + "QRTZ_"
        };
    }
}

/// <summary>
/// Standard Quartz 3.x ADO.NET JobStore schema for SQLite.
/// </summary>
internal static class QuartzSqliteSchema
{
    public static string[] GetTables(string p) =>
    [
        $"""
        CREATE TABLE IF NOT EXISTS {p}JOB_DETAILS (
            SCHED_NAME TEXT NOT NULL,
            JOB_NAME TEXT NOT NULL,
            JOB_GROUP TEXT NOT NULL,
            DESCRIPTION TEXT NULL,
            JOB_CLASS_NAME TEXT NOT NULL,
            IS_DURABLE INTEGER NOT NULL,
            IS_NONCONCURRENT INTEGER NOT NULL,
            IS_UPDATE_DATA INTEGER NOT NULL,
            REQUESTS_RECOVERY INTEGER NOT NULL,
            JOB_DATA BLOB NULL,
            PRIMARY KEY (SCHED_NAME, JOB_NAME, JOB_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}TRIGGERS (
            SCHED_NAME TEXT NOT NULL,
            TRIGGER_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            JOB_NAME TEXT NOT NULL,
            JOB_GROUP TEXT NOT NULL,
            DESCRIPTION TEXT NULL,
            NEXT_FIRE_TIME BIGINT NULL,
            PREV_FIRE_TIME BIGINT NULL,
            PRIORITY INTEGER NULL,
            TRIGGER_STATE TEXT NOT NULL,
            TRIGGER_TYPE TEXT NOT NULL,
            START_TIME BIGINT NOT NULL,
            END_TIME BIGINT NULL,
            CALENDAR_NAME TEXT NULL,
            MISFIRE_INSTR INTEGER NULL,
            JOB_DATA BLOB NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, JOB_NAME, JOB_GROUP)
                REFERENCES {p}JOB_DETAILS (SCHED_NAME, JOB_NAME, JOB_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}SIMPLE_TRIGGERS (
            SCHED_NAME TEXT NOT NULL,
            TRIGGER_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            REPEAT_COUNT BIGINT NOT NULL,
            REPEAT_INTERVAL BIGINT NOT NULL,
            TIMES_TRIGGERED BIGINT NOT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES {p}TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}CRON_TRIGGERS (
            SCHED_NAME TEXT NOT NULL,
            TRIGGER_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            CRON_EXPRESSION TEXT NOT NULL,
            TIME_ZONE_ID TEXT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES {p}TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}SIMPROP_TRIGGERS (
            SCHED_NAME TEXT NOT NULL,
            TRIGGER_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            STR_PROP_1 TEXT NULL,
            STR_PROP_2 TEXT NULL,
            STR_PROP_3 TEXT NULL,
            INT_PROP_1 INTEGER NULL,
            INT_PROP_2 INTEGER NULL,
            LONG_PROP_1 BIGINT NULL,
            LONG_PROP_2 BIGINT NULL,
            DEC_PROP_1 NUMERIC NULL,
            DEC_PROP_2 NUMERIC NULL,
            BOOL_PROP_1 INTEGER NULL,
            BOOL_PROP_2 INTEGER NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES {p}TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}BLOB_TRIGGERS (
            SCHED_NAME TEXT NOT NULL,
            TRIGGER_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            BLOB_DATA BLOB NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES {p}TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}CALENDARS (
            SCHED_NAME TEXT NOT NULL,
            CALENDAR_NAME TEXT NOT NULL,
            CALENDAR BLOB NOT NULL,
            PRIMARY KEY (SCHED_NAME, CALENDAR_NAME)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}PAUSED_TRIGGER_GRPS (
            SCHED_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_GROUP)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}FIRED_TRIGGERS (
            SCHED_NAME TEXT NOT NULL,
            ENTRY_ID TEXT NOT NULL,
            TRIGGER_NAME TEXT NOT NULL,
            TRIGGER_GROUP TEXT NOT NULL,
            INSTANCE_NAME TEXT NOT NULL,
            FIRED_TIME BIGINT NOT NULL,
            SCHED_TIME BIGINT NOT NULL,
            PRIORITY INTEGER NOT NULL,
            STATE TEXT NOT NULL,
            JOB_NAME TEXT NULL,
            JOB_GROUP TEXT NULL,
            IS_NONCONCURRENT INTEGER NULL,
            REQUESTS_RECOVERY INTEGER NULL,
            PRIMARY KEY (SCHED_NAME, ENTRY_ID)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}SCHEDULER_STATE (
            SCHED_NAME TEXT NOT NULL,
            INSTANCE_NAME TEXT NOT NULL,
            LAST_CHECKIN_TIME BIGINT NOT NULL,
            CHECKIN_INTERVAL BIGINT NOT NULL,
            PRIMARY KEY (SCHED_NAME, INSTANCE_NAME)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}LOCKS (
            SCHED_NAME TEXT NOT NULL,
            LOCK_NAME TEXT NOT NULL,
            PRIMARY KEY (SCHED_NAME, LOCK_NAME)
        )
        """
    ];
}

/// <summary>
/// Quartz 3.x ADO.NET JobStore schema for PostgreSQL (idempotent).
/// Uses CREATE TABLE IF NOT EXISTS for safe auto-initialization.
/// </summary>
internal static class QuartzPostgresSchema
{
    public static string[] GetTables(string p) =>
    [
        $"""
        CREATE TABLE IF NOT EXISTS {p}job_details (
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
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}triggers (
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
                REFERENCES {p}job_details(sched_name, job_name, job_group)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}simple_triggers (
            sched_name      VARCHAR(120) NOT NULL,
            trigger_name    VARCHAR(200) NOT NULL,
            trigger_group   VARCHAR(200) NOT NULL,
            repeat_count    BIGINT       NOT NULL,
            repeat_interval BIGINT       NOT NULL,
            times_triggered BIGINT       NOT NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES {p}triggers(sched_name, trigger_name, trigger_group)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}cron_triggers (
            sched_name      VARCHAR(120) NOT NULL,
            trigger_name    VARCHAR(200) NOT NULL,
            trigger_group   VARCHAR(200) NOT NULL,
            cron_expression VARCHAR(120) NOT NULL,
            time_zone_id    VARCHAR(80)  NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES {p}triggers(sched_name, trigger_name, trigger_group)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}simprop_triggers (
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
                REFERENCES {p}triggers(sched_name, trigger_name, trigger_group)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}blob_triggers (
            sched_name    VARCHAR(120) NOT NULL,
            trigger_name  VARCHAR(200) NOT NULL,
            trigger_group VARCHAR(200) NOT NULL,
            blob_data     BYTEA        NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES {p}triggers(sched_name, trigger_name, trigger_group)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}calendars (
            sched_name    VARCHAR(120) NOT NULL,
            calendar_name VARCHAR(200) NOT NULL,
            calendar      BYTEA        NOT NULL,
            PRIMARY KEY (sched_name, calendar_name)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}paused_trigger_grps (
            sched_name    VARCHAR(120) NOT NULL,
            trigger_group VARCHAR(200) NOT NULL,
            PRIMARY KEY (sched_name, trigger_group)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}fired_triggers (
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
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}scheduler_state (
            sched_name        VARCHAR(120) NOT NULL,
            instance_name     VARCHAR(200) NOT NULL,
            last_checkin_time BIGINT       NOT NULL,
            checkin_interval  BIGINT       NOT NULL,
            PRIMARY KEY (sched_name, instance_name)
        )
        """,

        $"""
        CREATE TABLE IF NOT EXISTS {p}locks (
            sched_name VARCHAR(120) NOT NULL,
            lock_name  VARCHAR(40)  NOT NULL,
            PRIMARY KEY (sched_name, lock_name)
        )
        """,

        $"""
        CREATE INDEX IF NOT EXISTS idx_{p}j_req_recovery ON {p}job_details(sched_name, requests_recovery)
        """,

        $"""
        CREATE INDEX IF NOT EXISTS idx_{p}j_grp ON {p}job_details(sched_name, job_group)
        """,

        $"""
        CREATE INDEX IF NOT EXISTS idx_{p}t_j ON {p}triggers(sched_name, job_name, job_group)
        """,

        $"""
        CREATE INDEX IF NOT EXISTS idx_{p}t_n_g_state ON {p}triggers(sched_name, trigger_group, trigger_state)
        """,

        $"""
        CREATE INDEX IF NOT EXISTS idx_{p}t_state ON {p}triggers(sched_name, trigger_state)
        """,

        $"""
        CREATE INDEX IF NOT EXISTS idx_{p}t_nft_st ON {p}triggers(sched_name, trigger_state, next_fire_time)
        """
    ];
}
