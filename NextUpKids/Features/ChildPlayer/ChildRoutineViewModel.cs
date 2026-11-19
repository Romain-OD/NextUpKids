using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;
using NextUpKids.Common.Services;

namespace NextUpKids.Features.ChildPlayer;

public partial class ChildRoutineViewModel : ObservableObject, IDisposable
{
	readonly AppDatabase database;
	readonly IVoiceService voice;
	readonly TimeProvider time;
	readonly IMessenger messenger;
	CancellationTokenSource playback = new();
	List<RoutineStep> steps = [];
	PeriodStartedMessage? period;
	VoiceMode mode;
	int index;
	bool active = true;
	bool reload;

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
			if (vm.period is { } current) _ = vm.SwitchPeriodAsync(current);
		});
	}

	public async Task SwitchPeriodAsync(PeriodStartedMessage next)
	{
		period = next;
		reload = true;
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
			var loaded = await db.Table<RoutineStep>().Where(s => s.Day == day && s.Period == next.Period).OrderBy(s => s.Order).ToListAsync();
			var settings = await db.FindAsync<AppSettings>(1);
			var periodSettings = await db.FindAsync<PeriodSetting>(next.Period);
			token.ThrowIfCancellationRequested();
			steps = loaded;
			Language = settings.Language;
			mode = periodSettings.Voice;
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
		try
		{
			await Task.WhenAll(SpeakAsync(Phrases.Praise(Language), token), Task.Delay(TimeSpan.FromMilliseconds(900), time, token));
			token.ThrowIfCancellationRequested();
			index++;
			IsCelebrating = false;
			await ShowFrameAsync(token);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		finally
		{
			if (!token.IsCancellationRequested) IsCelebrating = false;
		}
	}

	async Task ShowFrameAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		HasStep = index < steps.Count;
		IsDimmed = !HasStep && period?.Period == Period.Bedtime;
		Title = HasStep ? steps[index].Title : Phrases.Ending(period?.Period ?? Period.AfterSchool, Language);
		Image = HasStep ? steps[index].Image : period?.Period switch
		{
			Period.Mealtime => "meal.png",
			Period.Bedtime => "sleep.png",
			_ => "play.png"
		};
		Progress = HasStep ? $"{index + 1} / {steps.Count}" : "";
		await SpeakAsync(Title, token);
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
		DoneCommand.NotifyCanExecuteChanged();
	}

	public async Task ResumeAsync()
	{
		active = true;
		if (reload && period is { } current) await SwitchPeriodAsync(current);
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
