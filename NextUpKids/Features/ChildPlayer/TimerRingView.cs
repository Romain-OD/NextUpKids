namespace NextUpKids.Features.ChildPlayer;

/// <summary>Visual countdown like a Time Timer: a violet disc that shrinks as time runs out.</summary>
public sealed class TimerRingView : GraphicsView, IDrawable
{
	public static readonly BindableProperty FractionProperty =
		BindableProperty.Create(nameof(Fraction), typeof(double), typeof(TimerRingView), 1d, propertyChanged: Redraw);
	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(TimerRingView), "", propertyChanged: Redraw);

	static readonly Color Ink = Color.FromArgb("#2A2350");
	static readonly Color Time = Color.FromArgb("#7357E8");
	static readonly Color Over = Color.FromArgb("#BDB6D9");

	public TimerRingView()
	{
		Drawable = this;
		InputTransparent = true;
	}

	public double Fraction
	{
		get => (double)GetValue(FractionProperty);
		set => SetValue(FractionProperty, value);
	}

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	static void Redraw(BindableObject view, object oldValue, object newValue) => ((TimerRingView)view).Invalidate();

	public void Draw(ICanvas canvas, RectF rect)
	{
		var size = MathF.Min(rect.Width, rect.Height - 44) - 12;
		if (size <= 0) return;
		var x = rect.Center.X - size / 2;
		var y = rect.Top + 6;
		var fraction = (float)Math.Clamp(Fraction, 0, 1);

		canvas.FillColor = Colors.White;
		canvas.FillEllipse(x, y, size, size);
		if (fraction >= 0.999f)
		{
			canvas.FillColor = Time;
			canvas.FillEllipse(x, y, size, size);
		}
		else if (fraction > 0)
		{
			// Remaining time as a pie starting at 12 o'clock and going clockwise.
			canvas.FillColor = Time;
			var pie = new PathF();
			pie.MoveTo(x + size / 2, y + size / 2);
			pie.LineTo(x + size / 2, y);
			pie.AddArc(x, y, x + size, y + size, 90, 90 - 360 * fraction, true);
			pie.Close();
			canvas.FillPath(pie);
		}
		canvas.StrokeColor = fraction > 0 ? Ink : Over;
		canvas.StrokeSize = 6;
		canvas.DrawEllipse(x, y, size, size);
		canvas.FillColor = Ink;
		canvas.FillCircle(x + size / 2, y + size / 2, 7);

		canvas.FontColor = fraction > 0 ? Ink : Over;
		canvas.FontSize = 30;
		canvas.Font = new Microsoft.Maui.Graphics.Font("NunitoBold");
		canvas.DrawString(Text, rect.Left, y + size + 4, rect.Width, 40, HorizontalAlignment.Center, VerticalAlignment.Center);
	}
}
