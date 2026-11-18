using System.Runtime.Versioning;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NextUpKids.Common;
using NextUpKids.Features.AutoScheduler;
using NextUpKids.Features.ChildPlayer;
using NextUpKids.Features.ParentAccess;
using NextUpKids.Features.WeeklyPlanner;

namespace NextUpKids;

public static class MauiProgram
{
	[SupportedOSPlatform("android24.0")]
	[SupportedOSPlatform("ios15.0")]
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("Nunito.ttf", "Nunito");
			});

		builder.Services
			.AddCommon()
			.AddChildPlayerFeature()
			.AddAutoSchedulerFeature()
			.AddWeeklyPlannerFeature()
			.AddParentAccessFeature();
		builder.Services.AddSingleton<Microsoft.Maui.Media.IMediaPicker>(Microsoft.Maui.Media.MediaPicker.Default);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
