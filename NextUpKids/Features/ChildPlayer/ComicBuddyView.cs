using System.Diagnostics;

namespace NextUpKids.Features.ChildPlayer;

/// <summary>
/// "Nino", a comic-style buddy drawn in code: breathes, blinks and points at the step card,
/// waves when a new step appears, jumps and cheers when a step is done, sleeps at bedtime.
/// </summary>
public sealed class ComicBuddyView : GraphicsView, IDrawable
{
	enum Mood { Idle, Wave, Cheer }

	static readonly Color Ink = Color.FromArgb("#2B2D42");
	static readonly Color Skin = Color.FromArgb("#FFD3B0");
	static readonly Color Hair = Color.FromArgb("#7A4A2A");
	static readonly Color Shirt = Color.FromArgb("#58CC02");
	static readonly Color ShirtShade = Color.FromArgb("#46A302");
	static readonly Color Pants = Color.FromArgb("#1CB0F6");
	static readonly Color Shoe = Color.FromArgb("#FF4B4B");
	static readonly Color Cheek = Color.FromArgb("#FF9AA2");
	static readonly Color Mouth = Color.FromArgb("#C7253E");
	static readonly Color Star = Color.FromArgb("#FFC800");
	// Extra width on the right leaves room for the pointing/waving hand.
	const float ArtWidth = 230, ArtHeight = 300, Outline = 5;

	readonly Stopwatch clock = Stopwatch.StartNew();
	readonly PathF hair = CreateHair();
	readonly PathF star = CreateStar();
	IDispatcherTimer? timer;
	Mood mood;
	double moodStart;
	bool sleeping;

	public ComicBuddyView()
	{
		Drawable = this;
		InputTransparent = true;
		Loaded += (_, _) => Start();
		Unloaded += (_, _) => Stop();
	}

	public bool IsSleeping
	{
		get => sleeping;
		set { sleeping = value; Invalidate(); }
	}

	public void Wave() => SetMood(Mood.Wave);
	public void Cheer() => SetMood(Mood.Cheer);

	void SetMood(Mood next)
	{
		mood = next;
		moodStart = clock.Elapsed.TotalSeconds;
		Invalidate();
	}

	void Start()
	{
		// Reduced motion: a still, friendly pose that only changes with the mood.
		if (timer is not null || Motion.IsReduced || Dispatcher is null) return;
		timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(33);
		timer.Tick += (_, _) => Invalidate();
		timer.Start();
	}

	void Stop()
	{
		timer?.Stop();
		timer = null;
	}

	public void Draw(ICanvas canvas, RectF rect)
	{
		var still = Motion.IsReduced;
		var t = still ? 0 : clock.Elapsed.TotalSeconds;
		var since = still ? 0.5 : t - moodStart;
		var current = mood switch
		{
			Mood.Wave when since > 1.6 => Mood.Idle,
			Mood.Cheer when since > 1.2 && !still => Mood.Idle,
			_ => mood,
		};
		if (sleeping) current = Mood.Idle;

		var scale = MathF.Min(rect.Width / ArtWidth, rect.Height / ArtHeight);
		canvas.SaveState();
		// Feet on the bottom edge, leaning towards the card on the right.
		canvas.Translate(rect.Right - ArtWidth * scale, rect.Bottom - ArtHeight * scale);
		canvas.Scale(scale, scale);

		var bob = sleeping ? MathF.Sin((float)t * 1.6f) * 2 : MathF.Sin((float)t * 4f) * 3;
		var jump = 0f;
		if (current == Mood.Cheer && !still)
		{
			var hop = (float)(since % 0.6 / 0.6);
			jump = -MathF.Sin(hop * MathF.PI) * 38;
		}

		canvas.FillColor = Ink.WithAlpha(0.12f);
		var shadow = 1 + jump / 90;
		canvas.FillEllipse(100 - 60 * shadow, 286, 120 * shadow, 14);

		canvas.Translate(0, jump + bob);
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.StrokeLineCap = LineCap.Round;

		// Legs and shoes.
		foreach (var x in new[] { 78f, 106f })
		{
			Shape(canvas, Pants, c => c.FillRoundedRectangle(x, 222, 18, 52, 6), c => c.DrawRoundedRectangle(x, 222, 18, 52, 6));
			Shape(canvas, Shoe, c => c.FillEllipse(x - 6, 266, 32, 18), c => c.DrawEllipse(x - 6, 266, 32, 18));
		}

		var (left, right) = current switch
		{
			_ when sleeping => (115f, 65f),
			Mood.Cheer => (-125f + MathF.Sin((float)t * 14) * 8, -55f - MathF.Sin((float)t * 14) * 8),
			Mood.Wave => (110f, -28f + MathF.Sin((float)since * 14) * 24),
			// Idle: points at the step card with a gentle sway.
			_ => (105f + MathF.Sin((float)t * 2) * 4, -12f + MathF.Sin((float)t * 2.6f) * 6),
		};
		Arm(canvas, 64, 160, left);

		// T-shirt with a star badge.
		Shape(canvas, Shirt, c => c.FillRoundedRectangle(58, 145, 84, 88, 24), c => c.DrawRoundedRectangle(58, 145, 84, 88, 24));
		canvas.FillColor = ShirtShade;
		canvas.FillRoundedRectangle(62, 214, 76, 15, 8);
		canvas.SaveState();
		canvas.Translate(100, 182);
		canvas.Scale(15, 15);
		canvas.FillColor = Star;
		canvas.FillPath(star);
		canvas.RestoreState();

		Arm(canvas, 136, 160, right);
		Head(canvas, current, t, since, still);

		if (sleeping) Snore(canvas, t);
		canvas.RestoreState();
	}

	void Head(ICanvas canvas, Mood current, double t, double since, bool still)
	{
		var tilt = current == Mood.Wave ? MathF.Sin((float)since * 7) * 6 : sleeping ? 10 : 0;
		canvas.SaveState();
		canvas.Rotate(tilt, 100, 140);

		Shape(canvas, Skin, c => c.FillEllipse(34, 82, 20, 26), c => c.DrawEllipse(34, 82, 20, 26));
		Shape(canvas, Skin, c => c.FillEllipse(146, 82, 20, 26), c => c.DrawEllipse(146, 82, 20, 26));
		Shape(canvas, Skin, c => c.FillEllipse(42, 34, 116, 112), c => c.DrawEllipse(42, 34, 116, 112));
		canvas.FillColor = Hair;
		canvas.FillPath(hair);
		canvas.DrawPath(hair);

		canvas.FillColor = Cheek.WithAlpha(0.7f);
		canvas.FillEllipse(56, 104, 20, 12);
		canvas.FillEllipse(124, 104, 20, 12);

		// Blink for 0.12 s roughly every 3.2 s.
		var blink = !still && t % 3.2 > 3.08;
		if (sleeping || current == Mood.Cheer)
		{
			// Closed happy eyes: ^ ^ (or sleepy arcs).
			canvas.StrokeSize = Outline;
			foreach (var x in new[] { 72f, 112f })
				if (sleeping) canvas.DrawArc(x, 86, 18, 12, 180, 360, false, false);
				else canvas.DrawArc(x, 82, 18, 16, 0, 180, false, false);
		}
		else
		{
			var look = current == Mood.Idle ? 3 : 0; // glance at the card
			foreach (var x in new[] { 70f, 110f })
			{
				if (blink)
				{
					canvas.DrawLine(x + 2, 92, x + 18, 92);
					continue;
				}
				Shape(canvas, Colors.White, c => c.FillEllipse(x, 76, 22, 28), c => c.DrawEllipse(x, 76, 22, 28));
				canvas.FillColor = Ink;
				canvas.FillEllipse(x + 6 + look, 84, 12, 15);
				canvas.FillColor = Colors.White;
				canvas.FillCircle(x + 10 + look, 88, 3);
			}
		}

		if (current == Mood.Cheer)
		{
			Shape(canvas, Mouth, c => c.FillArc(82, 104, 36, 30, 180, 360, false), c => c.DrawArc(82, 104, 36, 30, 180, 360, false, true));
			canvas.FillColor = Shoe;
			canvas.FillEllipse(92, 122, 16, 8);
		}
		else if (sleeping) canvas.DrawEllipse(95, 116, 10, 8);
		else canvas.DrawArc(84, 100, 32, 22, 200, 340, false, false);
		canvas.RestoreState();
	}

	static void Arm(ICanvas canvas, float x, float y, float degrees)
	{
		var radians = degrees * MathF.PI / 180;
		float hx = x + MathF.Cos(radians) * 52, hy = y + MathF.Sin(radians) * 52;
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = 22;
		canvas.DrawLine(x, y, hx, hy);
		canvas.StrokeColor = Skin;
		canvas.StrokeSize = 22 - Outline * 2;
		canvas.DrawLine(x, y, hx, hy);
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		Shape(canvas, Skin, c => c.FillCircle(hx, hy, 12), c => c.DrawCircle(hx, hy, 12));
	}

	static void Snore(ICanvas canvas, double t)
	{
		var rise = (float)(t % 2.4 / 2.4);
		canvas.FontColor = Ink.WithAlpha(1 - rise);
		canvas.FontSize = 26;
		canvas.DrawString("Z", 150, 40 - rise * 30, HorizontalAlignment.Left);
		canvas.FontSize = 18;
		canvas.DrawString("z", 172, 14 - rise * 30, HorizontalAlignment.Left);
	}

	static void Shape(ICanvas canvas, Color fill, Action<ICanvas> fillShape, Action<ICanvas> outline)
	{
		canvas.FillColor = fill;
		fillShape(canvas);
		outline(canvas);
	}

	// Messy comic fringe across the top of the head.
	static PathF CreateHair()
	{
		var path = new PathF();
		path.MoveTo(44, 84);
		path.CurveTo(36, 30, 90, 14, 118, 26);
		path.CurveTo(126, 10, 146, 14, 140, 28);
		path.CurveTo(160, 40, 162, 64, 156, 84);
		path.CurveTo(146, 66, 134, 58, 120, 60);
		path.CurveTo(118, 70, 104, 72, 100, 62);
		path.CurveTo(90, 74, 72, 72, 70, 60);
		path.CurveTo(58, 64, 50, 72, 44, 84);
		path.Close();
		return path;
	}

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
