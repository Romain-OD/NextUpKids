using Microsoft.Extensions.Logging;
using Microsoft.Maui.Media;
using NextUpKids.Common.Database;

namespace NextUpKids.Common.Services;

public sealed class VoiceService(ITextToSpeech engine, ILogger<VoiceService> logger) : IVoiceService
{
	readonly object sync = new();
	readonly SemaphoreSlim speech = new(1);
	CancellationTokenSource? active;
	IEnumerable<Locale>? locales;

	public void Cancel()
	{
		lock (sync) active?.Cancel();
	}

	public async Task<bool> SpeakAsync(string text, AppLanguage language, VoiceMode mode, CancellationToken cancellationToken = default)
	{
		using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		lock (sync)
		{
			active?.Cancel();
			active = request;
		}
		var entered = false;
		try
		{
			if (mode == VoiceMode.Off || string.IsNullOrWhiteSpace(text))
				return true;
			await speech.WaitAsync(request.Token);
			entered = true;
			locales ??= await engine.GetLocalesAsync().WaitAsync(request.Token);
			request.Token.ThrowIfCancellationRequested();
			var code = language == AppLanguage.French ? "fr" : "en";
			var locale = locales.FirstOrDefault(l => l.Language.Split('-')[0].Equals(code, StringComparison.OrdinalIgnoreCase));
			await engine.SpeakAsync(text, new SpeechOptions
			{
				Locale = locale,
				Volume = mode == VoiceMode.Soft ? 0.3f : 1,
				Rate = mode == VoiceMode.Soft ? 0.8f : 1,
				Pitch = mode == VoiceMode.Soft ? 0.9f : 1
			}, request.Token);
			return true;
		}
		catch (OperationCanceledException) when (request.IsCancellationRequested) { return true; }
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Speech is unavailable");
			return false;
		}
		finally
		{
			if (entered) speech.Release();
			lock (sync)
			{
				if (ReferenceEquals(active, request)) active = null;
			}
		}
	}
}
