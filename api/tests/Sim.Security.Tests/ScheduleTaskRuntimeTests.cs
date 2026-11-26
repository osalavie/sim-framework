using Quartz;
using Sim.System.Application.ScheduleTask;

namespace Sim.Security.Tests;

public sealed class ScheduleTaskRuntimeTests
{
    private static readonly JobKey JobKey = new("test-job", "Tests");

    [Fact]
    public void Unchanged_cron_trigger_should_not_be_rescheduled()
    {
        var trigger = CreateTrigger("0 0/5 * * * ?", "Test description");

        Assert.False(ScheduleTaskRuntime.RequiresReschedule(
            trigger,
            JobKey,
            "0 0/5 * * * ?",
            "Test description"));
    }

    [Theory]
    [InlineData("0 0/10 * * * ?", "Test description")]
    [InlineData("0 0/5 * * * ?", "Updated description")]
    public void Changed_cron_trigger_should_be_rescheduled(
        string cronExpression,
        string description)
    {
        var trigger = CreateTrigger("0 0/5 * * * ?", "Test description");

        Assert.True(ScheduleTaskRuntime.RequiresReschedule(
            trigger,
            JobKey,
            cronExpression,
            description));
    }

    private static ITrigger CreateTrigger(string cronExpression, string description) =>
        TriggerBuilder.Create()
            .WithIdentity(ScheduleTaskRuntime.TriggerKeyFor(JobKey))
            .ForJob(JobKey)
            .WithDescription(description)
            .WithCronSchedule(cronExpression, builder =>
                builder.WithMisfireHandlingInstructionDoNothing())
            .Build();
}
