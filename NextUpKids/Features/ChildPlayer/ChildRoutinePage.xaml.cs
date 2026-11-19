using System.ComponentModel;
using CommunityToolkit.Maui.Alerts;

namespace NextUpKids.Features.ChildPlayer;

public partial class ChildRoutinePage : ContentPage
{
	readonly ChildRoutineViewModel viewModel;
	IDispatcherTimer? ticker;
	int shownStars;

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
		Buddy.IsSleeping = viewModel.IsDimmed;
		Buddy.Show(viewModel.HasStep ? viewModel.Image : null);
		UpdateStage();
		shownStars = viewModel.Stars;
		viewModel.PropertyChanged += OnFrameChanged;
		ticker ??= Dispatcher.CreateTimer();
		ticker.Interval = TimeSpan.FromMilliseconds(250);
		ticker.Tick += OnTick;
		ticker.Start();
	}

	protected override void OnDisappearing()
	{
		viewModel.PropertyChanged -= OnFrameChanged;
		if (ticker is not null)
		{
			ticker.Stop();
			ticker.Tick -= OnTick;
		}
		StarBadge.CancelAnimations();
		FrameContent.CancelAnimations();
		StepCard.CancelAnimations();
		Stage.CancelAnimations();
		base.OnDisappearing();
	}

	void OnTick(object? sender, EventArgs e) => viewModel.Tick();

	async void OnFrameChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ChildRoutineViewModel.Stars))
		{
			var earned = viewModel.Stars > shownStars;
			shownStars = viewModel.Stars;
			if (earned && !Motion.IsReduced)
			{
				StarBadge.Scale = 1.5;
				await StarBadge.ScaleToAsync(1, 450, Easing.SpringOut);
			}
			return;
		}
		if (e.PropertyName == nameof(ChildRoutineViewModel.IsCelebrating) && viewModel.IsCelebrating)
		{
			try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* no vibrator */ }
			Buddy.Cheer();
			if (!Motion.IsReduced) Sparkles.Burst();
			return;
		}
		if (e.PropertyName == nameof(ChildRoutineViewModel.IsDimmed)) Buddy.IsSleeping = viewModel.IsDimmed;
		if (e.PropertyName != nameof(ChildRoutineViewModel.Title)) return;
		if (viewModel.HasStep) Buddy.Show(viewModel.Image);
		else Buddy.Cheer();
		UpdateStage();
		FrameContent.Opacity = 0.4;
		if (Motion.IsReduced)
		{
			await FrameContent.FadeToAsync(1, 150);
			return;
		}
		View pop = StepCard.IsVisible ? StepCard : Stage;
		pop.Scale = 0.85;
		await Task.WhenAll(FrameContent.FadeToAsync(1, 180), pop.ScaleToAsync(1, 350, Easing.SpringOut));
	}

	// The picture card only shows for steps Filou cannot act out (e.g. a photo); the timer then moves aside.
	void UpdateStage()
	{
		var card = viewModel.HasStep && !FoxBuddyView.CanAct(viewModel.Image);
		StepCard.IsVisible = card;
		Grid.SetColumn(Timer, card ? 1 : 0);
		Grid.SetColumnSpan(Timer, card ? 1 : 2);
	}
}
