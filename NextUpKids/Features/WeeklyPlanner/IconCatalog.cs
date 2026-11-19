namespace NextUpKids.Features.WeeklyPlanner;

public sealed record IconChoice(string Title, string Category, string Image);
public sealed record DayChoice(string Title, DayOfWeek Day);

public static class IconCatalog
{
	public static IReadOnlyList<IconChoice> All { get; } =
	[
		new("Dents", "Hygiène", "toothbrush.png"),
		new("Douche", "Hygiène", "shower.png"),
		new("Vêtements", "Hygiène", "dress.png"),
		new("Toilettes", "Hygiène", "toilet.png"),
		new("Chaussures", "Vie quotidienne", "shoes.png"),
		new("Hockey", "Sports", "hockey.png"),
		new("Patinage", "Sports", "skating.png"),
		new("Natation", "Sports", "swimming.png"),
		new("Devoirs", "Vie quotidienne", "homework.png"),
		new("Jeux", "Vie quotidienne", "play.png"),
		new("Repas", "Vie quotidienne", "meal.png"),
		new("Sommeil", "Vie quotidienne", "sleep.png")
	];
}
