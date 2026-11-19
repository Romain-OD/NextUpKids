using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;
using NextUpKids.Common.Services;

namespace NextUpKids.Features.ChildPlayer;

public sealed record RewardOption(EventRoutine Routine, bool Affordable)
{
	public string Text => $"{Routine.Title}  ★ {Routine.Cost}";
}

public partial class ChildRoutineViewModel : ObservableObject, IDisposable
{
	readonly AppDatabase database;
	readonly IVoiceService voice;
	readonly TimeProvider time;
	readonly IMessenger messenger;
	CancellationTokenSource playback = new();
	List<RoutineStep> steps = [];
	PeriodStartedMessage? period;
	EventRoutine? activity;
	EventRoutine? reward;
	List<EventRoutine> rewardRoutines = [];
	VoiceMode mode;
	int index;
	bool active = true;
	bool reload;
	DateTimeOffset? deadline;
	TimeSpan? paused;

	[ObservableProperty] public partial string Title { get; set; } = "NextUpKids";
	[ObservableProperty] public partial string Image { get; set; } = "play.png";
	[ObservableProperty] public partial string Progress { get; set; } = "";
	[ObservableProperty] public partial string Error { get; set; } = "";
	[ObservableProperty] public partial bool IsDimmed { get; set; }
	[ObservableProperty, NotifyCanExecuteChangedFor(nameof(DoneCommand))]
	public partial bool HasStep { get; set; }
	[ObservableProperty, NotifyCanExecuteChangedFor(nameof(DoneCommand))]
	public partial bool IsCelebrating { get; set; }
	[ObservableProperty, NotifyCanExecuteChangedFor(nameof(DoneCommand))]
	public partial bool IsLoading { get; set; }
	[ObservableProperty, NotifyPropertyChangedFor(nameof(DoneText))]
	public partial AppLanguage Language { get; set; }
	public string DoneText => Phrases.Done(Language);
	[ObservableProperty] public partial int Stars { get; set; }
	[ObservableProperty] public partial bool HasTimer { get; set; }
	[ObservableProperty] public partial double TimerFraction { get; set; }
	[ObservableProperty] public partial string TimerText { get; set; } = "";
	[ObservableProperty] public partial bool ShowRewards { get; set; }
	public System.Collections.ObjectModel.ObservableCollection<RewardOption> Rewards { get; } = [];

	public ChildRoutineViewModel(AppDatabase database, IVoiceService voice, TimeProvider time, IMessenger messenger)
	{
		this.database = database;
		this.voice = voice;
		this.time = time;
		this.messenger = messenger;
		messenger.Register<PeriodStartedMessage>(this, (r, m) => _ = ((ChildRoutineViewModel)r).SwitchPeriodAsync(m));
		messenger.Register<RoutineChangedMessage>(this, (r, m) =>
		{
			var vm = (ChildRoutineViewModel)r;
			if (vm.period is not null) _ = vm.PlayAsync();
		});
	}

	public Task SwitchPeriodAsync(PeriodStartedMessage next)
	{
		period = next;
		reward = null;
		return PlayAsync();
	}

	async Task PlayAsync()
	{
		if (period is not { } next) return;
		reload = true;
		deadline = null;
		paused = null;
		HasTimer = false;
		ShowRewards = false;
		playback.Cancel();
		playback.Dispose();
		playback = new();
		voice.Cancel();
		if (!active) return;
		reload = false;
		var token = playback.Token;
		IsCelebrating = false;
		IsLoading = true;
		HasStep = false;
		Error = "";
		try
		{
			var db = await database.ConnectionAsync();
			var day = next.Day.DayOfWeek;
			List<RoutineStep> loaded;
			EventRoutine? loadedActivity = null;
			VoiceMode loadedMode;
			if (reward is not null || next.EventId > 0)
			{
				if (reward is not null) loadedActivity = await db.FindAsync<EventRoutine>(reward.Id);
				else if (await db.FindAsync<CalendarEvent>(next.EventId) is { } calendarEvent)
					loadedActivity = await db.FindAsync<EventRoutine>(calendarEvent.RoutineId);
				var routineId = loadedActivity?.Id ?? -1;
				loaded = (await db.Table<EventStep>().Where(s => s.RoutineId == routineId).OrderBy(s => s.Order).ToListAsync())
					.Select(s => new RoutineStep { Title = s.Title, Image = s.Image, Seconds = s.Seconds }).ToList();
				loadedMode = VoiceMode.On;
			}
			else
			{
				loaded = await db.Table<RoutineStep>().Where(s => s.Day == day && s.Period == next.Period).OrderBy(s => s.Order).ToListAsync();
				loadedMode = (await db.FindAsync<PeriodSetting>(next.Period)).Voice;
			}
			var settings = await db.FindAsync<AppSettings>(1);
			var loadedRewards = await db.Table<EventRoutine>().Where(r => r.Cost > 0).OrderBy(r => r.Cost).ToListAsync();
			token.ThrowIfCancellationRequested();
			steps = loaded;
			activity = loadedActivity;
			rewardRoutines = loadedRewards;
			Language = settings.Language;
			Stars = settings.Stars;
			mode = loadedMode;
			index = 0;
			IsLoading = false;
			await ShowFrameAsync(token);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		catch (Exception)
		{
			if (!token.IsCancellationRequested)
				Error = "Impossible de charger le programme. Réessayez depuis l'espace parents.";
		}
		finally
		{
			if (!token.IsCancellationRequested) IsLoading = false;
		}
	}

	bool CanAdvance() => active && HasStep && !IsLoading;

	[RelayCommand(CanExecute = nameof(CanAdvance), AllowConcurrentExecutions = true)]
	async Task DoneAsync()
	{
		if (!CanAdvance()) return;
		var skipping = IsCelebrating;
		playback.Cancel();
		playback.Dispose();
		playback = new();
		var token = playback.Token;
		voice.Cancel();
		if (skipping)
		{
			// A tap during the celebration skips it: the cancelled celebration never reaches its index++,
			// so exactly one step is advanced here.
			IsCelebrating = false;
			index++;
			try { await ShowFrameAsync(token); }
			catch (OperationCanceledException) when (token.IsCancellationRequested) { }
			return;
		}
		IsCelebrating = true;
		var onTime = deadline is { } due && time.GetUtcNow() <= due && activity?.Cost is not > 0;
		deadline = null;
		if (onTime) Stars++;
		try
		{
			// The next step appears after a short cheer; only its spoken title waits for the praise to finish,
			// so a long "Bravo ! Une étoile !" never leaves the screen empty.
			var praise = SpeakAsync(onTime ? Phrases.StarPraise(Language) : Phrases.Praise(Language), token);
			await Task.WhenAll(
				Task.Delay(TimeSpan.FromMilliseconds(700), time, token),
				onTime ? SaveStarAsync() : Task.CompletedTask);
			token.ThrowIfCancellationRequested();
			index++;
			IsCelebrating = false;
			await ShowFrameAsync(token, praise);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		finally
		{
			if (!token.IsCancellationRequested) IsCelebrating = false;
		}
	}

	async Task ShowFrameAsync(CancellationToken token, Task? speakAfter = null)
	{
		token.ThrowIfCancellationRequested();
		HasStep = index < steps.Count;
		IsDimmed = !HasStep && activity is null && period?.Period == Period.Bedtime;
		// Image first: the page reacts to Title and reads Image to pick the buddy's action.
		Image = HasStep ? steps[index].Image : activity?.Image ?? period?.Period switch
		{
			Period.Mealtime => "meal.png",
			Period.Bedtime => "sleep.png",
			_ => "play.png"
		};
		Title = HasStep ? steps[index].Title
			: activity?.Cost > 0 ? Phrases.RewardOver(Language)
			: activity is not null ? Phrases.EventReady(Language)
			: Phrases.Ending(period?.Period ?? Period.AfterSchool, Language);
		Progress = HasStep ? $"{index + 1} / {steps.Count}" : "";
		var seconds = HasStep ? steps[index].Seconds : 0;
		deadline = seconds > 0 ? time.GetUtcNow().AddSeconds(seconds) : null;
		paused = null;
		HasTimer = seconds > 0;
		Tick();
		UpdateRewards();
		if (speakAfter is not null) await speakAfter;
		token.ThrowIfCancellationRequested();
		await SpeakAsync(Title, token);
	}

	// Called by the page a few times per second; kept off a background timer so state only changes on the UI thread.
	public void Tick()
	{
		if (!HasTimer || !HasStep || (deadline is null && paused is null)) return;
		var total = TimeSpan.FromSeconds(steps[index].Seconds);
		var left = deadline is { } due ? due - time.GetUtcNow() : paused ?? TimeSpan.Zero;
		if (left < TimeSpan.Zero) left = TimeSpan.Zero;
		TimerFraction = total > TimeSpan.Zero ? Math.Clamp(left / total, 0, 1) : 0;
		TimerText = $"{(int)left.TotalMinutes}:{left.Seconds:00}";
	}

	partial void OnStarsChanged(int value) => UpdateRewards();

	void UpdateRewards()
	{
		Rewards.Clear();
		foreach (var r in rewardRoutines) Rewards.Add(new(r, r.Cost <= Stars));
		ShowRewards = !HasStep && !IsLoading && !IsDimmed && activity?.Cost is not > 0 && Rewards.Count > 0 && Error.Length == 0;
	}

	async Task SaveStarAsync()
	{
		try
		{
			var db = await database.ConnectionAsync();
			await db.ExecuteAsync("UPDATE AppSettings SET Stars = COALESCE(Stars, 0) + 1 WHERE Id = 1");
		}
		catch (Exception) { }
	}

	[RelayCommand]
	async Task RedeemAsync(RewardOption? option)
	{
		if (option is null || !active || IsLoading || IsCelebrating) return;
		var cost = option.Routine.Cost;
		try
		{
			var db = await database.ConnectionAsync();
			var changed = await db.ExecuteAsync("UPDATE AppSettings SET Stars = Stars - ? WHERE Id = 1 AND COALESCE(Stars, 0) >= ?", cost, cost);
			Stars = (await db.FindAsync<AppSettings>(1)).Stars;
			if (changed == 0) return;
		}
		catch (Exception)
		{
			Error = Language == AppLanguage.French ? "Impossible d'utiliser les étoiles." : "Could not use the stars.";
			return;
		}
		reward = option.Routine;
		await PlayAsync();
	}

	async Task SpeakAsync(string text, CancellationToken token)
	{
		if (!await voice.SpeakAsync(text, Language, mode, token) && !token.IsCancellationRequested)
			Error = Language == AppLanguage.French ? "Voix indisponible sur cette tablette." : "Voice is unavailable on this tablet.";
	}

	[RelayCommand(AllowConcurrentExecutions = true)]
	async Task ReplayAsync()
	{
		if (active && !IsCelebrating && !IsLoading) await SpeakAsync(Title, playback.Token);
	}

	[RelayCommand]
	void RequestParentAccess() => messenger.Send(new ParentGateRequestedMessage());

	public void Suspend()
	{
		active = false;
		reload |= IsLoading;
		IsLoading = false;
		playback.Cancel();
		voice.Cancel();
		IsCelebrating = false;
		if (deadline is { } due)
		{
			paused = due - time.GetUtcNow();
			deadline = null;
		}
		DoneCommand.NotifyCanExecuteChanged();
	}

	public async Task ResumeAsync()
	{
		active = true;
		if (paused is { } left)
		{
			deadline = time.GetUtcNow() + left;
			paused = null;
		}
		if (reload && period is not null) await PlayAsync();
		else
		{
			playback.Dispose();
			playback = new();
			await ReplayAsync();
		}
		DoneCommand.NotifyCanExecuteChanged();
	}

	public void Dispose()
	{
		Suspend();
		playback.Dispose();
		messenger.UnregisterAll(this);
	}
}
