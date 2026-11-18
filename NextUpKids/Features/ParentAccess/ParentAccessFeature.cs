using Microsoft.Extensions.DependencyInjection;

namespace NextUpKids.Features.ParentAccess;

public static class ParentAccessFeature
{
	public static IServiceCollection AddParentAccessFeature(this IServiceCollection services) => services
		.AddSingleton<ParentGateViewModel>()
		.AddSingleton(Random.Shared);
}
