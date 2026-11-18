namespace NextUpKids.Features.ChildPlayer;

public sealed class SparkleView : GraphicsView, IDrawable
{
	public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
		nameof(IsActive), typeof(bool), typeof(SparkleView), false,
		propertyChanged: (view, oldValue, newValue) => ((SparkleView)view).Animate((bool)newValue));

	public bool IsActive
	{
		get => (bool)GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	readonly (float X, float Speed, Color Color)[] particles = Enumerable.Range(0, 64)
		.Select(i => ((float)Random.Shared.NextDouble(), 0.7f + (float)Random.Shared.NextDouble(),
			new[] { Colors.Gold, Colors.DeepPink, Colors.Turquoise, Colors.MediumPurple }[i % 4])).ToArray();
	float progress;

	public SparkleView()
	{
		Drawable = this;
		InputTransparent = true;
		Unloaded += (sender, args) => this.AbortAnimation("Sparkles");
	}

	void Animate(bool active)
	{
		this.AbortAnimation("Sparkles");
		if (active)
			new Animation(value => { progress = (float)value; Invalidate(); })
				.Commit(this, "Sparkles", length: 900, easing: Easing.Linear);
		Invalidate();
	}

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (!IsActive) return;
		canvas.Alpha = 1 - progress;
		foreach (var particle in particles)
		{
			canvas.FillColor = particle.Color;
			var x = particle.X * dirtyRect.Width + MathF.Sin(progress * 8 + particle.X) * 30;
			var y = progress * particle.Speed * (dirtyRect.Height + 80) - 40;
			canvas.FillRoundedRectangle(x, y, 10, 18, 3);
		}
	}
}
