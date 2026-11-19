using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Media;
using NextUpKids.Common.Messages;
using NextUpKids.Common.Services;
using NextUpKids.Features.AutoScheduler;
using NextUpKids.Features.ChildPlayer;
using NextUpKids.Features.ParentAccess;
using NextUpKids.Features.WeeklyPlanner;

namespace NextUpKids;

public partial class AppShell : Shell
{
	readonly ChildRoutineViewModel child;
	readonly ParentDashboardViewModel planner;
	readonly ParentGateViewModel gate;
	readonly ScheduleMonitorService scheduler;
	readonly IMessenger messenger;
	readonly IVoiceService voice;
	readonly IMediaPicker media;
	readonly EventPlannerViewModel events;
	ParentDashboardPage? dashboard;
	bool openingParents;
	bool foreground;

	public AppShell(ChildRoutineViewModel child, ParentDashboardViewModel planner, ParentGateViewModel gate,
		ScheduleMonitorService scheduler, IMessenger messenger, IVoiceService voice, IMediaPicker media, EventPlannerViewModel events)
	{
		InitializeComponent();
		this.child = child;
		this.planner = planner;
		this.gate = gate;
		this.scheduler = scheduler;
		this.messenger = messenger;
		this.voice = voice;
		this.media = media;
		this.events = events;
		ChildContent.Content = new ChildRoutinePage(child);
		messenger.Register<ParentGateRequestedMessage>(this, (r, m) => _ = ((AppShell)r).OpenParentsAsync());
	}

	public async Task ActivateAsync()
	{
		foreground = true;
		if (dashboard is not null || openingParents) return;
		try
		{
			child.Suspend();
			await scheduler.StartAsync();
			if (foreground && dashboard is null && !openingParents) await child.ResumeAsync();
		}
		catch (Exception) { child.Error = "Programme indisponible. Ouvrez l'espace parents pour réessayer."; }
	}

	public void Suspend()
	{
		foreground = false;
		scheduler.Stop();
		child.Suspend();
		voice.Cancel();
	}

	async Task OpenParentsAsync()
	{
		if (!foreground || openingParents || dashboard is not null) return;
		openingParents = true;
		scheduler.Stop();
		child.Suspend();
		try
		{
			var result = await this.ShowPopupAsync<bool>(new ParentGatePopup(gate),
				new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false });
			if (!result.WasDismissedByTappingOutsideOfPopup && result.Result && foreground)
			{
				await planner.LoadAsync();
				if (!foreground) return;
				dashboard = new ParentDashboardPage(planner, media, events);
				dashboard.CloseRequested += OnClosePlanner;
				await Navigation.PushModalAsync(dashboard);
			}
		}
		catch (Exception)
		{
			dashboard = null;
			child.Error = "Impossible d'ouvrir l'espace parents.";
		}
		finally
		{
			openingParents = false;
			if (dashboard is null && foreground) await ActivateAsync();
		}
	}

	async void OnClosePlanner(object? sender, EventArgs e)
	{
		if (dashboard is null) return;
		voice.Cancel();
		try { await Navigation.PopModalAsync(); }
		catch (Exception) { planner.Error = "Impossible de fermer cette page. Réessayez."; }
	}

	public async void OnModalPopped(object? sender, ModalPoppedEventArgs e)
	{
		if (e.Modal != dashboard) return;
		if (dashboard is not null) dashboard.CloseRequested -= OnClosePlanner;
		dashboard = null;
		voice.Cancel();
		if (foreground) await ActivateAsync();
	}

	public void Detach()
	{
		Suspend();
		messenger.UnregisterAll(this);
	}
}
