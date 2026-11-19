using SQLite;

namespace NextUpKids.Common.Database;

public sealed class AppDatabase(string path) : IAsyncDisposable
{
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
			await connection.CreateTableAsync<AppSettings>().ConfigureAwait(false);
			await connection.CreateTableAsync<PeriodSetting>().ConfigureAwait(false);
			await connection.CreateTableAsync<RoutineStep>().ConfigureAwait(false);
			// Seeded once, also for installs that predate activities; deleting them later must not bring them back.
			var newActivities = await connection.CreateTableAsync<EventRoutine>().ConfigureAwait(false) == CreateTableResult.Created;
			await connection.CreateTableAsync<EventStep>().ConfigureAwait(false);
			await connection.CreateTableAsync<CalendarEvent>().ConfigureAwait(false);
			if (newActivities)
				await connection.RunInTransactionAsync(db =>
				{
					Add("Hockey", "hockey.png", ("S'habiller", "dress.png"), ("Aller aux toilettes", "toilet.png"), ("Mettre ses chaussures", "shoes.png"));
					Add("Coucher", "sleep.png", ("Aller aux toilettes", "toilet.png"), ("Aller au lit", "sleep.png"));
					Add("Piscine", "swimming.png", ("Mettre son maillot de bain", "swimming.png"), ("Aller aux toilettes", "toilet.png"), ("Mettre ses chaussures", "shoes.png"));

					void Add(string title, string image, params (string Title, string Image)[] steps)
					{
						var routine = new EventRoutine { Title = title, Image = image };
						db.Insert(routine);
						for (var i = 0; i < steps.Length; i++)
							db.Insert(new EventStep { RoutineId = routine.Id, Order = i, Title = steps[i].Title, Image = steps[i].Image });
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
						db.Insert(new RoutineStep { Day = day, Period = period, Order = i, Title = steps[i].Title, Image = steps[i].Image });
				}
			}).ConfigureAwait(false);
			initialized = true;
			return connection;
		}
		finally { initialization.Release(); }
	}

	public async ValueTask DisposeAsync()
	{
		await connection.CloseAsync().ConfigureAwait(false);
		initialization.Dispose();
	}
}
