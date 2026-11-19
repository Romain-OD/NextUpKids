namespace NextUpKids.Features.WeeklyPlanner;

public partial class EventPlannerPage : ContentPage
{
	readonly EventPlannerViewModel viewModel;

	public EventPlannerPage(EventPlannerViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await viewModel.LoadAsync();
	}

	async void OnAddStep(object? sender, EventArgs e) => await viewModel.AddStepCommand.ExecuteAsync(null);

	void OnRoutineIconSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is IconChoice icon && icon != viewModel.RoutineIcon)
			viewModel.ChooseRoutineIconCommand.Execute(icon);
	}

	void OnStepIconSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is IconChoice icon && icon != viewModel.StepIcon)
			viewModel.ChooseStepIconCommand.Execute(icon);
	}

	async void OnDeleteRoutine(object? sender, EventArgs e)
	{
		if (viewModel.SelectedRoutine is { } routine &&
			await DisplayAlertAsync("Supprimer l'activité ?", $"« {routine.Title} », ses étapes et ses dates prévues seront supprimées.", "Supprimer", "Annuler"))
			await viewModel.DeleteRoutineCommand.ExecuteAsync(null);
	}

	async void OnClose(object? sender, EventArgs e)
	{
		if (!viewModel.IsBusy) await Navigation.PopModalAsync();
	}

	protected override bool OnBackButtonPressed()
	{
		OnClose(this, EventArgs.Empty);
		return true;
	}
}
