using System.Diagnostics;

namespace NextUpKids.Features.ChildPlayer;

/// <summary>
/// "Filou", a storybook fox drawn in code (soft shading, fur tufts, amber eyes) that shows the child how to do the current step:
/// it waves when a step appears, then acts it out from the step illustration (brushing teeth,
/// putting shoes on...), jumps and cheers when the step is done, and sleeps at bedtime.
/// </summary>
public sealed class FoxBuddyView : GraphicsView, IDrawable
{
	enum Mood { Idle, Wave, Cheer }

	enum Act { Point, Brush, Shower, Dress, Pajamas, Coat, Toilet, Shoes, Wash, Eat, Write, Read, Comb, Backpack, Tidy, Play, Hockey, Swim, Skate, Sleep }

	// Warm brown line work instead of a black comic outline.
	static readonly Color Ink = Color.FromArgb("#3A2418");
	static readonly Color Fur = Color.FromArgb("#E8742A");
	static readonly Color FurLight = Color.FromArgb("#F9A55A");
	static readonly Color FurShade = Color.FromArgb("#B9521A");
	static readonly Color Cream = Color.FromArgb("#FFF8EE");
	static readonly Color CreamShade = Color.FromArgb("#EBD9C4");
	static readonly Color Paw = Color.FromArgb("#3B2519");
	static readonly Color Stocking = Color.FromArgb("#5B3726");
	static readonly Color Iris = Color.FromArgb("#E3A032");
	static readonly Color Cheek = Color.FromArgb("#FF9AA2");
	static readonly Color Mouth = Color.FromArgb("#C7253E");
	static readonly Color Water = Color.FromArgb("#1CB0F6");
	static readonly Color Green = Color.FromArgb("#58CC02");
	static readonly Color Red = Color.FromArgb("#FF4B4B");
	static readonly Color Purple = Color.FromArgb("#CE82FF");
	static readonly Color Sun = Color.FromArgb("#FFC800");
	static readonly Color Wood = Color.FromArgb("#B9773C");
	static readonly Color Petal = Color.FromArgb("#FFB3D1");
	// Extra width on the right leaves room for the pointing/waving paw.
	const float ArtWidth = 230, ArtHeight = 300, Outline = 3.5f;

	static readonly Dictionary<string, Act> Acts = new()
	{
		["toothbrush"] = Act.Brush, ["shower"] = Act.Shower, ["dress"] = Act.Dress, ["pajamas"] = Act.Pajamas,
		["coat"] = Act.Coat, ["toilet"] = Act.Toilet, ["shoes"] = Act.Shoes, ["handwash"] = Act.Wash,
		["meal"] = Act.Eat, ["homework"] = Act.Write, ["book"] = Act.Read, ["comb"] = Act.Comb,
		["backpack"] = Act.Backpack, ["tidy"] = Act.Tidy, ["play"] = Act.Play, ["hockey"] = Act.Hockey,
		["swimming"] = Act.Swim, ["skating"] = Act.Skate, ["sleep"] = Act.Sleep
	};

	readonly Stopwatch clock = Stopwatch.StartNew();
	IDispatcherTimer? timer;
	Mood mood;
	Act act;
	double moodStart;
	bool sleeping;

	public FoxBuddyView()
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

	/// <summary>True when Filou can act out the step himself, so no picture card is needed.</summary>
	public static bool CanAct(string? image) => Acts.ContainsKey(Path.GetFileNameWithoutExtension(image ?? "").ToLowerInvariant());

	/// <summary>Waves hello, then demonstrates the step drawn by <paramref name="image"/>.</summary>
	public void Show(string? image)
	{
		act = Acts.GetValueOrDefault(Path.GetFileNameWithoutExtension(image ?? "").ToLowerInvariant(), Act.Point);
		SetMood(Mood.Wave);
	}

	public void Cheer() => SetMood(Mood.Cheer);

	void SetMood(Mood next)
	{
		mood = next;
		moodStart = clock.Elapsed.TotalSeconds;
		Invalidate();
	}

	void Start()
	{
		// Reduced motion: a still pose of the action that only changes with the step.
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
		var t = still ? 0.4f : (float)clock.Elapsed.TotalSeconds;
		var since = still ? 2f : t - (float)moodStart;
		var current = mood switch
		{
			Mood.Wave when since > 1.3 => Mood.Idle,
			Mood.Cheer when since > 1.2 && !still => Mood.Idle,
			_ => mood,
		};
		var doing = sleeping ? Act.Sleep : current == Mood.Idle ? act : Act.Point;

		var scale = MathF.Min(rect.Width / ArtWidth, rect.Height / ArtHeight);
		canvas.SaveState();
		// Feet on the bottom edge, centred on the stage.
		canvas.Translate(rect.Center.X - 100 * scale, rect.Bottom - ArtHeight * scale);
		canvas.Scale(scale, scale);
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.StrokeLineCap = LineCap.Round;

		var jump = current == Mood.Cheer && !still ? -MathF.Sin(since % 0.6f / 0.6f * MathF.PI) * 38 : 0;
		var bob = sleeping ? MathF.Sin(t * 1.6f) * 2 : MathF.Sin(t * 4f) * 3;
		var sit = doing switch { Act.Toilet => 22f, Act.Shoes => 26f, _ => 0f };
		var sway = doing == Act.Skate ? MathF.Sin(t * 2.4f) * 14 : doing == Act.Backpack ? MathF.Sin(t * 5) * 3 : 0;

		// Soft ground shadow: stacked ellipses fake a blur.
		var shadow = 1 + jump / 90;
		for (var i = 0; i < 4; i++)
		{
			canvas.FillColor = Ink.WithAlpha(0.05f);
			var w = (130 - i * 14) * shadow;
			canvas.FillEllipse(100 - w / 2 + sway, 284 + i * 1.5f, w, 18 - i * 3);
		}

		if (doing == Act.Tidy) ToyBox(canvas);
		if (doing == Act.Toilet) Toilet(canvas);

		canvas.SaveState();
		canvas.Translate(sway, jump + bob + sit);

		Tail(canvas, t, still);
		if (doing == Act.Backpack) Backpack(canvas);
		Legs(canvas, doing, t, sit);

		var (left, right) = Hands(current, doing, t, since);

		// Shaded body with a tufted white chest; outfits are drawn over it.
		var body = new PathF();
		body.AppendRoundedRectangle(60, 140, 80, 95, 30);
		Shaded(canvas, body, FurLight, FurShade);
		Shaded(canvas, Chest(), Cream, CreamShade, outline: false);
		canvas.FillColor = Ink.WithAlpha(0.18f);
		canvas.FillEllipse(70, 136, 60, 14);
		if (doing is Act.Dress or Act.Pajamas) Garment(canvas, doing, t);
		if (doing == Act.Coat) Coat(canvas);

		Head(canvas, current, doing, t, since, still);
		if (doing == Act.Coat) Beanie(canvas, t, still);
		ArmTo(canvas, 64, 160, left);
		ArmTo(canvas, 136, 160, right);
		Props(canvas, doing, t, left, right, still);
		canvas.RestoreState();

		if (doing == Act.Swim) Waves(canvas, t);
		if (doing == Act.Sleep) Snore(canvas, t);
		canvas.RestoreState();
	}

	static (PointF Left, PointF Right) Hands(Mood current, Act doing, float t, float since)
	{
		if (current == Mood.Cheer)
			return (new(36 + MathF.Sin(t * 14) * 6, 92), new(164 - MathF.Sin(t * 14) * 6, 92));
		if (current == Mood.Wave)
			return (new(58, 210), new(178 + MathF.Sin(since * 14) * 14, 104));
		var s = MathF.Sin(t * 6);
		return doing switch
		{
			Act.Brush => (new(58, 210), new(126 + MathF.Sin(t * 18) * 7, 124)),
			Act.Shower => (new(72 + s * 6, 52), new(128 - s * 6, 52)),
			Act.Comb => (new(58, 210), new(100 + MathF.Sin(t * 3) * 28, 40)),
			Act.Dress or Act.Pajamas => DressHands(t),
			Act.Coat => (new(52, 60), new(148, 60)),
			Act.Toilet => (new(60, 205), new(140, 205)),
			Act.Shoes => (new(82 + s * 5, 248), new(118 - s * 5, 248)),
			Act.Wash => (new(92 + s * 7, 192), new(108 - s * 7, 194)),
			Act.Eat => (new(78, 196), EatHand(t)),
			Act.Write => (new(70, 206), new(92 + (t * 30 % 40), 204 + MathF.Sin(t * 20) * 3)),
			Act.Read => (new(70, 192), new(130, 192)),
			Act.Backpack => (new(78, 170), new(122, 170)),
			Act.Tidy => (new(58, 210), TidyHand(t)),
			Act.Play => (new(58, 210), new(176, 196 + MathF.Abs(MathF.Sin(t * 4)) * 12)),
			Act.Hockey => (new(150 + MathF.Sin(t * 3) * 10, 196), new(166 + MathF.Sin(t * 3) * 14, 206)),
			Act.Swim => (Circle(64, 160, t * 4), Circle(136, 160, t * 4 + MathF.PI)),
			Act.Skate => (new(20, 150 + MathF.Sin(t * 2.4f) * 10), new(180, 150 - MathF.Sin(t * 2.4f) * 10)),
			Act.Sleep => (new(70, 212), new(130, 212)),
			// Point at the step card with a gentle sway.
			_ => (new(58, 210 + MathF.Sin(t * 2) * 3), new(190, 152 + MathF.Sin(t * 2.6f) * 5)),
		};
	}

	static (PointF, PointF) DressHands(float t)
	{
		var y = 20 + Slide(t) * 130;
		return (new(56, y + 30), new(144, y + 30));
	}

	// Garment progress: drops from above the head to the body, holds, and starts again.
	static float Slide(float t) => Clamp01(t % 3.2f / 1.8f);

	static PointF EatHand(float t)
	{
		var k = (MathF.Sin(t * 3) + 1) / 2;
		return new(84 + k * 16, 196 - k * 72);
	}

	static PointF TidyHand(float t)
	{
		var k = t % 2 / 2;
		return new(178, 270 - MathF.Sin(k * MathF.PI) * 70 - k * 20);
	}

	static PointF Circle(float x, float y, float a) => new(x + MathF.Cos(a) * 46, y - 20 + MathF.Sin(a) * 46);

	static float Clamp01(float v) => Math.Clamp(v, 0, 1);

	void Head(ICanvas canvas, Mood current, Act doing, float t, float since, bool still)
	{
		var tilt = current == Mood.Wave ? MathF.Sin(since * 7) * 6 : doing == Act.Sleep ? 10 : doing == Act.Read ? 6 : 0;
		canvas.SaveState();
		canvas.Rotate(tilt, 100, 140);

		// Tall ears with black tips and a fluffy cream inside.
		foreach (var flip in new[] { 1f, -1f })
		{
			var ear = Triangle(100 - 50 * flip, 74, 100 - 46 * flip, 2, 100 - 10 * flip, 48);
			Shaded(canvas, ear, FurLight, FurShade);
			canvas.FillColor = Paw;
			canvas.FillPath(Triangle(100 - 46 * flip, 2, 100 - 47.6f * flip, 31, 100 - 31.6f * flip, 21));
			canvas.FillColor = CreamShade;
			canvas.FillPath(Triangle(100 - 42 * flip, 64, 100 - 42 * flip, 30, 100 - 20 * flip, 48));
			canvas.StrokeColor = Cream;
			canvas.StrokeSize = 2;
			for (var i = 0; i < 3; i++) canvas.DrawLine(100 - (38 - i * 5) * flip, 62, 100 - (34 - i * 3) * flip, 44 + i * 2);
			canvas.StrokeColor = Ink;
			canvas.DrawPath(ear);
			canvas.StrokeSize = Outline;
		}
		Shaded(canvas, HeadShape(), FurLight, FurShade);

		// Darker fur around the eyes and a lighter forehead streak.
		canvas.FillColor = FurShade.WithAlpha(0.35f);
		canvas.FillEllipse(62, 70, 34, 30);
		canvas.FillEllipse(104, 70, 34, 30);
		canvas.FillColor = FurLight.WithAlpha(0.6f);
		canvas.FillEllipse(90, 46, 20, 34);

		// White cheek ruffs and muzzle.
		Shaded(canvas, FaceShape(), Cream, CreamShade, outline: false);
		canvas.FillColor = Cheek.WithAlpha(0.3f);
		canvas.FillEllipse(56, 108, 18, 10);
		canvas.FillEllipse(126, 108, 18, 10);
		canvas.FillColor = Ink.WithAlpha(0.5f);
		foreach (var (wx, wy) in new[] { (84f, 118f), (80f, 124f), (116f, 118f), (120f, 124f) }) canvas.FillCircle(wx, wy, 1.4f);

		var blink = !still && t % 3.2 > 3.08;
		var closed = doing == Act.Sleep || current == Mood.Cheer || (doing == Act.Shower && current == Mood.Idle);
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		if (closed)
		{
			foreach (var x in new[] { 70f, 112f })
				if (doing == Act.Sleep) canvas.DrawArc(x, 82, 18, 12, 180, 360, false, false);
				else canvas.DrawArc(x, 78, 18, 16, 0, 180, false, false);
		}
		else
		{
			var (lx, ly) = doing switch
			{
				Act.Point when current == Mood.Idle => (4f, 0f),
				Act.Read or Act.Write or Act.Shoes or Act.Wash or Act.Tidy => (0f, 5f),
				_ => (0f, 0f),
			};
			foreach (var x in new[] { 70f, 112f })
			{
				if (blink)
				{
					canvas.DrawLine(x + 2, 88, x + 18, 88);
					continue;
				}
				Eye(canvas, x + 10, 86, lx, ly, x < 100);
			}
		}

		// Shiny black nose at the tip of the muzzle.
		var nose = new PathF();
		nose.MoveTo(91, 104);
		nose.CurveTo(96, 100, 104, 100, 109, 104);
		nose.CurveTo(108, 111, 103, 115, 100, 115);
		nose.CurveTo(97, 115, 92, 111, 91, 104);
		nose.Close();
		canvas.FillColor = Color.FromArgb("#1E120B");
		canvas.FillPath(nose);
		canvas.FillColor = Colors.White.WithAlpha(0.7f);
		canvas.FillEllipse(95, 104, 6, 3);
		if (current == Mood.Cheer || doing == Act.Eat && EatHand(t).Y < 140)
		{
			Shape(canvas, Mouth, c => c.FillArc(86, 112, 28, 22, 180, 360, false), c => c.DrawArc(86, 112, 28, 22, 180, 360, false, true));
		}
		else if (doing == Act.Sleep) canvas.DrawEllipse(96, 120, 8, 7);
		else
		{
			canvas.DrawArc(86, 110, 14, 12, 180, 360, false, false);
			canvas.DrawArc(100, 110, 14, 12, 180, 360, false, false);
		}
		if (doing != Act.Coat) Flower(canvas, 136, 46);
		canvas.RestoreState();
	}

	static void Flower(ICanvas canvas, float x, float y)
	{
		canvas.StrokeSize = 2.5f;
		for (var i = 0; i < 5; i++)
		{
			var a = i * MathF.Tau / 5 - MathF.PI / 2;
			var (px, py) = (x + MathF.Cos(a) * 8, y + MathF.Sin(a) * 8);
			Shape(canvas, Petal, c => c.FillCircle(px, py, 7), c => c.DrawCircle(px, py, 7));
		}
		Shape(canvas, Sun, c => c.FillCircle(x, y, 5), c => c.DrawCircle(x, y, 5));
		canvas.StrokeSize = Outline;
	}

	// Almond fox eye: dark rim, amber iris, slit pupil and two catch-lights.
	static void Eye(ICanvas canvas, float cx, float cy, float lx, float ly, bool left)
	{
		var lid = new PathF();
		var outer = left ? -13f : 13f;
		lid.MoveTo(cx - outer, cy + 2);
		lid.CurveTo(cx - outer * 0.4f, cy - 12, cx + outer * 0.6f, cy - 12, cx + outer, cy - 3);
		lid.CurveTo(cx + outer * 0.5f, cy + 11, cx - outer * 0.5f, cy + 11, cx - outer, cy + 2);
		lid.Close();
		canvas.FillColor = Ink;
		canvas.FillPath(lid);
		canvas.SaveState();
		canvas.ClipPath(lid);
		var (ix, iy) = (cx + lx * 0.7f, cy + ly * 0.6f);
		canvas.SaveState();
		canvas.SetFillPaint(new RadialGradientPaint { StartColor = Color.FromArgb("#FFD27A"), EndColor = Color.FromArgb("#B86E14"), Center = new Point(0.45, 0.35), Radius = 0.6 }, new RectF(ix - 9, iy - 9, 18, 18));
		canvas.FillCircle(ix, iy, 8.5f);
		canvas.RestoreState();
		canvas.FillColor = Color.FromArgb("#140B06");
		canvas.FillEllipse(ix - 2.6f, iy - 7, 5.2f, 14);
		canvas.FillColor = Colors.White;
		canvas.FillCircle(ix + 2.5f, iy - 3.5f, 2.6f);
		canvas.FillCircle(ix - 3, iy + 3.5f, 1.2f);
		canvas.RestoreState();
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
	}

	// Rounded skull flaring into fluffy cheek tufts, narrowing to the chin.
	static PathF HeadShape()
	{
		var head = new PathF();
		head.MoveTo(100, 38);
		head.CurveTo(130, 38, 152, 56, 154, 84);
		head.CurveTo(160, 96, 166, 104, 172, 112);
		head.LineTo(158, 114);
		head.LineTo(165, 124);
		head.LineTo(148, 124);
		head.CurveTo(136, 140, 116, 150, 100, 150);
		head.CurveTo(84, 150, 64, 140, 52, 124);
		head.LineTo(35, 124);
		head.LineTo(42, 114);
		head.LineTo(28, 112);
		head.CurveTo(34, 104, 40, 96, 46, 84);
		head.CurveTo(48, 56, 70, 38, 100, 38);
		head.Close();
		return head;
	}

	static PathF FaceShape()
	{
		var face = new PathF();
		face.MoveTo(34, 112);
		face.CurveTo(52, 104, 72, 100, 86, 102);
		face.CurveTo(92, 96, 108, 96, 114, 102);
		face.CurveTo(128, 100, 148, 104, 166, 112);
		face.LineTo(154, 116);
		face.LineTo(160, 123);
		face.LineTo(147, 123);
		face.CurveTo(135, 139, 116, 148, 100, 148);
		face.CurveTo(84, 148, 65, 139, 53, 123);
		face.LineTo(40, 123);
		face.LineTo(46, 116);
		face.Close();
		return face;
	}

	static PathF Chest()
	{
		var chest = new PathF();
		chest.MoveTo(72, 144);
		chest.LineTo(128, 144);
		chest.CurveTo(127, 172, 124, 200, 116, 224);
		chest.LineTo(108, 216);
		chest.LineTo(100, 228);
		chest.LineTo(92, 216);
		chest.LineTo(84, 224);
		chest.CurveTo(76, 200, 73, 172, 72, 144);
		chest.Close();
		return chest;
	}

	// Soft light from the top-left gives the fur some volume.
	static void Shaded(ICanvas canvas, PathF path, Color light, Color dark, bool outline = true)
	{
		// Save/Restore keeps the gradient from leaking into later fills (Android keeps the shader).
		canvas.SaveState();
		canvas.SetFillPaint(new RadialGradientPaint { StartColor = light, EndColor = dark, Center = new Point(0.35, 0.3), Radius = 0.85 }, path.Bounds);
		canvas.FillPath(path);
		canvas.RestoreState();
		if (!outline) return;
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		canvas.DrawPath(path);
	}

	static void Tail(ICanvas canvas, float t, bool still)
	{
		canvas.SaveState();
		canvas.Rotate(still ? 0 : MathF.Sin(t * 3) * 10, 72, 222);
		// Big bushy tail with a tufted white tip.
		var tail = new PathF();
		tail.MoveTo(80, 226);
		tail.CurveTo(44, 248, 0, 222, 4, 176);
		tail.CurveTo(4, 166, 6, 158, 10, 148);
		tail.LineTo(16, 160);
		tail.LineTo(20, 145);
		tail.LineTo(28, 163);
		tail.CurveTo(42, 184, 62, 196, 84, 204);
		tail.Close();
		Shaded(canvas, tail, FurLight, FurShade, outline: false);
		var tip = new PathF();
		tip.MoveTo(4, 178);
		tip.CurveTo(4, 166, 6, 158, 10, 148);
		tip.LineTo(16, 160);
		tip.LineTo(20, 145);
		tip.LineTo(28, 163);
		tip.CurveTo(32, 172, 32, 182, 28, 190);
		tip.LineTo(22, 184);
		tip.LineTo(18, 194);
		tip.LineTo(12, 186);
		tip.Close();
		Shaded(canvas, tip, Cream, CreamShade, outline: false);
		canvas.StrokeColor = FurLight.WithAlpha(0.8f);
		canvas.StrokeSize = 2;
		canvas.DrawArc(20, 196, 46, 30, 200, 260, false, false);
		canvas.DrawArc(30, 204, 40, 24, 200, 250, false, false);
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		canvas.DrawPath(tail);
		canvas.RestoreState();
	}

	static void Legs(ICanvas canvas, Act doing, float t, float sit)
	{
		foreach (var (x, i) in new[] { (78f, 0), (104f, 1) })
		{
			var swing = doing == Act.Toilet ? MathF.Sin(t * 5 + i * MathF.PI) * 6 : 0;
			var length = 48 - sit;
			// Dark "stockings" like a real fox.
			var leg = new PathF();
			leg.AppendRoundedRectangle(x, 224, 18, length, 6);
			Shaded(canvas, leg, Stocking, Paw);
			var y = 224 + length - 8 + swing;
			Shape(canvas, Paw, c => c.FillEllipse(x - 6, y, 30, 16), c => c.DrawEllipse(x - 6, y, 30, 16));
			if (doing == Act.Shoes)
				Shape(canvas, Red, c => c.FillRoundedRectangle(x - 8, y + 2, 34, 16, 8), c => c.DrawRoundedRectangle(x - 8, y + 2, 34, 16, 8));
			if (doing == Act.Skate)
			{
				Shape(canvas, Water, c => c.FillRoundedRectangle(x - 8, y + 2, 34, 14, 6), c => c.DrawRoundedRectangle(x - 8, y + 2, 34, 14, 6));
				canvas.DrawLine(x - 10, y + 22, x + 28, y + 22);
			}
		}
	}

	static void ArmTo(ICanvas canvas, float x, float y, PointF hand)
	{
		var dx = hand.X - x;
		var dy = hand.Y - y;
		var length = MathF.Sqrt(dx * dx + dy * dy);
		if (length > 84)
		{
			dx *= 84 / length;
			dy *= 84 / length;
		}
		float hx = x + dx, hy = y + dy;
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = 22;
		canvas.DrawLine(x, y, hx, hy);
		canvas.StrokeColor = Fur;
		canvas.StrokeSize = 22 - Outline * 2;
		canvas.DrawLine(x, y, hx, hy);
		// The forearm darkens towards the paw.
		canvas.StrokeColor = Stocking;
		canvas.DrawLine(x + dx * 0.55f, y + dy * 0.55f, hx, hy);
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
		Shape(canvas, Paw, c => c.FillCircle(hx, hy, 12), c => c.DrawCircle(hx, hy, 12));
	}

	static void Props(ICanvas canvas, Act doing, float t, PointF left, PointF right, bool still)
	{
		var k = still ? 0.4f : t;
		switch (doing)
		{
			case Act.Brush:
				canvas.StrokeSize = 6;
				canvas.StrokeColor = Water;
				canvas.DrawLine(right.X + 4, right.Y + 8, right.X - 24, right.Y - 2);
				canvas.StrokeColor = Ink;
				canvas.StrokeSize = Outline;
				Bubbles(canvas, 86, 146, k, 3, 6);
				break;
			case Act.Shower:
				canvas.StrokeColor = Water;
				canvas.StrokeSize = 5;
				for (var i = 0; i < 7; i++)
				{
					var x = 40 + i * 20;
					var y = (k * 120 + i * 37) % 110 - 30;
					canvas.DrawLine(x, y, x, y + 14);
				}
				canvas.StrokeColor = Ink;
				canvas.StrokeSize = Outline;
				Bubbles(canvas, 100, 40, k, 4, 9);
				break;
			case Act.Wash:
				Bubbles(canvas, 100, 176, k, 5, 8);
				break;
			case Act.Eat:
				Shape(canvas, Colors.White, c => c.FillArc(56, 186, 52, 40, 180, 360, false), c => c.DrawArc(56, 186, 52, 40, 180, 360, false, true));
				canvas.FillColor = Sun;
				canvas.FillEllipse(right.X - 8, right.Y - 22, 16, 12);
				canvas.DrawLine(right.X, right.Y, right.X, right.Y - 14);
				break;
			case Act.Write:
				Shape(canvas, Colors.White, c => c.FillRectangle(62, 196, 76, 40), c => c.DrawRectangle(62, 196, 76, 40));
				canvas.StrokeColor = Water;
				canvas.StrokeSize = 3;
				canvas.DrawLine(70, 210, Math.Max(70, right.X - 4), 210);
				canvas.StrokeColor = Sun;
				canvas.StrokeSize = 6;
				canvas.DrawLine(right.X, right.Y, right.X + 10, right.Y - 22);
				canvas.StrokeColor = Ink;
				canvas.StrokeSize = Outline;
				break;
			case Act.Read:
				Shape(canvas, Red, c => c.FillRoundedRectangle(66, 176, 68, 40, 4), c => c.DrawRoundedRectangle(66, 176, 68, 40, 4));
				Shape(canvas, Colors.White, c => c.FillRectangle(70, 180, 28, 30), c => c.DrawRectangle(70, 180, 28, 30));
				Shape(canvas, Colors.White, c => c.FillRectangle(102, 180, 28, 30), c => c.DrawRectangle(102, 180, 28, 30));
				break;
			case Act.Comb:
				Shape(canvas, Purple, c => c.FillRoundedRectangle(right.X - 22, right.Y - 16, 44, 10, 3), c => c.DrawRoundedRectangle(right.X - 22, right.Y - 16, 44, 10, 3));
				break;
			case Act.Play:
				var ball = new PointF(right.X, right.Y + 26 + MathF.Abs(MathF.Cos(k * 4)) * 50);
				Shape(canvas, Red, c => c.FillCircle(ball.X, ball.Y, 16), c => c.DrawCircle(ball.X, ball.Y, 16));
				canvas.StrokeColor = Colors.White;
				canvas.DrawArc(ball.X - 10, ball.Y - 10, 20, 20, 200, 320, false, false);
				canvas.StrokeColor = Ink;
				break;
			case Act.Hockey:
				canvas.StrokeColor = Wood;
				canvas.StrokeSize = 8;
				var blade = new PointF(right.X + 30 + MathF.Sin(k * 3) * 16, 282);
				canvas.DrawLine(left.X, left.Y, blade.X, blade.Y);
				canvas.DrawLine(blade.X, blade.Y, blade.X + 22, blade.Y);
				canvas.StrokeColor = Ink;
				canvas.StrokeSize = Outline;
				canvas.FillColor = Ink;
				canvas.FillEllipse(blade.X + 34 + (k * 40 % 30), 280, 18, 8);
				break;
			case Act.Tidy:
				Shape(canvas, Sun, c => c.FillRectangle(right.X - 12, right.Y - 30, 24, 24), c => c.DrawRectangle(right.X - 12, right.Y - 30, 24, 24));
				break;
			case Act.Shoes:
				canvas.StrokeColor = Colors.White;
				canvas.StrokeSize = 3;
				canvas.DrawEllipse(right.X - 10, right.Y + 14 - MathF.Abs(MathF.Sin(k * 6)) * 6, 12, 10);
				canvas.DrawEllipse(left.X - 4, left.Y + 14 - MathF.Abs(MathF.Sin(k * 6)) * 6, 12, 10);
				canvas.StrokeColor = Ink;
				canvas.StrokeSize = Outline;
				break;
		}
	}

	static void Garment(ICanvas canvas, Act doing, float t)
	{
		// Shirt (or striped pyjama) pulled down over the head onto the body.
		var y = -120 + Slide(t) * 132;
		var color = doing == Act.Pajamas ? Purple : Water;
		var shirt = new PathF();
		shirt.MoveTo(64, y + 142);
		shirt.LineTo(44, y + 160);
		shirt.LineTo(56, y + 178);
		shirt.LineTo(64, y + 170);
		shirt.LineTo(64, y + 228);
		shirt.LineTo(136, y + 228);
		shirt.LineTo(136, y + 170);
		shirt.LineTo(144, y + 178);
		shirt.LineTo(156, y + 160);
		shirt.LineTo(136, y + 142);
		shirt.LineTo(116, y + 140);
		shirt.CurveTo(110, y + 152, 90, y + 152, 84, y + 140);
		shirt.Close();
		Shape(canvas, color, c => c.FillPath(shirt), c => c.DrawPath(shirt));
		if (doing == Act.Pajamas)
		{
			canvas.FillColor = Colors.White.WithAlpha(0.6f);
			for (var i = 0; i < 4; i++) canvas.FillRectangle(66, y + 164 + i * 16, 68, 6);
		}
	}

	static void Coat(ICanvas canvas)
	{
		Shape(canvas, Green, c => c.FillRoundedRectangle(58, 146, 84, 92, 24), c => c.DrawRoundedRectangle(58, 146, 84, 92, 24));
		canvas.DrawLine(100, 152, 100, 234);
		Shape(canvas, Red, c => c.FillRoundedRectangle(60, 136, 80, 20, 10), c => c.DrawRoundedRectangle(60, 136, 80, 20, 10));
		Shape(canvas, Red, c => c.FillRoundedRectangle(112, 146, 18, 44, 6), c => c.DrawRoundedRectangle(112, 146, 18, 44, 6));
	}

	static void Beanie(ICanvas canvas, float t, bool still)
	{
		var y = still ? 0 : -70 * (1 - Clamp01(t % 3.2f / 1.4f));
		Shape(canvas, Red, c => c.FillArc(50, y + 22, 100, 70, 0, 180, true), c => c.DrawArc(50, y + 22, 100, 70, 0, 180, true, true));
		Shape(canvas, Colors.White, c => c.FillRoundedRectangle(46, y + 50, 108, 16, 8), c => c.DrawRoundedRectangle(46, y + 50, 108, 16, 8));
		Shape(canvas, Colors.White, c => c.FillCircle(100, y + 18, 11), c => c.DrawCircle(100, y + 18, 11));
	}

	static void Backpack(ICanvas canvas)
	{
		Shape(canvas, Purple, c => c.FillRoundedRectangle(34, 150, 50, 70, 14), c => c.DrawRoundedRectangle(34, 150, 50, 70, 14));
		Shape(canvas, Sun, c => c.FillRoundedRectangle(40, 186, 30, 22, 6), c => c.DrawRoundedRectangle(40, 186, 30, 22, 6));
	}

	static void Toilet(ICanvas canvas)
	{
		Shape(canvas, Colors.White, c => c.FillRoundedRectangle(56, 140, 88, 60, 10), c => c.DrawRoundedRectangle(56, 140, 88, 60, 10));
		var bowl = new PathF();
		bowl.MoveTo(70, 240);
		bowl.LineTo(78, 290);
		bowl.LineTo(122, 290);
		bowl.LineTo(130, 240);
		bowl.Close();
		Shape(canvas, Colors.White, c => c.FillPath(bowl), c => c.DrawPath(bowl));
		Shape(canvas, Colors.White, c => c.FillEllipse(44, 226, 112, 26), c => c.DrawEllipse(44, 226, 112, 26));
	}

	static void ToyBox(ICanvas canvas)
	{
		Shape(canvas, Wood, c => c.FillRoundedRectangle(158, 248, 66, 44, 6), c => c.DrawRoundedRectangle(158, 248, 66, 44, 6));
		canvas.FillColor = Green;
		canvas.FillCircle(176, 246, 10);
		canvas.FillColor = Water;
		canvas.FillRectangle(196, 236, 16, 14);
	}

	static void Waves(ICanvas canvas, float t)
	{
		var water = new PathF();
		water.MoveTo(0, 300);
		water.LineTo(0, 214);
		for (var x = 0; x <= 230; x += 23)
			water.QuadTo(x + 11.5f, 204 + MathF.Sin(t * 3 + x / 20f) * 6, x + 23, 214);
		water.LineTo(230, 300);
		water.Close();
		canvas.FillColor = Water.WithAlpha(0.75f);
		canvas.FillPath(water);
	}

	static void Bubbles(ICanvas canvas, float x, float y, float t, int count, float size)
	{
		canvas.StrokeColor = Water;
		canvas.StrokeSize = 3;
		canvas.FillColor = Colors.White;
		for (var i = 0; i < count; i++)
		{
			var rise = (t * 0.8f + i / (float)count) % 1;
			var bx = x + MathF.Sin(i * 2.1f + t * 3) * 18;
			var r = size * (0.6f + rise * 0.6f);
			canvas.FillCircle(bx, y - rise * 40, r);
			canvas.DrawCircle(bx, y - rise * 40, r);
		}
		canvas.StrokeColor = Ink;
		canvas.StrokeSize = Outline;
	}

	static void Snore(ICanvas canvas, float t)
	{
		var rise = t % 2.4f / 2.4f;
		canvas.FontColor = Ink.WithAlpha(1 - rise);
		canvas.FontSize = 26;
		canvas.DrawString("Z", 160, 40 - rise * 30, HorizontalAlignment.Left);
		canvas.FontSize = 18;
		canvas.DrawString("z", 182, 14 - rise * 30, HorizontalAlignment.Left);
	}

	static PathF Triangle(float x1, float y1, float x2, float y2, float x3, float y3)
	{
		var path = new PathF();
		path.MoveTo(x1, y1);
		path.LineTo(x2, y2);
		path.LineTo(x3, y3);
		path.Close();
		return path;
	}

	static void Shape(ICanvas canvas, Color fill, Action<ICanvas> fillShape, Action<ICanvas> outline)
	{
		canvas.FillColor = fill;
		fillShape(canvas);
		outline(canvas);
	}
}