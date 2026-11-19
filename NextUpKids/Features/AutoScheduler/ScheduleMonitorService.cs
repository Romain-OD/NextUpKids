using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;

namespace NextUpKids.Features.AutoScheduler;

public sealed class ScheduleMonitorService : IDisposable
{
	readonly AppDatabase database;
	readonly TimeProvider time;
	readonly IMessenger messenger;
	readonly ILogger<ScheduleMonitorService> logger;
	readonly SemaphoreSlim checking = new(1);
	SynchronizationContext? context;
	ITimer? timer;
	PeriodStartedMessage? last;
	volatile bool running;
	int generation;
	int ticking;

	public ScheduleMonitorService(AppDatabase database, TimeProvider time, IMessenger messenger, ILogger<ScheduleMonitorService> logger)
	{
		this.database = database;
		this.time = time;
		this.messenger = messenger;
		this.logger = logger;
		messenger.Register<RoutineChangedMessage>(this, (r, message) => _ = ((ScheduleMonitorService)r).TickAsync());
	}

	public async Task StartAsync()
	{
		if (running) return;
		running = true;
		var run = ++generation;
		context = SynchronizationContext.Current;
		try
		{
			// Schedule times have minute precision, independent of when the app was opened.
			var due = TimeSpan.FromTicks(TimeSpan.TicksPerMinute - time.GetLocalNow().Ticks % TimeSpan.TicksPerMinute);
			timer = time.CreateTimer(_ => _ = TickAsync(), null, due, TimeSpan.FromMinutes(1));
			await CheckAsync();
		}
		catch
		{
			if (generation == run) Stop();
			throw;
		}
	}

	public void Stop()
	{
		running = false;
		generation++;
		timer?.Dispose();
		timer = null;
	}

	public async Task<PeriodStartedMessage?> GetCurrentAsync()
	{
		var db = await database.ConnectionAsync().ConfigureAwait(false);
		var settings = await db.Table<PeriodSetting>().ToListAsync().ConfigureAwait(false);
		var steps = await db.Table<RoutineStep>().ToListAsync().ConfigureAwait(false);
		var now = time.GetLocalNow().DateTime;
		var from = now.Date.AddDays(-1);
		var events = await db.Table<CalendarEvent>().Where(e => e.Start <= now && e.Start >= from).ToListAsync().ConfigureAwait(false);
		var withSteps = (await db.Table<EventStep>().ToListAsync().ConfigureAwait(false)).Select(s => s.RoutineId).ToHashSet();
		// The most recent start wins; at the same minute an event beats the daily period.
		var periods = from offset in new[] { 0, -1 }
					  let date = now.Date.AddDays(offset)
					  from setting in settings
					  let start = date + setting.StartTime
					  where start <= now && steps.Any(s => s.Day == date.DayOfWeek && s.Period == setting.Period)
					  select (start, rank: (int)setting.Period, message: new PeriodStartedMessage(DateOnly.FromDateTime(date), setting.Period));
		var dated = from e in events
					where withSteps.Contains(e.RoutineId)
					select (start: e.Start, rank: 100 + e.Id, message: new PeriodStartedMessage(DateOnly.FromDateTime(e.Start), Period.AfterSchool, e.Id));
		return periods.Concat(dated).OrderByDescending(c => c.start).ThenByDescending(c => c.rank).Select(c => c.message).FirstOrDefault();
	}

	public async Task CheckAsync()
	{
		if (!running) return;
		await checking.WaitAsync().ConfigureAwait(false);
		try
		{
			var run = generation;
			var current = await GetCurrentAsync().ConfigureAwait(false);
			if (!running || generation != run || current is null || current == last) return;
			await PublishAsync(current, run).ConfigureAwait(false);
			if (running && generation == run) last = current;
		}
		finally { checking.Release(); }
	}

	async Task PublishAsync(PeriodStartedMessage message, int run)
	{
		if (context is null || context == SynchronizationContext.Current)
		{
			if (running && generation == run) messenger.Send(message);
			return;
		}
		var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		context.Post(_ =>
		{
			try
			{
				if (running && generation == run) messenger.Send(message);
				sent.SetResult();
			}
			catch (Exception ex) { sent.SetException(ex); }
		}, null);
		await sent.Task.ConfigureAwait(false);
	}

	async Task TickAsync()
	{
		if (!running || Interlocked.Exchange(ref ticking, 1) != 0) return;
		try { await CheckAsync().ConfigureAwait(false); }
		catch (Exception ex) { logger.LogError(ex, "Unable to check the routine schedule"); }
		finally { Volatile.Write(ref ticking, 0); }
	}

	public void Dispose()
	{
		Stop();
		messenger.UnregisterAll(this);
	}
}
