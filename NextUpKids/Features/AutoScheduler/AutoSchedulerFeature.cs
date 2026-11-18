using Microsoft.Extensions.DependencyInjection;

namespace NextUpKids.Features.AutoScheduler;

public static class AutoSchedulerFeature
{
	public static IServiceCollection AddAutoSchedulerFeature(this IServiceCollection services) => services
		.AddSingleton<ScheduleMonitorService>();
}
