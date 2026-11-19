using SQLite;

namespace NextUpKids.Common.Database;

public enum Period { BeforeSchool, AfterSchool, Mealtime, Bedtime }
public enum VoiceMode { On, Soft, Off }
public enum AppLanguage { French, English }

public sealed class PeriodSetting
{
	[PrimaryKey]
	public Period Period { get; set; }
	public TimeSpan StartTime { get; set; }
	public VoiceMode Voice { get; set; }
}

public sealed class RoutineStep
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }
	[Indexed("DayPeriod", 1)]
	public DayOfWeek Day { get; set; }
	[Indexed("DayPeriod", 2)]
	public Period Period { get; set; }
	public int Order { get; set; }
	public string Title { get; set; } = "";
	public string Image { get; set; } = "play.png";
	// Countdown for the step; 0 = no timer. Finishing in time earns a star.
	public int Seconds { get; set; }
}

// A reusable activity ("Hockey") with its own ordered subtasks, played when a dated CalendarEvent starts.
public sealed class EventRoutine
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }
	public string Title { get; set; } = "";
	public string Image { get; set; } = "play.png";
	// Stars the child spends to start it (e.g. play time); 0 = a normal activity.
	public int Cost { get; set; }
}

public sealed class EventStep
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }
	[Indexed]
	public int RoutineId { get; set; }
	public int Order { get; set; }
	public string Title { get; set; } = "";
	public string Image { get; set; } = "play.png";
	public int Seconds { get; set; }
}

public sealed class CalendarEvent
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }
	[Indexed]
	public int RoutineId { get; set; }
	// Local wall-clock time.
	[Indexed]
	public DateTime Start { get; set; }
}

public sealed class AppSettings
{
	[PrimaryKey]
	public int Id { get; set; } = 1;
	public AppLanguage Language { get; set; } = AppLanguage.French;
	public int Stars { get; set; }
}
