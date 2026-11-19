using System.ComponentModel;
using CommunityToolkit.Maui.Alerts;

namespace NextUpKids.Features.ChildPlayer;

public partial class ChildRoutinePage : ContentPage
{
	readonly ChildRoutineViewModel viewModel;

	public ChildRoutinePage(ChildRoutineViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		BindingContext = viewModel;
		ParentHold.BindingContext = viewModel;
		// A short tap gets a hint; only the 3-second hold (LongPressCommand) opens the gate.
		ParentHold.Command = new Command(() => Toast.Make("Parents : maintenir 3 secondes").Show());
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		viewModel.PropertyChanged += OnFrameChanged;
	}

	protected override void OnDisappearing()
	{
		viewModel.PropertyChanged -= OnFrameChanged;
		FrameContent.CancelAnimations();
		base.OnDisappearing();
	}

	async void OnFrameChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ChildRoutineViewModel.Title)) return;
		FrameContent.Opacity = 0.4;
		await FrameContent.FadeToAsync(1, 220);
	}
}
