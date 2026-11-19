namespace NextUpKids.Features.ChildPlayer;

/// <summary>Radial "you did it" burst: shockwave ring, sun rays and spinning star sparks (~600 ms).</summary>
public sealed class SparkleView : GraphicsView, IDrawable
{
	const int Rays = 12;
	static readonly Color[] Palette = [Color.FromArgb("#FFC800"), Color.FromArgb("#1CB0F6"), Color.FromArgb("#58CC02"), Color.FromArgb("#FF4B4B")];
	static readonly Color RayColor = Color.FromArgb("#FFE066");

	readonly (float Angle, float Speed, float Size, float Spin, Color Color)[] sparks = Enumerable.Range(0, 24)
		.Select(i => (i * MathF.Tau / 24 + (float)Random.Shared.NextDouble() * 0.25f,
			0.55f + (float)Random.Shared.NextDouble() * 0.45f,
			9 + (float)Random.Shared.NextDouble() * 9,
			(float)(Random.Shared.NextDouble() * 2 - 1) * 6,
			Palette[i % Palette.Length])).ToArray();
	readonly PathF star = CreateStar();
	float progress = 1;

	public SparkleView()
	{
		Drawable = this;
		InputTransparent = true;
		Unloaded += (sender, args) => this.AbortAnimation("Burst");
	}

	public void Burst()
	{
		this.AbortAnimation("Burst");
		new Animation(value => { progress = (float)value; Invalidate(); })
			.Commit(this, "Burst", length: 600, easing: Easing.Linear, finished: (_, _) => { progress = 1; Invalidate(); });
	}

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (progress >= 1) return;
		float cx = dirtyRect.Width / 2, cy = dirtyRect.Height * 0.45f;
		var reach = MathF.Min(dirtyRect.Width, dirtyRect.Height) * 0.6f;
		var outward = 1 - (1 - progress) * (1 - progress) * (1 - progress); // ease-out cubic
		var fade = 1 - progress;

		canvas.StrokeColor = RayColor;
		canvas.StrokeLineCap = LineCap.Round;
		canvas.Alpha = fade * 0.8f;
		canvas.StrokeSize = 10 * fade + 2;
		for (var i = 0; i < Rays; i++)
		{
			var a = i * MathF.Tau / Rays;
			float c = MathF.Cos(a), s = MathF.Sin(a);
			canvas.DrawLine(cx + c * reach * 0.25f * outward, cy + s * reach * 0.25f * outward,
				cx + c * reach * 0.55f * outward, cy + s * reach * 0.55f * outward);
		}

		canvas.Alpha = fade;
		canvas.StrokeColor = Palette[0];
		canvas.StrokeSize = 14 * fade + 1;
		var ring = reach * 0.7f * outward;
		canvas.DrawEllipse(cx - ring, cy - ring, ring * 2, ring * 2);

		foreach (var spark in sparks)
		{
			var distance = reach * spark.Speed * outward;
			canvas.SaveState();
			canvas.Translate(cx + MathF.Cos(spark.Angle) * distance, cy + MathF.Sin(spark.Angle) * distance);
			canvas.Rotate(spark.Spin * progress * 60);
			canvas.Scale(spark.Size * (1.2f - progress * 0.5f), spark.Size * (1.2f - progress * 0.5f));
			canvas.FillColor = spark.Color;
			canvas.FillPath(star);
			canvas.RestoreState();
		}
	}

	// Unit five-point star centred on (0,0); scaled per spark at draw time.
	static PathF CreateStar()
	{
		var path = new PathF();
		for (var i = 0; i < 10; i++)
		{
			var r = i % 2 == 0 ? 1f : 0.45f;
			var a = -MathF.PI / 2 + i * MathF.PI / 5;
			if (i == 0) path.MoveTo(MathF.Cos(a) * r, MathF.Sin(a) * r);
			else path.LineTo(MathF.Cos(a) * r, MathF.Sin(a) * r);
		}
		path.Close();
		return path;
	}
}
