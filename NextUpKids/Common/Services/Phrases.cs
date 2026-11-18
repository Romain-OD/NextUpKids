using NextUpKids.Common.Database;

namespace NextUpKids.Common.Services;

public static class Phrases
{
	public static string Praise(AppLanguage language) => language == AppLanguage.French ? "Bravo !" : "Great job!";
	public static string Done(AppLanguage language) => language == AppLanguage.French ? "Terminé !" : "Done!";
	public static string Ending(Period period, AppLanguage language) => (period, language) switch
	{
		(Period.BeforeSchool, AppLanguage.French) => "Bravo ! Tu es prêt pour l'école !",
		(Period.AfterSchool, AppLanguage.French) => "Bravo ! C'est l'heure de jouer !",
		(Period.Mealtime, AppLanguage.French) => "C'est l'heure de manger !",
		(Period.Bedtime, AppLanguage.French) => "Bonne nuit",
		(Period.BeforeSchool, _) => "Great job! Ready for school!",
		(Period.AfterSchool, _) => "Great job! Free play time!",
		(Period.Mealtime, _) => "It's mealtime!",
		_ => "Good night"
	};
}
