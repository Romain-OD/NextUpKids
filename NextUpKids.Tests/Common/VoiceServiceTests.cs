using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Media;
using NextUpKids.Common.Database;
using NextUpKids.Common.Services;
using NSubstitute;

namespace NextUpKids.Tests.Common;

public class VoiceServiceTests
{
	[Fact]
	public async Task New_speech_cancels_the_old_utterance_and_uses_the_requested_locale()
	{
		var engine = Substitute.For<ITextToSpeech>();
		var french = new Locale("fr", "CA", "Français", "fr-CA");
		var english = new Locale("en", "CA", "English", "en-CA");
		engine.GetLocalesAsync().Returns(new[] { french, english });
		var started = new TaskCompletionSource();
		CancellationToken firstToken = default;
		engine.SpeakAsync("ancien", Arg.Any<SpeechOptions>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			firstToken = call.Arg<CancellationToken>();
			started.SetResult();
			return Task.Delay(Timeout.Infinite, firstToken);
		});
		var voice = new VoiceService(engine, NullLogger<VoiceService>.Instance);
		var first = voice.SpeakAsync("ancien", AppLanguage.French, VoiceMode.On, TestContext.Current.CancellationToken);
		await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		(await voice.SpeakAsync("new", AppLanguage.English, VoiceMode.On, TestContext.Current.CancellationToken)).Should().BeTrue();
		await first;
		firstToken.IsCancellationRequested.Should().BeTrue();
		await engine.Received().SpeakAsync("ancien", Arg.Is<SpeechOptions>(s => s.Locale == french), Arg.Any<CancellationToken>());
		await engine.Received().SpeakAsync("new", Arg.Is<SpeechOptions>(s => s.Locale == english), Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData(VoiceMode.Off, 0)]
	[InlineData(VoiceMode.Soft, 1)]
	public async Task Voice_modes_respect_mute_and_soft_volume(VoiceMode mode, int expectedCalls)
	{
		var engine = Substitute.For<ITextToSpeech>();
		engine.GetLocalesAsync().Returns(Array.Empty<Locale>());
		var voice = new VoiceService(engine, NullLogger<VoiceService>.Instance);
		(await voice.SpeakAsync("Bonsoir", AppLanguage.French, mode, TestContext.Current.CancellationToken)).Should().BeTrue();
		await engine.Received(expectedCalls).SpeakAsync("Bonsoir", Arg.Any<SpeechOptions>(), Arg.Any<CancellationToken>());
		if (mode == VoiceMode.Soft)
			await engine.Received().SpeakAsync("Bonsoir",
				Arg.Is<SpeechOptions>(s => s.Locale == null && s.Volume == 0.3f && s.Rate < 1), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Cancel_stops_active_speech_and_an_unavailable_engine_is_nonfatal()
	{
		var engine = Substitute.For<ITextToSpeech>();
		engine.GetLocalesAsync().Returns(Array.Empty<Locale>());
		var started = new TaskCompletionSource();
		engine.SpeakAsync(Arg.Any<string>(), Arg.Any<SpeechOptions>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			started.TrySetResult();
			return Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
		});
		var voice = new VoiceService(engine, NullLogger<VoiceService>.Instance);
		var speech = voice.SpeakAsync("texte", AppLanguage.French, VoiceMode.On, TestContext.Current.CancellationToken);
		await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		voice.Cancel();
		(await speech.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().BeTrue();
		engine.SpeakAsync(Arg.Any<string>(), Arg.Any<SpeechOptions>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new InvalidOperationException("Engine unavailable")));
		(await voice.SpeakAsync("texte", AppLanguage.French, VoiceMode.On, TestContext.Current.CancellationToken)).Should().BeFalse();
	}

	[Fact]
	public async Task Muting_cancels_a_pending_locale_lookup_and_does_not_queue_stale_speech()
	{
		var engine = Substitute.For<ITextToSpeech>();
		var locales = new TaskCompletionSource<IEnumerable<Locale>>();
		engine.GetLocalesAsync().Returns(locales.Task);
		var voice = new VoiceService(engine, NullLogger<VoiceService>.Instance);
		var stale = voice.SpeakAsync("ancien", AppLanguage.French, VoiceMode.On, TestContext.Current.CancellationToken);
		await voice.SpeakAsync("muet", AppLanguage.French, VoiceMode.Off, TestContext.Current.CancellationToken);
		locales.SetResult(Array.Empty<Locale>());
		await stale.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		await engine.DidNotReceive().SpeakAsync(Arg.Any<string>(), Arg.Any<SpeechOptions>(), Arg.Any<CancellationToken>());
	}
}
