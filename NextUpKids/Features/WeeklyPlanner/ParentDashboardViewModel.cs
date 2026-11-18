using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;
using NextUpKids.Common.Services;

namespace NextUpKids.Features.WeeklyPlanner;

public partial class ParentDashboardViewModel(AppDatabase database, IVoiceService voice, IMessenger messenger, IFileSystem files) : ObservableObject
{
	readonly SemaphoreSlim operations = new(1);
	DayOfWeek day = DayOfWeek.Monday;
	Period period;
	public static IReadOnlyList<DayChoice> Days { get; } =
	[
		new("Lundi", DayOfWeek.Monday), new("Mardi", DayOfWeek.Tuesday), new("Mercredi", DayOfWeek.Wednesday),
		new("Jeudi", DayOfWeek.Thursday), new("Vendredi", DayOfWeek.Friday), new("Samedi", DayOfWeek.Saturday), new("Dimanche", DayOfWeek.Sunday)
	];
	public static IReadOnlyList<string> PeriodNames { get; } = ["Avant l'école", "Après l'école", "Repas", "Coucher"];
	public static IReadOnlyList<string> VoiceNames { get; } = ["Activée", "Douce", "Désactivée"];
	public static IReadOnlyList<string> LanguageNames { get; } = ["Français", "English"];
	public static IReadOnlyList<string> Categories { get; } = ["Hygiène", "Sports", "Vie quotidienne"];
	public IReadOnlyList<IconChoice> Icons => IconCatalog.All.Where(i => i.Category == Category).ToArray();
	public bool CanEdit => !IsBusy;

	[ObservableProperty] public partial ObservableCollection<RoutineStep> Steps { get; set; } = [];
	[ObservableProperty] public partial RoutineStep? SelectedStep { get; set; }
	[ObservableProperty] public partial string StepTitle { get; set; } = "";
	[ObservableProperty] public partial string StepImage { get; set; } = "play.png";
	[ObservableProperty] public partial string Error { get; set; } = "";
	[ObservableProperty] public partial string Status { get; set; } = "";
	[ObservableProperty] public partial TimeSpan StartTime { get; set; }
	[ObservableProperty] public partial int VoiceIndex { get; set; }
	[ObservableProperty] public partial int LanguageIndex { get; set; }
	[ObservableProperty] public partial IList<object> CopyTargets { get; set; } = new List<object>();
	[ObservableProperty, NotifyPropertyChangedFor(nameof(Icons))]
	public partial string Category { get; set; } = "Hygiène";
	[ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))]
	public partial bool IsBusy { get; set; }

	partial void OnSelectedStepChanged(RoutineStep? value)
	{
		StepTitle = value?.Title ?? "";
		StepImage = value?.Image ?? "play.png";
	}

	public Task LoadAsync() => SelectAsync(DayOfWeek.Monday, Period.BeforeSchool);

	public Task SelectAsync(DayOfWeek selectedDay, Period selectedPeriod) => RunAsync(async () =>
	{
		if (!Enum.IsDefined(selectedDay) || !Enum.IsDefined(selectedPeriod))
		{
			Error = "Jour ou période invalide.";
			return;
		}
		day = selectedDay;
		period = selectedPeriod;
		var db = await database.ConnectionAsync();
		var settings = await db.FindAsync<AppSettings>(1);
		var selected = await db.FindAsync<PeriodSetting>(period);
		LanguageIndex = (int)settings.Language;
		StartTime = selected.StartTime;
		VoiceIndex = (int)selected.Voice;
		await ReloadStepsAsync();
	});

	async Task ReloadStepsAsync()
	{
		var db = await database.ConnectionAsync();
		Steps = new(await db.Table<RoutineStep>().Where(s => s.Day == day && s.Period == period).OrderBy(s => s.Order).ToListAsync());
		NewStep();
	}

	[RelayCommand]
	void NewStep()
	{
		SelectedStep = null;
		StepTitle = "";
		StepImage = "play.png";
	}

	[RelayCommand]
	void ChooseIcon(IconChoice? icon)
	{
		if (icon is not null && IconCatalog.All.Contains(icon)) StepImage = icon.Image;
	}

	[RelayCommand]
	Task SaveStepAsync() => RunAsync(async () =>
	{
		var title = StepTitle.Trim();
		if (title.Length is < 1 or > 140)
		{
			Error = "Le titre doit contenir entre 1 et 140 caractères.";
			return;
		}
		var photos = Path.GetFullPath(Path.Combine(files.AppDataDirectory, "Photos")) + Path.DirectorySeparatorChar;
		if (!IconCatalog.All.Any(i => i.Image == StepImage) &&
			!(Path.IsPathFullyQualified(StepImage) && Path.GetFullPath(StepImage).StartsWith(photos, StringComparison.Ordinal) && File.Exists(StepImage)))
		{
			Error = "Choisissez une illustration ou importez une photo.";
			return;
		}
		var items = CopySteps();
		var existing = items.FindIndex(s => s.Id == SelectedStep?.Id);
		var edited = new RoutineStep { Id = SelectedStep?.Id ?? 0, Title = title, Image = StepImage, Day = day, Period = period };
		if (existing < 0) items.Add(edited);
		else items[existing] = edited;
		await SaveStepsAsync(items);
	});

	[RelayCommand]
	Task DeleteStepAsync() => RunAsync(async () =>
	{
		if (SelectedStep is null) return;
		var items = CopySteps();
		items.RemoveAll(s => s.Id == SelectedStep.Id);
		// ponytail: copies share photo files; collect unreferenced photos only if storage becomes an issue.
		await SaveStepsAsync(items);
	});

	[RelayCommand] Task MoveUpAsync() => MoveAsync(-1);
	[RelayCommand] Task MoveDownAsync() => MoveAsync(1);

	Task MoveAsync(int direction) => RunAsync(async () =>
	{
		var items = CopySteps();
		var index = items.FindIndex(s => s.Id == SelectedStep?.Id);
		var target = index + direction;
		if (index < 0 || target < 0 || target >= items.Count) return;
		var id = items[index].Id;
		(items[index], items[target]) = (items[target], items[index]);
		await SaveStepsAsync(items);
		SelectedStep = Steps.First(s => s.Id == id);
	});

	List<RoutineStep> CopySteps() => Steps.Select(s => new RoutineStep
	{
		Id = s.Id, Day = s.Day, Period = s.Period, Order = s.Order, Title = s.Title, Image = s.Image
	}).ToList();

	async Task SaveStepsAsync(List<RoutineStep> items)
	{
		var db = await database.ConnectionAsync();
		await db.RunInTransactionAsync(connection =>
		{
			connection.Execute("DELETE FROM RoutineStep WHERE Day = ? AND Period = ?", day, period);
			for (var i = 0; i < items.Count; i++)
			{
				items[i].Order = i;
				connection.Insert(items[i]);
			}
		});
		await ReloadStepsAsync();
		Status = "Programme enregistré.";
		messenger.Send(new RoutineChangedMessage());
	}

	[RelayCommand]
	Task DuplicateDayAsync() => RunAsync(async () =>
	{
		var targets = CopyTargets.OfType<DayChoice>().Select(d => d.Day).Where(d => d != day && Enum.IsDefined(d)).Distinct().ToArray();
		if (targets.Length == 0)
		{
			Error = "Sélectionnez au moins un autre jour.";
			return;
		}
		var db = await database.ConnectionAsync();
		await db.RunInTransactionAsync(connection =>
		{
			var source = connection.Table<RoutineStep>().Where(s => s.Day == day).ToList();
			foreach (var target in targets)
			{
				connection.Execute("DELETE FROM RoutineStep WHERE Day = ?", target);
				foreach (var step in source)
					connection.Insert(new RoutineStep { Day = target, Period = step.Period, Order = step.Order, Title = step.Title, Image = step.Image });
			}
		});
		Status = "Journée copiée.";
		messenger.Send(new RoutineChangedMessage());
	});

	[RelayCommand]
	Task SaveSettingsAsync() => RunAsync(async () =>
	{
		if (StartTime < TimeSpan.Zero || StartTime >= TimeSpan.FromDays(1) || StartTime.Ticks % TimeSpan.TicksPerMinute != 0)
		{
			Error = "Choisissez une heure entre 00:00 et 23:59.";
			return;
		}
		if (!ValidVoiceSettings()) return;
		var db = await database.ConnectionAsync();
		if (await db.Table<PeriodSetting>().Where(p => p.Period != period && p.StartTime == StartTime).CountAsync() > 0)
		{
			Error = "Chaque période doit avoir une heure différente.";
			return;
		}
		await db.RunInTransactionAsync(connection =>
		{
			connection.Update(new AppSettings { Language = (AppLanguage)LanguageIndex });
			connection.Update(new PeriodSetting { Period = period, StartTime = StartTime, Voice = (VoiceMode)VoiceIndex });
		});
		Status = "Réglages enregistrés pour tous les jours.";
		messenger.Send(new RoutineChangedMessage());
	});

	bool ValidVoiceSettings()
	{
		if (LanguageIndex is >= 0 and <= 1 && VoiceIndex is >= 0 and <= 2) return true;
		Error = "Choisissez une langue et un mode de voix valides.";
		return false;
	}

	[RelayCommand]
	Task PreviewAsync() => RunAsync(async () =>
	{
		if (!ValidVoiceSettings()) return;
		if (!await voice.SpeakAsync(string.IsNullOrWhiteSpace(StepTitle) ? "Bravo !" : StepTitle,
			(AppLanguage)LanguageIndex, (VoiceMode)VoiceIndex))
			Error = "Voix indisponible sur cette tablette.";
	});

	public Task ImportPhotoAsync(Stream input, string fileName, CancellationToken cancellationToken = default) => RunAsync(async () =>
	{
		cancellationToken.ThrowIfCancellationRequested();
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".heic" or ".heif"))
		{
			Error = "Format photo non pris en charge.";
			return;
		}
		var directory = Path.Combine(files.AppDataDirectory, "Photos");
		Directory.CreateDirectory(directory);
		string? destination = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
		try
		{
			await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
			{
				var buffer = new byte[81920];
				var total = 0;
				int count;
				while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
				{
					total += count;
					if (total > 20 * 1024 * 1024) throw new InvalidDataException("Photo larger than 20 MB.");
					await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
				}
				if (total == 0) throw new InvalidDataException("Empty photo.");
			}
			cancellationToken.ThrowIfCancellationRequested();
			StepImage = destination;
			destination = null;
		}
		finally
		{
			if (destination is not null) File.Delete(destination);
		}
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
		catch (OperationCanceledException) { }
		catch (InvalidDataException) { Error = "La photo doit contenir une image de moins de 20 Mo."; }
		catch (Exception) { Error = "Opération impossible. Vos données enregistrées sont conservées."; }
		finally
		{
			IsBusy = false;
			operations.Release();
		}
	}
}
