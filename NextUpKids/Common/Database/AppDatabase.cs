using SQLite;

namespace NextUpKids.Common.Database;

public sealed class AppDatabase(string path) : IAsyncDisposable
{
	// Suggested countdown per illustration, in seconds; other steps have no timer.
	static readonly Dictionary<string, int> TimerPresets = new()
	{
		["toothbrush.png"] = 120, ["shower.png"] = 300, ["dress.png"] = 180, ["coat.png"] = 180, ["pajamas.png"] = 180,
		["toilet.png"] = 120, ["shoes.png"] = 120, ["handwash.png"] = 60, ["comb.png"] = 60, ["backpack.png"] = 120,
		["tidy.png"] = 300, ["swimming.png"] = 180
	};

	public static int DefaultSeconds(string image) => TimerPresets.GetValueOrDefault(image);

	readonly SQLiteAsyncConnection connection = new(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
	readonly SemaphoreSlim initialization = new(1);
	bool initialized;

	public async Task<SQLiteAsyncConnection> ConnectionAsync()
	{
		await initialization.WaitAsync().ConfigureAwait(false);
		try
		{
			if (initialized)
				return connection;

			await connection.EnableWriteAheadLoggingAsync().ConfigureAwait(false);
			// Columns added in 1.4: existing rows get the timer presets and the new reward/outdoor activities.
			var addTimers = await MissingColumnAsync("RoutineStep", "Seconds").ConfigureAwait(false);
			var addEventTimers = await MissingColumnAsync("EventStep", "Seconds").ConfigureAwait(false);
			var addRewards = await MissingColumnAsync("EventRoutine", "Cost").ConfigureAwait(false);
			await connection.CreateTableAsync<AppSettings>().ConfigureAwait(false);
			await connection.CreateTableAsync<PeriodSetting>().ConfigureAwait(false);
			await connection.CreateTableAsync<RoutineStep>().ConfigureAwait(false);
			// Seeded once, also for installs that predate activities; deleting them later must not bring them back.
			var newActivities = await connection.CreateTableAsync<EventRoutine>().ConfigureAwait(false) == CreateTableResult.Created;
			await connection.CreateTableAsync<EventStep>().ConfigureAwait(false);
			await connection.CreateTableAsync<CalendarEvent>().ConfigureAwait(false);
			if (newActivities || addRewards || addTimers || addEventTimers)
				await connection.RunInTransactionAsync(db =>
				{
					foreach (var (image, seconds) in TimerPresets)
					{
						if (addTimers) db.Execute("UPDATE RoutineStep SET Seconds = ? WHERE Image = ?", seconds, image);
						if (addEventTimers) db.Execute("UPDATE EventStep SET Seconds = ? WHERE Image = ?", seconds, image);
					}
					if (newActivities)
					{
						Add("Hockey", "hockey.png", 0, ("S'habiller", "dress.png"), ("Aller aux toilettes", "toilet.png"), ("Mettre ses chaussures", "shoes.png"));
						Add("Coucher", "sleep.png", 0, ("Aller aux toilettes", "toilet.png"), ("Aller au lit", "sleep.png"));
						Add("Piscine", "swimming.png", 0, ("Mettre son maillot de bain", "swimming.png"), ("Aller aux toilettes", "toilet.png"), ("Mettre ses chaussures", "shoes.png"));
					}
					if (newActivities || addRewards)
					{
						Add("Aller dehors", "coat.png", 0, ("Aller aux toilettes", "toilet.png"), ("S'habiller pour aller dehors", "coat.png"), ("Mettre ses chaussures", "shoes.png"));
						var play = Add("Temps de jeu", "play.png", 10, ("Jouer", "play.png"));
						db.Execute("UPDATE EventStep SET Seconds = ? WHERE RoutineId = ?", 15 * 60, play);
					}

					int Add(string title, string image, int cost, params (string Title, string Image)[] steps)
					{
						var routine = new EventRoutine { Title = title, Image = image, Cost = cost };
						db.Insert(routine);
						for (var i = 0; i < steps.Length; i++)
							db.Insert(new EventStep { RoutineId = routine.Id, Order = i, Title = steps[i].Title, Image = steps[i].Image, Seconds = DefaultSeconds(steps[i].Image) });
						return routine.Id;
					}
				}).ConfigureAwait(false);
			await connection.RunInTransactionAsync(db =>
			{
				if (db.Find<AppSettings>(1) is not null)
					return;
				db.Insert(new AppSettings());
				foreach (var period in Enum.GetValues<Period>())
					db.Insert(new PeriodSetting
					{
						Period = period,
						StartTime = period switch
						{
							Period.BeforeSchool => new(7, 0, 0),
							Period.AfterSchool => new(16, 30, 0),
							Period.Mealtime => new(18, 0, 0),
							_ => new(19, 30, 0)
						},
						Voice = period == Period.Bedtime ? VoiceMode.Soft : VoiceMode.On
					});
				foreach (var day in Enum.GetValues<DayOfWeek>())
				{
					Add(day, Period.BeforeSchool, ("S'habiller", "dress.png"), ("Prendre le petit déjeuner", "meal.png"), ("Se brosser les dents", "toothbrush.png"));
					Add(day, Period.AfterSchool, ("Faire les devoirs", "homework.png"), ("Jouer", "play.png"));
					Add(day, Period.Mealtime, ("Passer à table", "meal.png"));
					Add(day, Period.Bedtime, ("Prendre une douche", "shower.png"), ("Se brosser les dents", "toothbrush.png"), ("Se coucher", "sleep.png"));
				}

				void Add(DayOfWeek day, Period period, params (string Title, string Image)[] steps)
				{
					for (var i = 0; i < steps.Length; i++)
						db.Insert(new RoutineStep { Day = day, Period = period, Order = i, Title = steps[i].Title, Image = steps[i].Image, Seconds = DefaultSeconds(steps[i].Image) });
				}
			}).ConfigureAwait(false);
			initialized = true;
			return connection;
		}
		finally { initialization.Release(); }
	}

	async Task<bool> MissingColumnAsync(string table, string column)
	{
		var columns = await connection.GetTableInfoAsync(table).ConfigureAwait(false);
		return columns.Count > 0 && columns.All(c => c.Name != column);
	}

	public async ValueTask DisposeAsync()
	{
		await connection.CloseAsync().ConfigureAwait(false);
		initialization.Dispose();
	}
}
