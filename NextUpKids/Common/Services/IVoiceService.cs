using NextUpKids.Common.Database;

namespace NextUpKids.Common.Services;

public interface IVoiceService
{
	Task<bool> SpeakAsync(string text, AppLanguage language, VoiceMode mode, CancellationToken cancellationToken = default);
	void Cancel();
}
