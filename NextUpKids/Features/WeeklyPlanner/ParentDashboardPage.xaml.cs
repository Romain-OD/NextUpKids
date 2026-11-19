using Microsoft.Maui.Media;
using NextUpKids.Common.Database;

namespace NextUpKids.Features.WeeklyPlanner;

public partial class ParentDashboardPage : ContentPage
{
	readonly ParentDashboardViewModel viewModel;
	readonly IMediaPicker mediaPicker;
	readonly EventPlannerViewModel events;
	bool syncing;
	public event EventHandler? CloseRequested;

	public ParentDashboardPage(ParentDashboardViewModel viewModel, IMediaPicker mediaPicker, EventPlannerViewModel events)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		this.mediaPicker = mediaPicker;
		this.events = events;
		BindingContext = viewModel;
		viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(ParentDashboardViewModel.SelectedStep)) Sync(() => StepsList.SelectedItem = viewModel.SelectedStep);
		};
	}

	void Sync(Action update)
	{
		syncing = true;
		try { update(); }
		finally { syncing = false; }
	}

	Task<bool> ConfirmDiscardAsync() => viewModel.HasUnsavedChanges
		? DisplayAlertAsync("Abandonner les modifications ?", "L'étape en cours n'est pas enregistrée.", "Abandonner", "Continuer")
		: Task.FromResult(true);

	async void OnSelectionChanged(object? sender, EventArgs e)
	{
		if (syncing || DayPicker.SelectedIndex < 0 || PeriodPicker.SelectedIndex < 0) return;
		var day = ParentDashboardViewModel.Days[DayPicker.SelectedIndex].Day;
		var period = (Period)PeriodPicker.SelectedIndex;
		if (day == viewModel.Day && period == viewModel.Period) return;
		if (!await ConfirmDiscardAsync())
		{
			Sync(() =>
			{
				DayPicker.SelectedIndex = ((int)viewModel.Day + 6) % 7;
				PeriodPicker.SelectedIndex = (int)viewModel.Period;
			});
			return;
		}
		await viewModel.SelectAsync(day, period);
	}

	async void OnStepSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (syncing || viewModel.IsBusy || e.CurrentSelection.FirstOrDefault() is not RoutineStep step || step == viewModel.SelectedStep) return;
		if (await ConfirmDiscardAsync()) viewModel.SelectedStep = step;
		else Sync(() => StepsList.SelectedItem = viewModel.SelectedStep);
	}

	async void OnNewStep(object? sender, EventArgs e)
	{
		if (!await ConfirmDiscardAsync()) return;
		viewModel.NewStepCommand.Execute(null);
		TitleEntry.Focus();
	}

	// Enter on the keyboard or the button: save, then stay ready for the next step.
	async void OnSave(object? sender, EventArgs e)
	{
		var adding = !viewModel.IsEditing;
		await viewModel.SaveStepCommand.ExecuteAsync(null);
		if (viewModel.TitleError.Length > 0) TitleEntry.Focus();
		if (!adding || viewModel.TitleError.Length > 0 || viewModel.Error.Length > 0) return;
		if (viewModel.Steps.LastOrDefault() is { } added) StepsList.ScrollTo(added, position: ScrollToPosition.End);
		TitleEntry.Focus();
	}

	void OnIconSelected(object? sender, SelectionChangedEventArgs e)
	{
		// Selection also mirrors the current image; only a real tap on another icon applies it.
		if (e.CurrentSelection.FirstOrDefault() is IconChoice icon && icon != viewModel.SelectedIcon)
			viewModel.ChooseIconCommand.Execute(icon);
	}

	async void OnPickPhoto(object? sender, EventArgs e)
	{
		try
		{
			var photos = await mediaPicker.PickPhotosAsync(new MediaPickerOptions
			{
				SelectionLimit = 1, MaximumWidth = 1600, MaximumHeight = 1600, Title = "Illustration de l'étape"
			});
			if (photos.FirstOrDefault() is not { } photo) return;
			await using var stream = await photo.OpenReadAsync();
			await viewModel.ImportPhotoAsync(stream, photo.FileName);
		}
		catch (OperationCanceledException) { }
		catch (Exception) { viewModel.Error = "Impossible d'importer la photo. Vérifiez l'accès à la photothèque."; }
	}

	async void OnDelete(object? sender, EventArgs e)
	{
		if (viewModel.SelectedStep is not null &&
			await DisplayAlertAsync("Supprimer l'étape ?", viewModel.SelectedStep.Title, "Supprimer", "Annuler"))
			await viewModel.DeleteStepCommand.ExecuteAsync(null);
	}

	async void OnDuplicate(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync("Remplacer les journées sélectionnées ?",
			"Les quatre périodes enregistrées du jour affiché remplaceront celles des jours sélectionnés.", "Copier", "Annuler"))
			await viewModel.DuplicateDayCommand.ExecuteAsync(null);
	}

	async void OnOpenEvents(object? sender, EventArgs e)
	{
		if (!viewModel.IsBusy && await ConfirmDiscardAsync()) await Navigation.PushModalAsync(new EventPlannerPage(events));
	}

	async void OnClose(object? sender, EventArgs e)
	{
		if (!viewModel.IsBusy && await ConfirmDiscardAsync()) CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	protected override bool OnBackButtonPressed()
	{
		OnClose(this, EventArgs.Empty);
		return true;
	}
}
