using Microsoft.Extensions.DependencyInjection;

namespace NextUpKids.Features.WeeklyPlanner;

public static class WeeklyPlannerFeature
{
	public static IServiceCollection AddWeeklyPlannerFeature(this IServiceCollection services) => services
		.AddSingleton<ParentDashboardViewModel>()
		.AddSingleton<EventPlannerViewModel>()
		.AddSingleton<Microsoft.Maui.Storage.IFileSystem>(Microsoft.Maui.Storage.FileSystem.Current);
}
