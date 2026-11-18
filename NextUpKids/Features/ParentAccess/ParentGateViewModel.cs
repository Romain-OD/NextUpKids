using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NextUpKids.Features.ParentAccess;

public partial class ParentGateViewModel : ObservableObject
{
	readonly Random random;
	int expected;
	[ObservableProperty] public partial string Challenge { get; set; } = "";
	[ObservableProperty] public partial string Answer { get; set; } = "";
	[ObservableProperty] public partial string Error { get; set; } = "";
	[ObservableProperty] public partial bool IsUnlocked { get; set; }

	public ParentGateViewModel(Random random)
	{
		this.random = random;
		Reset();
	}

	public void Reset()
	{
		var left = random.Next(10, 50);
		var right = random.Next(10, 50);
		expected = left + right;
		Challenge = $"{left} + {right} = ?";
		Answer = "";
		Error = "";
		IsUnlocked = false;
	}

	[RelayCommand]
	void Submit()
	{
		if (int.TryParse(Answer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var answer) && answer == expected)
			IsUnlocked = true;
		else
		{
			Reset();
			Error = "Réessaie avec ce nouveau calcul.";
		}
	}
}
