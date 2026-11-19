using System.Globalization;

namespace NextUpKids.Features.WeeklyPlanner;

public sealed class TimerTextConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		IconCatalog.TimerText(value is int seconds ? seconds : 0);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
