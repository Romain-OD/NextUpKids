using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;
using NextUpKids.Features.AutoScheduler;

namespace NextUpKids.Tests.Features.AutoScheduler;

public class ScheduleMonitorServiceTests
{
	[Theory]
	[InlineData(30000)]
	[InlineData(12750)]
	public async Task Timer_triggers_at_the_boundary_once_and_stops_cleanly(int millisecondsBeforeStart)
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 7, 0, 0, TimeSpan.Zero).AddMilliseconds(-millisecondsBeforeStart));
		var messenger = new WeakReferenceMessenger();
		var received = new List<PeriodStartedMessage>();
		var boundary = new TaskCompletionSource<PeriodStartedMessage>();
		messenger.Register<PeriodStartedMessage>(received, (r, m) =>
		{
			((List<PeriodStartedMessage>)r).Add(m);
			if (m.Period == Period.BeforeSchool) boundary.TrySetResult(m);
		});
		using var monitor = new ScheduleMonitorService(scope.Database, clock, messenger, NullLogger<ScheduleMonitorService>.Instance);
		await monitor.StartAsync();
		received.Should().Equal(new PeriodStartedMessage(new DateOnly(2026, 11, 15), Period.Bedtime));
		clock.Advance(TimeSpan.FromMilliseconds(millisecondsBeforeStart - 1));
		received.Should().HaveCount(1);
		clock.Advance(TimeSpan.FromMilliseconds(1));
		(await boundary.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
			.Should().Be(new PeriodStartedMessage(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		await monitor.CheckAsync();
		received.Should().HaveCount(2);
		monitor.Stop();
		clock.Advance(TimeSpan.FromHours(12));
		await monitor.CheckAsync();
		received.Should().HaveCount(2);
	}

	[Fact]
	public async Task Resume_uses_only_the_latest_period_and_skips_empty_periods()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 7, 0, 0, TimeSpan.Zero));
		var messenger = new WeakReferenceMessenger();
		var received = new List<PeriodStartedMessage>();
		messenger.Register<PeriodStartedMessage>(received, (r, m) => ((List<PeriodStartedMessage>)r).Add(m));
		using var monitor = new ScheduleMonitorService(scope.Database, clock, messenger, NullLogger<ScheduleMonitorService>.Instance);
		await monitor.StartAsync();
		monitor.Stop();
		clock.Advance(TimeSpan.FromHours(11.5));
		await monitor.StartAsync();
		received.Select(m => m.Period).Should().Equal(Period.BeforeSchool, Period.Mealtime);
		var db = await scope.Database.ConnectionAsync();
		await db.ExecuteAsync("DELETE FROM RoutineStep WHERE Day = ? AND Period = ?", DayOfWeek.Monday, Period.Mealtime);
		(await monitor.GetCurrentAsync())?.Period.Should().Be(Period.AfterSchool);
		await db.DeleteAllAsync<RoutineStep>();
		(await monitor.GetCurrentAsync()).Should().BeNull();
	}

	[Fact]
	public async Task Schedule_changes_use_local_time_and_midnight_keeps_the_previous_day()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 5, 0, 0, TimeSpan.Zero));
		clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("TestZone", TimeSpan.FromHours(-5), "TestZone", "TestZone"));
		using var monitor = new ScheduleMonitorService(scope.Database, clock, new WeakReferenceMessenger(), NullLogger<ScheduleMonitorService>.Instance);
		(await monitor.GetCurrentAsync()).Should().Be(new PeriodStartedMessage(new DateOnly(2026, 11, 16), Period.Bedtime));
		var db = await scope.Database.ConnectionAsync();
		await db.UpdateAsync(new PeriodSetting { Period = Period.BeforeSchool, StartTime = TimeSpan.Zero });
		(await monitor.GetCurrentAsync()).Should().Be(new PeriodStartedMessage(new DateOnly(2026, 11, 17), Period.BeforeSchool));
	}

	[Fact]
	public async Task Routine_changed_message_rechecks_the_schedule_without_waiting_for_a_tick()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
		var messenger = new WeakReferenceMessenger();
		var changed = new TaskCompletionSource<PeriodStartedMessage>();
		messenger.Register<PeriodStartedMessage>(changed, (recipient, message) =>
		{
			if (message.Period == Period.AfterSchool) ((TaskCompletionSource<PeriodStartedMessage>)recipient).TrySetResult(message);
		});
		using var monitor = new ScheduleMonitorService(scope.Database, clock, messenger, NullLogger<ScheduleMonitorService>.Instance);
		await monitor.StartAsync();
		await (await scope.Database.ConnectionAsync()).UpdateAsync(new PeriodSetting { Period = Period.AfterSchool, StartTime = new(9, 0, 0) });
		messenger.Send(new RoutineChangedMessage());
		(await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
			.Should().Be(new PeriodStartedMessage(new DateOnly(2026, 11, 16), Period.AfterSchool));
		await monitor.CheckAsync();
	}
}
