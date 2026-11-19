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
		foreach (var view in StepViews)
		{
			view.Opacity = 1;
			view.TranslationX = 0;
		}
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
		foreach (var view in StepViews) view.CancelAnimations();
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
		if (e.PropertyName == nameof(ChildRoutineViewModel.IsCelebrating))
		{
			if (!viewModel.IsCelebrating)
			{
				// Fires just before the next step is set, so the new title and timer glide in.
				SlideStepIn();
				return;
			}
			try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* no vibrator */ }
			Buddy.Cheer();
			if (!Motion.IsReduced) Sparkles.Burst();
			SlideStepOut();
			return;
		}
		if (e.PropertyName == nameof(ChildRoutineViewModel.IsDimmed)) Buddy.IsSleeping = viewModel.IsDimmed;
		if (e.PropertyName != nameof(ChildRoutineViewModel.Title)) return;
		if (viewModel.HasStep) Buddy.Show(viewModel.Image);
		else Buddy.Cheer();
		UpdateStage();
		if (Motion.IsReduced) return;
		View pop = StepCard.IsVisible ? StepCard : Stage;
		pop.Scale = 0.94;
		await pop.ScaleToAsync(1, 420, Easing.SpringOut);
	}

	// The step text and timer leave while Filou celebrates, then the next step slides in from the right;
	// Filou and the buttons stay put so the screen never flashes.
	View[] StepViews => [StepTitle, StepInfo];

	void SlideStepOut()
	{
		foreach (var view in StepViews)
		{
			_ = view.FadeToAsync(0, 200, Easing.CubicIn);
			if (!Motion.IsReduced) _ = view.TranslateToAsync(-48, 0, 200, Easing.CubicIn);
		}
	}

	void SlideStepIn()
	{
		foreach (var view in StepViews)
		{
			view.CancelAnimations();
			if (!Motion.IsReduced)
			{
				view.TranslationX = 48;
				_ = view.TranslateToAsync(0, 0, 320, Easing.CubicOut);
			}
			else view.TranslationX = 0;
			_ = view.FadeToAsync(1, 260, Easing.CubicOut);
		}
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
