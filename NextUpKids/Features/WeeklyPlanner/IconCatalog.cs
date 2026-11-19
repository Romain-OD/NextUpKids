namespace NextUpKids.Features.WeeklyPlanner;

// Step is the sentence read to the child when the icon pre-fills an empty step title.
public sealed record IconChoice(string Title, string Category, string Image, string? Step = null)
{
	public string Phrase => Step ?? Title;
}

public sealed record DayChoice(string Title, DayOfWeek Day);
public sealed record TimerChoice(string Title, int Seconds);

public static class IconCatalog
{
	public static IReadOnlyList<IconChoice> All { get; } =
	[
		new("Dents", "Hygiène", "toothbrush.png", "Se brosser les dents"),
		new("Douche", "Hygiène", "shower.png", "Prendre une douche"),
		new("Mains", "Hygiène", "handwash.png", "Se laver les mains"),
		new("Toilettes", "Hygiène", "toilet.png", "Aller aux toilettes"),
		new("Coiffer", "Hygiène", "comb.png", "Se coiffer"),
		new("Vêtements", "Habits", "dress.png", "S'habiller"),
		new("Dehors", "Habits", "coat.png", "S'habiller pour aller dehors"),
		new("Chaussures", "Habits", "shoes.png", "Mettre ses chaussures"),
		new("Pyjama", "Habits", "pajamas.png", "Mettre son pyjama"),
		new("Hockey", "Sports", "hockey.png"),
		new("Patinage", "Sports", "skating.png"),
		new("Natation", "Sports", "swimming.png"),
		new("Cartable", "Vie quotidienne", "backpack.png", "Préparer son sac"),
		new("Devoirs", "Vie quotidienne", "homework.png", "Faire les devoirs"),
		new("Ranger", "Vie quotidienne", "tidy.png", "Ranger ses jouets"),
		new("Histoire", "Vie quotidienne", "book.png", "Lire une histoire"),
		new("Jeux", "Vie quotidienne", "play.png", "Jouer"),
		new("Repas", "Vie quotidienne", "meal.png", "Passer à table"),
		new("Sommeil", "Vie quotidienne", "sleep.png", "Aller au lit")
	];

	public static IReadOnlyList<TimerChoice> Timers { get; } =
	[
		new("Sans minuteur", 0), new("1 min", 60), new("2 min", 120), new("3 min", 180), new("5 min", 300),
		new("10 min", 600), new("15 min", 900), new("20 min", 1200), new("30 min", 1800)
	];

	public static int TimerIndex(int seconds)
	{
		for (var i = Timers.Count - 1; i > 0; i--)
			if (seconds >= Timers[i].Seconds) return i;
		return 0;
	}

	public static string TimerText(int seconds) => seconds > 0 ? $"⏱ {Timers[TimerIndex(seconds)].Title}" : "";
}