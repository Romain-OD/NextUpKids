using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;

namespace NextUpKids.Features.WeeklyPlanner;

public sealed record UpcomingEvent(int Id, string Text, string Image);

// Activities ("Hockey") with their own subtasks, placed on specific dates because no two weeks are alike.
public partial class EventPlannerViewModel(AppDatabase database, IMessenger messenger, TimeProvider time) : ObservableObject
{
	const string DefaultImage = "play.png";
	static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
	readonly SemaphoreSlim operations = new(1);
	string autoTitle = "";

	public static IReadOnlyList<IconChoice> Icons => IconCatalog.All;
	public static IReadOnlyList<TimerChoice> Timers => IconCatalog.Timers;
	public bool HasRoutine => SelectedRoutine is not null;
	public bool CanEdit => !IsBusy;
	public string RoutineEditorTitle => HasRoutine ? "Modifier l'activité" : "Nouvelle activité";
	public IconChoice? RoutineIcon => IconCatalog.All.FirstOrDefault(i => i.Image == RoutineImage);
	public IconChoice? StepIcon => IconCatalog.All.FirstOrDefault(i => i.Image == NewStepImage);

	[ObservableProperty] public partial ObservableCollection<EventRoutine> Routines { get; set; } = [];
	[ObservableProperty, NotifyPropertyChangedFor(nameof(HasRoutine), nameof(RoutineEditorTitle))]
	public partial EventRoutine? SelectedRoutine { get; set; }
	[ObservableProperty] public partial string RoutineTitle { get; set; } = "";
	[ObservableProperty, NotifyPropertyChangedFor(nameof(RoutineIcon))]
	public partial string RoutineImage { get; set; } = DefaultImage;
	[ObservableProperty, NotifyPropertyChangedFor(nameof(IsReward))] public partial int RoutineCost { get; set; }
	public bool IsReward => RoutineCost > 0;
	[ObservableProperty] public partial int Stars { get; set; }
	[ObservableProperty] public partial ObservableCollection<EventStep> Steps { get; set; } = [];
	[ObservableProperty] public partial string NewStepTitle { get; set; } = "";
	[ObservableProperty, NotifyPropertyChangedFor(nameof(StepIcon))]
	public partial string NewStepImage { get; set; } = DefaultImage;
	[ObservableProperty] public partial int NewStepTimerIndex { get; set; }
	[ObservableProperty] public partial ObservableCollection<UpcomingEvent> Upcoming { get; set; } = [];
	[ObservableProperty] public partial DateTime EventDate { get; set; }
	[ObservableProperty] public partial TimeSpan EventTime { get; set; } = new(17, 0, 0);
	[ObservableProperty] public partial int RepeatWeeks { get; set; } = 1;
	[ObservableProperty] public partial string Error { get; set; } = "";
	[ObservableProperty] public partial string Status { get; set; } = "";
	[ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))]
	public partial bool IsBusy { get; set; }

	public DateTime Today => time.GetLocalNow().Date;

	partial void OnSelectedRoutineChanged(EventRoutine? value)
	{
		RoutineTitle = value?.Title ?? "";
		RoutineImage = value?.Image ?? DefaultImage;
		RoutineCost = value?.Cost ?? 0;
		// Reloads triggered inside an operation are awaited there instead.
		if (!IsBusy) _ = RunAsync(ReloadStepsAsync);
	}

	public Task LoadAsync() => RunAsync(async () =>
	{
		if (EventDate < Today) EventDate = Today;
		await ReloadAsync(SelectedRoutine?.Id);
	});

	async Task ReloadAsync(int? selectId)
	{
		var db = await database.ConnectionAsync();
		Routines = new(await db.Table<EventRoutine>().OrderBy(r => r.Title).ToListAsync());
		SelectedRoutine = Routines.FirstOrDefault(r => r.Id == selectId);
		Stars = (await db.FindAsync<AppSettings>(1)).Stars;
		await ReloadStepsAsync();
		var from = Today;
		var titles = Routines.ToDictionary(r => r.Id);
		Upcoming = new((await db.Table<CalendarEvent>().Where(e => e.Start >= from).OrderBy(e => e.Start).Take(100).ToListAsync())
			.Where(e => titles.ContainsKey(e.RoutineId))
			.Select(e => new UpcomingEvent(e.Id, $"{e.Start.ToString("ddd d MMM · HH:mm", French)}  {titles[e.RoutineId].Title}", titles[e.RoutineId].Image)));
	}

	async Task ReloadStepsAsync()
	{
		var routineId = SelectedRoutine?.Id ?? -1;
		var db = await database.ConnectionAsync();
		Steps = new(await db.Table<EventStep>().Where(s => s.RoutineId == routineId).OrderBy(s => s.Order).ToListAsync());
	}

	[RelayCommand]
	void NewRoutine()
	{
		SelectedRoutine = null;
		RoutineTitle = "";
		RoutineImage = DefaultImage;
		RoutineCost = 0;
		Error = Status = "";
	}

	[RelayCommand]
	Task SaveRoutineAsync() => RunAsync(async () =>
	{
		var title = RoutineTitle.Trim();
		if (title.Length is < 1 or > 60)
		{
			Error = "Le nom de l'activité doit contenir entre 1 et 60 caractères.";
			return;
		}
		if (RoutineCost is < 0 or > 100)
		{
			Error = "Le coût d'une récompense va de 0 à 100 étoiles.";
			return;
		}
		var routine = new EventRoutine { Id = SelectedRoutine?.Id ?? 0, Title = title, Image = RoutineImage, Cost = RoutineCost };
		var db = await database.ConnectionAsync();
		if (routine.Id == 0) await db.InsertAsync(routine);
		else await db.UpdateAsync(routine);
		await ReloadAsync(routine.Id);
		Status = SelectedRoutine is not null && Steps.Count == 0 ? $"« {title} » créée. Ajoutez ses étapes." : $"« {title} » enregistrée.";
		messenger.Send(new RoutineChangedMessage());
	});

	[RelayCommand]
	Task DeleteRoutineAsync() => RunAsync(async () =>
	{
		if (SelectedRoutine is not { } routine) return;
		var db = await database.ConnectionAsync();
		await db.RunInTransactionAsync(connection =>
		{
			connection.Execute("DELETE FROM EventStep WHERE RoutineId = ?", routine.Id);
			connection.Execute("DELETE FROM CalendarEvent WHERE RoutineId = ?", routine.Id);
			connection.Delete<EventRoutine>(routine.Id);
		});
		await ReloadAsync(null);
		Status = $"« {routine.Title} » supprimée.";
		messenger.Send(new RoutineChangedMessage());
	});

	[RelayCommand]
	void ChooseRoutineIcon(IconChoice? icon)
	{
		if (icon is null || !IconCatalog.All.Contains(icon)) return;
		RoutineImage = icon.Image;
		if (RoutineTitle.Trim().Length == 0) RoutineTitle = icon.Title;
	}

	[RelayCommand]
	void ChooseStepIcon(IconChoice? icon)
	{
		if (icon is null || !IconCatalog.All.Contains(icon)) return;
		NewStepImage = icon.Image;
		if (NewStepTitle.Trim().Length == 0 || NewStepTitle == autoTitle)
		{
			NewStepTitle = autoTitle = icon.Phrase;
			NewStepTimerIndex = IconCatalog.TimerIndex(AppDatabase.DefaultSeconds(icon.Image));
		}
	}

	[RelayCommand]
	Task AddStepAsync() => RunAsync(async () =>
	{
		if (SelectedRoutine is not { } routine)
		{
			Error = "Enregistrez d'abord l'activité.";
			return;
		}
		var title = NewStepTitle.Trim();
		if (title.Length is < 1 or > 140)
		{
			Error = "L'étape doit contenir entre 1 et 140 caractères.";
			return;
		}
		var db = await database.ConnectionAsync();
		var seconds = IconCatalog.Timers[Math.Clamp(NewStepTimerIndex, 0, IconCatalog.Timers.Count - 1)].Seconds;
		await db.InsertAsync(new EventStep { RoutineId = routine.Id, Order = Steps.Count, Title = title, Image = NewStepImage, Seconds = seconds });
		await ReloadStepsAsync();
		NewStepTitle = autoTitle = "";
		NewStepImage = DefaultImage;
		NewStepTimerIndex = 0;
		Status = $"« {title} » ajoutée.";
		messenger.Send(new RoutineChangedMessage());
	});

	[RelayCommand]
	Task RemoveStepAsync(EventStep? step) => SaveOrderAsync(step, items => items.RemoveAll(s => s.Id == step!.Id), $"« {step?.Title} » supprimée.");

	[RelayCommand]
	Task MoveStepUpAsync(EventStep? step) => SaveOrderAsync(step, items =>
	{
		var index = items.FindIndex(s => s.Id == step!.Id);
		if (index > 0) (items[index - 1], items[index]) = (items[index], items[index - 1]);
	}, "Ordre enregistré.");

	// Each tap moves the step to the next timer duration, wrapping back to no timer.
	[RelayCommand]
	Task CycleStepTimerAsync(EventStep? step) => SaveOrderAsync(step, items =>
	{
		var item = items.First(s => s.Id == step!.Id);
		item.Seconds = IconCatalog.Timers[(IconCatalog.TimerIndex(item.Seconds) + 1) % IconCatalog.Timers.Count].Seconds;
	}, "Minuteur enregistré.");

	[RelayCommand] Task GiveStarAsync() => SetStarsAsync(1);
	[RelayCommand] Task ResetStarsAsync() => SetStarsAsync(null);

	Task SetStarsAsync(int? add) => RunAsync(async () =>
	{
		var db = await database.ConnectionAsync();
		await db.ExecuteAsync(add is { } n ? "UPDATE AppSettings SET Stars = COALESCE(Stars, 0) + ? WHERE Id = 1" : "UPDATE AppSettings SET Stars = 0 WHERE Id = 1", add ?? 0);
		Stars = (await db.FindAsync<AppSettings>(1)).Stars;
		messenger.Send(new RoutineChangedMessage());
	});

	Task SaveOrderAsync(EventStep? step, Action<List<EventStep>> change, string status) => RunAsync(async () =>
	{
		if (step is null || Steps.All(s => s.Id != step.Id)) return;
		var items = Steps.ToList();
		change(items);
		var db = await database.ConnectionAsync();
		await db.RunInTransactionAsync(connection =>
		{
			connection.Execute("DELETE FROM EventStep WHERE RoutineId = ?", step.RoutineId);
			for (var i = 0; i < items.Count; i++)
				connection.Insert(new EventStep { RoutineId = step.RoutineId, Order = i, Title = items[i].Title, Image = items[i].Image, Seconds = items[i].Seconds });
		});
		await ReloadStepsAsync();
		Status = status;
		messenger.Send(new RoutineChangedMessage());
	});

	[RelayCommand]
	Task ScheduleAsync() => RunAsync(async () =>
	{
		if (SelectedRoutine is not { } routine)
		{
			Error = "Choisissez une activité.";
			return;
		}
		if (Steps.Count == 0)
		{
			Error = "Ajoutez au moins une étape à l'activité.";
			return;
		}
		if (RepeatWeeks is < 1 or > 52)
		{
			Error = "Répétez entre 1 et 52 semaines.";
			return;
		}
		var start = EventDate.Date + TimeSpan.FromMinutes(Math.Floor(EventTime.TotalMinutes));
		if (EventTime < TimeSpan.Zero || EventTime >= TimeSpan.FromDays(1) || start < time.GetLocalNow().DateTime.AddMinutes(-1))
		{
			Error = "Choisissez une date et une heure à venir.";
			return;
		}
		var db = await database.ConnectionAsync();
		await db.InsertAllAsync(Enumerable.Range(0, RepeatWeeks).Select(w => new CalendarEvent { RoutineId = routine.Id, Start = start.AddDays(7 * w) }));
		await ReloadAsync(routine.Id);
		Status = RepeatWeeks == 1
			? $"« {routine.Title} » prévue le {start.ToString("dddd d MMMM à HH:mm", French)}."
			: $"« {routine.Title} » prévue {RepeatWeeks} semaines à partir du {start.ToString("dddd d MMMM à HH:mm", French)}.";
		messenger.Send(new RoutineChangedMessage());
	});

	[RelayCommand]
	Task DeleteEventAsync(UpcomingEvent? item) => RunAsync(async () =>
	{
		if (item is null) return;
		var db = await database.ConnectionAsync();
		await db.DeleteAsync<CalendarEvent>(item.Id);
		await ReloadAsync(SelectedRoutine?.Id);
		Status = "Événement retiré du calendrier.";
		messenger.Send(new RoutineChangedMessage());
	});

	async Task RunAsync(Func<Task> action)
	{
		await operations.WaitAsync();
		try
		{
			IsBusy = true;
			Error = "";
			Status = "";
			await action();
		}
		catch (Exception) { Error = "Opération impossible. Vos données enregistrées sont conservées."; }
		finally
		{
			IsBusy = false;
			operations.Release();
		}
	}
}
