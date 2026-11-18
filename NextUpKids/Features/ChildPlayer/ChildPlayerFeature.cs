using Microsoft.Extensions.DependencyInjection;

namespace NextUpKids.Features.ChildPlayer;

public static class ChildPlayerFeature
{
	public static IServiceCollection AddChildPlayerFeature(this IServiceCollection services) => services
		.AddSingleton<ChildRoutineViewModel>()
		.AddSingleton<ChildRoutinePage>();
}
