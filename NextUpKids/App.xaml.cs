using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Media;
using NextUpKids.Common.Services;
using NextUpKids.Features.AutoScheduler;
using NextUpKids.Features.ChildPlayer;
using NextUpKids.Features.ParentAccess;
using NextUpKids.Features.WeeklyPlanner;

namespace NextUpKids;

public partial class App : Application
{
	readonly ChildRoutineViewModel child;
	readonly ParentDashboardViewModel planner;
	readonly ParentGateViewModel gate;
	readonly ScheduleMonitorService scheduler;
	readonly IMessenger messenger;
	readonly IVoiceService voice;
	readonly IMediaPicker media;

	public App(ChildRoutineViewModel child, ParentDashboardViewModel planner, ParentGateViewModel gate,
		ScheduleMonitorService scheduler, IMessenger messenger, IVoiceService voice, IMediaPicker media)
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Light;
		this.child = child;
		this.planner = planner;
		this.gate = gate;
		this.scheduler = scheduler;
		this.messenger = messenger;
		this.voice = voice;
		this.media = media;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = new AppShell(child, planner, gate, scheduler, messenger, voice, media);
		var window = new Window(shell);
		window.Activated += async (sender, args) =>
		{
			DeviceDisplay.Current.KeepScreenOn = true;
			await shell.ActivateAsync();
		};
		window.Stopped += (sender, args) =>
		{
			shell.Suspend();
			DeviceDisplay.Current.KeepScreenOn = false;
		};
		window.ModalPopped += shell.OnModalPopped;
		window.Destroying += (sender, args) =>
		{
			window.ModalPopped -= shell.OnModalPopped;
			shell.Detach();
		};
		return window;
	}
}
