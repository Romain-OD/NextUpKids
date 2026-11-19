using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Time.Testing;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;
using NextUpKids.Common.Services;
using NextUpKids.Features.ChildPlayer;
using NSubstitute;

namespace NextUpKids.Tests.Features.ChildPlayer;

public class ChildRoutineViewModelTests
{
	static IVoiceService Voice()
	{
		var voice = Substitute.For<IVoiceService>();
		voice.SpeakAsync(Arg.Any<string>(), Arg.Any<AppLanguage>(), Arg.Any<VoiceMode>(), Arg.Any<CancellationToken>()).Returns(true);
		return voice;
	}

	[Fact]
	public async Task Calendar_event_plays_its_activity_subtasks_then_a_go_message()
	{
		await using var scope = new TestDatabase();
		var voice = Voice();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, new FakeTimeProvider(), new WeakReferenceMessenger());
		var db = await scope.Database.ConnectionAsync();
		var bed = await db.Table<EventRoutine>().FirstAsync(r => r.Title == "Coucher");
		var calendarEvent = new CalendarEvent { RoutineId = bed.Id, Start = new DateTime(2026, 11, 16, 20, 0, 0) };
		await db.InsertAsync(calendarEvent);

		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.AfterSchool, calendarEvent.Id));
		vm.Title.Should().Be("Aller aux toilettes");
		vm.Progress.Should().Be("1 / 2");
		await voice.Received().SpeakAsync("Aller aux toilettes", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
		var celebrating = vm.DoneCommand.ExecuteAsync(null);
		await vm.DoneCommand.ExecuteAsync(null);
		await celebrating;
		vm.Title.Should().Be("Aller au lit");
		await db.DeleteAsync(calendarEvent);
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.AfterSchool, calendarEvent.Id));
		vm.HasStep.Should().BeFalse();
	}

	[Fact]
	public async Task Tap_during_celebration_skips_it_and_advances_exactly_one_step()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider();
		var voice = Voice();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		vm.Title.Should().Be("S'habiller");
		vm.Progress.Should().Be("1 / 3");
		voice.ClearReceivedCalls();
		var done = vm.DoneCommand.ExecuteAsync(null);
		vm.IsCelebrating.Should().BeTrue();
		await vm.DoneCommand.ExecuteAsync(null);
		voice.Received(2).Cancel();
		vm.IsCelebrating.Should().BeFalse();
		vm.Title.Should().Be("Prendre le petit déjeuner");
		vm.Progress.Should().Be("2 / 3");
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		vm.Progress.Should().Be("2 / 3");
		vm.IsCelebrating.Should().BeFalse();
		await vm.ReplayCommand.ExecuteAsync(null);
		await voice.Received(2).SpeakAsync("Prendre le petit déjeuner", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Next_step_appears_after_the_cheer_and_its_title_is_spoken_once_the_praise_ends()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider();
		var voice = Voice();
		var praise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		voice.SpeakAsync("Bravo !", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>()).Returns(praise.Task);
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		clock.Advance(TimeSpan.FromHours(1));
		var done = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await Task.Delay(50, TestContext.Current.CancellationToken);

		vm.Title.Should().Be("Prendre le petit déjeuner");
		vm.IsCelebrating.Should().BeFalse();
		await voice.DidNotReceive().SpeakAsync("Prendre le petit déjeuner", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
		praise.SetResult(true);
		await done.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		await voice.Received(1).SpeakAsync("Prendre le petit déjeuner", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Done_remains_available_during_the_next_instruction_and_cancels_it()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider();
		var voice = Voice();
		var speaking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		CancellationToken instructionToken = default;
		voice.SpeakAsync("Prendre le petit déjeuner", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>())
			.Returns(async call =>
			{
				instructionToken = call.Arg<CancellationToken>();
				speaking.TrySetResult();
				try { await Task.Delay(Timeout.Infinite, instructionToken); }
				catch (OperationCanceledException) when (instructionToken.IsCancellationRequested) { }
				return true;
			});
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		var first = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await speaking.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		vm.DoneCommand.CanExecute(null).Should().BeTrue();
		var second = vm.DoneCommand.ExecuteAsync(null);
		instructionToken.IsCancellationRequested.Should().BeTrue();
		await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		vm.IsCelebrating.Should().BeTrue();
		clock.Advance(TimeSpan.FromSeconds(1));
		await second;
		vm.Title.Should().Be("Se brosser les dents");
		vm.Progress.Should().Be("3 / 3");
	}

	[Theory]
	[InlineData(Period.BeforeSchool, AppLanguage.French, "Bravo ! Tu es prêt pour l'école !", false)]
	[InlineData(Period.AfterSchool, AppLanguage.English, "Great job! Free play time!", false)]
	[InlineData(Period.Mealtime, AppLanguage.English, "It's mealtime!", false)]
	[InlineData(Period.Bedtime, AppLanguage.English, "Good night", true)]
	public async Task Last_step_shows_and_speaks_the_period_ending(Period period, AppLanguage language, string ending, bool dimmed)
	{
		await using var scope = new TestDatabase();
		var db = await scope.Database.ConnectionAsync();
		await db.DeleteAllAsync<RoutineStep>();
		await db.InsertAsync(new RoutineStep { Day = DayOfWeek.Monday, Period = period, Title = "Une étape" });
		await db.UpdateAsync(new AppSettings { Language = language });
		var voice = Voice();
		var clock = new FakeTimeProvider();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), period));
		var done = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		vm.Title.Should().Be(ending);
		vm.HasStep.Should().BeFalse();
		vm.DoneCommand.CanExecute(null).Should().BeFalse();
		vm.IsDimmed.Should().Be(dimmed);
		await voice.Received().SpeakAsync(ending, language, dimmed ? VoiceMode.Soft : VoiceMode.On, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task New_period_cancels_an_old_celebration_and_empty_routines_finish_safely()
	{
		await using var scope = new TestDatabase();
		var voice = Voice();
		var clock = new FakeTimeProvider();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		var old = vm.DoneCommand.ExecuteAsync(null);
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.Mealtime));
		clock.Advance(TimeSpan.FromSeconds(2));
		await old;
		vm.Title.Should().Be("Passer à table");
		vm.Progress.Should().Be("1 / 1");
		await (await scope.Database.ConnectionAsync()).DeleteAllAsync<RoutineStep>();
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.Bedtime));
		vm.Title.Should().Be("Bonne nuit");
		vm.HasStep.Should().BeFalse();
	}

	[Fact]
	public async Task Messages_switch_periods_but_suspended_player_does_not_speak()
	{
		await using var scope = new TestDatabase();
		var voice = Voice();
		var messenger = new WeakReferenceMessenger();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, new FakeTimeProvider(), messenger);
		var gate = new List<ParentGateRequestedMessage>();
		messenger.Register<ParentGateRequestedMessage>(gate, (r, m) => ((List<ParentGateRequestedMessage>)r).Add(m));
		vm.RequestParentAccessCommand.Execute(null);
		gate.Should().HaveCount(1);
		vm.Suspend();
		messenger.Send(new PeriodStartedMessage(new DateOnly(2026, 11, 16), Period.Mealtime));
		await voice.DidNotReceive().SpeakAsync(Arg.Any<string>(), Arg.Any<AppLanguage>(), Arg.Any<VoiceMode>(), Arg.Any<CancellationToken>());
		await vm.ResumeAsync();
		vm.Title.Should().Be("Passer à table");
		await voice.Received(1).SpeakAsync("Passer à table", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Suspending_during_loading_can_resume_without_staying_busy()
	{
		await using var scope = new TestDatabase();
		var voice = Voice();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, new FakeTimeProvider(), new WeakReferenceMessenger());
		var suspendOnce = true;
		vm.PropertyChanged += (sender, e) =>
		{
			if (suspendOnce && e.PropertyName == nameof(vm.IsLoading) && vm.IsLoading)
			{
				suspendOnce = false;
				vm.Suspend();
			}
		};
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.Mealtime));
		vm.IsLoading.Should().BeFalse();
		await vm.ResumeAsync();
		vm.Title.Should().Be("Passer à table");
		vm.DoneCommand.CanExecute(null).Should().BeTrue();
		await voice.Received(1).SpeakAsync("Passer à table", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData(AppLanguage.French, "Voix indisponible sur cette tablette.")]
	[InlineData(AppLanguage.English, "Voice is unavailable on this tablet.")]
	public async Task Unavailable_voice_keeps_the_visual_routine_usable(AppLanguage language, string warning)
	{
		await using var scope = new TestDatabase();
		await (await scope.Database.ConnectionAsync()).UpdateAsync(new AppSettings { Language = language });
		var voice = Substitute.For<IVoiceService>();
		voice.SpeakAsync(Arg.Any<string>(), Arg.Any<AppLanguage>(), Arg.Any<VoiceMode>(), Arg.Any<CancellationToken>()).Returns(false);
		var clock = new FakeTimeProvider();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		vm.Error.Should().Be(warning);
		vm.Title.Should().Be("S'habiller");
		vm.DoneCommand.CanExecute(null).Should().BeTrue();
		var done = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		vm.Title.Should().Be("Prendre le petit déjeuner");
		vm.Progress.Should().Be("2 / 3");
		vm.DoneCommand.CanExecute(null).Should().BeTrue();
	}

	[Fact]
	public async Task Finishing_a_timed_step_before_the_timer_ends_earns_a_star()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider();
		var voice = Voice();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.BeforeSchool));
		vm.HasTimer.Should().BeTrue();
		vm.TimerText.Should().Be("3:00");
		clock.Advance(TimeSpan.FromSeconds(60));
		vm.Tick();
		vm.TimerText.Should().Be("2:00");
		vm.TimerFraction.Should().BeApproximately(2 / 3d, 0.001);

		var done = vm.DoneCommand.ExecuteAsync(null);
		vm.Stars.Should().Be(1);
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		await voice.Received().SpeakAsync("Bravo ! Une étoile !", AppLanguage.French, VoiceMode.On, Arg.Any<CancellationToken>());
		var db = await scope.Database.ConnectionAsync();
		(await db.FindAsync<AppSettings>(1)).Stars.Should().Be(1);

		vm.HasTimer.Should().BeFalse("breakfast has no timer");
		done = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		vm.Title.Should().Be("Se brosser les dents");
		clock.Advance(TimeSpan.FromSeconds(121));
		vm.Tick();
		vm.TimerText.Should().Be("0:00");
		vm.TimerFraction.Should().Be(0);
		done = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		vm.Stars.Should().Be(1, "a late step and a step without timer earn nothing");
		(await db.FindAsync<AppSettings>(1)).Stars.Should().Be(1);
	}

	[Fact]
	public async Task Stars_unlock_play_time_at_the_end_of_a_routine()
	{
		await using var scope = new TestDatabase();
		var db = await scope.Database.ConnectionAsync();
		await db.DeleteAllAsync<RoutineStep>();
		await db.ExecuteAsync("UPDATE AppSettings SET Stars = 5 WHERE Id = 1");
		var clock = new FakeTimeProvider();
		var voice = Voice();
		using var vm = new ChildRoutineViewModel(scope.Database, voice, clock, new WeakReferenceMessenger());
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.AfterSchool));
		vm.ShowRewards.Should().BeTrue();
		vm.Rewards.Should().ContainSingle().Which.Affordable.Should().BeFalse();
		await vm.RedeemCommand.ExecuteAsync(vm.Rewards[0]);
		vm.Stars.Should().Be(5, "the child cannot spend stars they do not have");
		vm.HasStep.Should().BeFalse();

		await db.ExecuteAsync("UPDATE AppSettings SET Stars = 12 WHERE Id = 1");
		await vm.SwitchPeriodAsync(new(new DateOnly(2026, 11, 16), Period.AfterSchool));
		vm.Rewards[0].Affordable.Should().BeTrue();
		await vm.RedeemCommand.ExecuteAsync(vm.Rewards[0]);
		vm.Stars.Should().Be(2);
		(await db.FindAsync<AppSettings>(1)).Stars.Should().Be(2);
		vm.Title.Should().Be("Jouer");
		vm.TimerText.Should().Be("15:00");
		vm.ShowRewards.Should().BeFalse();

		var done = vm.DoneCommand.ExecuteAsync(null);
		clock.Advance(TimeSpan.FromSeconds(1));
		await done;
		vm.Stars.Should().Be(2, "play time itself earns no star");
		vm.Title.Should().Be("Temps de jeu terminé !");
		vm.ShowRewards.Should().BeFalse();
	}
}