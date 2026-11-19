using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Time.Testing;
using NextUpKids.Common.Database;
using NextUpKids.Features.WeeklyPlanner;

namespace NextUpKids.Tests.Features.WeeklyPlanner;

public class EventPlannerViewModelTests
{
	[Fact]
	public async Task Parent_builds_an_activity_and_places_it_on_several_weeks()
	{
		await using var scope = new TestDatabase();
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero));
		var vm = new EventPlannerViewModel(scope.Database, new WeakReferenceMessenger(), clock);
		await vm.LoadAsync();
		vm.Routines.Select(r => r.Title).Should().Equal("Aller dehors", "Coucher", "Hockey", "Piscine", "Temps de jeu");

		vm.ChooseRoutineIconCommand.Execute(IconCatalog.All.First(i => i.Image == "swimming.png"));
		vm.RoutineTitle = "Piscine du samedi";
		await vm.SaveRoutineCommand.ExecuteAsync(null);
		vm.SelectedRoutine!.Title.Should().Be("Piscine du samedi");
		vm.ChooseStepIconCommand.Execute(IconCatalog.All.First(i => i.Image == "shoes.png"));
		IconCatalog.Timers[vm.NewStepTimerIndex].Seconds.Should().Be(120, "the icon presets a sensible timer");
		await vm.AddStepCommand.ExecuteAsync(null);
		vm.NewStepTitle = "Mettre son maillot";
		await vm.AddStepCommand.ExecuteAsync(null);
		await vm.MoveStepUpCommand.ExecuteAsync(vm.Steps[1]);
		vm.Steps.Select(s => (s.Title, s.Order)).Should().Equal(("Mettre son maillot", 0), ("Mettre ses chaussures", 1));
		vm.Steps.Select(s => s.Seconds).Should().Equal(0, 120);
		await vm.CycleStepTimerCommand.ExecuteAsync(vm.Steps[0]);
		vm.Steps[0].Seconds.Should().Be(60);

		vm.EventDate = new DateTime(2026, 11, 15);
		vm.EventTime = new TimeSpan(10, 0, 0);
		await vm.ScheduleCommand.ExecuteAsync(null);
		vm.Error.Should().NotBeEmpty();

		vm.EventDate = new DateTime(2026, 11, 21);
		vm.RepeatWeeks = 3;
		await vm.ScheduleCommand.ExecuteAsync(null);
		vm.Error.Should().BeEmpty();
		var db = await scope.Database.ConnectionAsync();
		(await db.Table<CalendarEvent>().OrderBy(e => e.Start).ToListAsync()).Select(e => e.Start).Should().Equal(
			new DateTime(2026, 11, 21, 10, 0, 0), new DateTime(2026, 11, 28, 10, 0, 0), new DateTime(2026, 12, 5, 10, 0, 0));
		vm.Upcoming.Should().HaveCount(3);

		await vm.DeleteEventCommand.ExecuteAsync(vm.Upcoming[0]);
		vm.Upcoming.Should().HaveCount(2);
		await vm.DeleteRoutineCommand.ExecuteAsync(null);
		(await db.Table<CalendarEvent>().CountAsync()).Should().Be(0);
		(await db.Table<EventStep>().Where(s => s.RoutineId == 6).CountAsync()).Should().Be(0);
		vm.Routines.Should().HaveCount(5);
	}

	[Fact]
	public async Task Parent_prices_a_reward_and_adjusts_the_child_stars()
	{
		await using var scope = new TestDatabase();
		var vm = new EventPlannerViewModel(scope.Database, new WeakReferenceMessenger(), new FakeTimeProvider());
		await vm.LoadAsync();
		vm.SelectedRoutine = vm.Routines.First(r => r.Title == "Temps de jeu");
		vm.RoutineCost.Should().Be(10);
		vm.RoutineCost = 20;
		await vm.SaveRoutineCommand.ExecuteAsync(null);
		var db = await scope.Database.ConnectionAsync();
		(await db.Table<EventRoutine>().FirstAsync(r => r.Title == "Temps de jeu")).Cost.Should().Be(20);

		await vm.GiveStarCommand.ExecuteAsync(null);
		await vm.GiveStarCommand.ExecuteAsync(null);
		vm.Stars.Should().Be(2);
		(await db.FindAsync<AppSettings>(1)).Stars.Should().Be(2);
		await vm.ResetStarsCommand.ExecuteAsync(null);
		vm.Stars.Should().Be(0);
	}
}
