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
}

public sealed class AppSettings
{
	[PrimaryKey]
	public int Id { get; set; } = 1;
	public AppLanguage Language { get; set; } = AppLanguage.French;
}
