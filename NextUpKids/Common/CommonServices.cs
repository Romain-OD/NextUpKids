using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Media;
using NextUpKids.Common.Database;
using NextUpKids.Common.Services;

namespace NextUpKids.Common;

public static class CommonServices
{
	public static IServiceCollection AddCommon(this IServiceCollection services) => services
		.AddSingleton(TimeProvider.System)
		.AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
		.AddSingleton(_ => new AppDatabase(Path.Combine(FileSystem.Current.AppDataDirectory, "nextupkids.db3")))
		.AddSingleton<ITextToSpeech>(TextToSpeech.Default)
		.AddSingleton<IVoiceService, VoiceService>();
}
