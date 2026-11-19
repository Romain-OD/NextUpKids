using AwesomeAssertions;
using NextUpKids.Common.Database;

namespace NextUpKids.Tests.Common;

public class AppDatabaseTests
{
	[Fact]
	public async Task Activities_are_seeded_once_with_their_subtasks()
	{
		await using var scope = new TestDatabase();
		var connection = await scope.Database.ConnectionAsync();
		var hockey = await connection.Table<EventRoutine>().FirstAsync(r => r.Title == "Hockey");
		(await connection.Table<EventStep>().Where(s => s.RoutineId == hockey.Id).OrderBy(s => s.Order).ToListAsync())
			.Select(s => s.Title).Should().Equal("S'habiller", "Aller aux toilettes", "Mettre ses chaussures");
		(await connection.Table<EventRoutine>().CountAsync()).Should().Be(3);
		await connection.DeleteAllAsync<EventRoutine>();

		await using var reopened = new AppDatabase(scope.Path);
		(await (await reopened.ConnectionAsync()).Table<EventRoutine>().CountAsync()).Should().Be(0);
	}

	[Fact]
	public async Task Seed_is_persistent_and_not_recreated_after_deletion()
	{
		await using var scope = new TestDatabase();
		var connections = await Task.WhenAll(scope.Database.ConnectionAsync(), scope.Database.ConnectionAsync());
		var connection = connections[0];
		var periods = await connection.Table<PeriodSetting>().OrderBy(p => p.Period).ToListAsync();
		periods.Select(p => p.StartTime).Should().Equal(
			new TimeSpan(7, 0, 0), new TimeSpan(16, 30, 0), new TimeSpan(18, 0, 0), new TimeSpan(19, 30, 0));
		periods[3].Voice.Should().Be(VoiceMode.Soft);
		(await connection.FindAsync<AppSettings>(1)).Language.Should().Be(AppLanguage.French);
		var steps = await connection.Table<RoutineStep>().ToListAsync();
		steps.Select(s => s.Day).Distinct().Should().HaveCount(7);
		steps.Should().Contain(s => s.Title == "Se brosser les dents");
		await connection.DeleteAllAsync<RoutineStep>();

		await using var reopened = new AppDatabase(scope.Path);
		(await (await reopened.ConnectionAsync()).Table<RoutineStep>().CountAsync()).Should().Be(0);
		(await connection.Table<PeriodSetting>().CountAsync()).Should().Be(4);
	}
}
