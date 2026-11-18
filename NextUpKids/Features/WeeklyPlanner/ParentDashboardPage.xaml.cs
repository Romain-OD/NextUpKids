using Microsoft.Maui.Media;
using NextUpKids.Common.Database;

namespace NextUpKids.Features.WeeklyPlanner;

public partial class ParentDashboardPage : ContentPage
{
	readonly ParentDashboardViewModel viewModel;
	readonly IMediaPicker mediaPicker;
	public event EventHandler? CloseRequested;

	public ParentDashboardPage(ParentDashboardViewModel viewModel, IMediaPicker mediaPicker)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		this.mediaPicker = mediaPicker;
		BindingContext = viewModel;
	}

	async void OnSelectionChanged(object? sender, EventArgs e)
	{
		if (BindingContext is not ParentDashboardViewModel vm || DayPicker.SelectedIndex < 0 || PeriodPicker.SelectedIndex < 0) return;
		await vm.SelectAsync(ParentDashboardViewModel.Days[DayPicker.SelectedIndex].Day, (Period)PeriodPicker.SelectedIndex);
	}

	void OnIconSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is IconChoice icon)
			viewModel.ChooseIconCommand.Execute(icon);
		if (sender is CollectionView gallery) gallery.SelectedItem = null;
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

	void OnClose(object? sender, EventArgs e)
	{
		if (!viewModel.IsBusy) CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	protected override bool OnBackButtonPressed()
	{
		OnClose(this, EventArgs.Empty);
		return true;
	}
}
