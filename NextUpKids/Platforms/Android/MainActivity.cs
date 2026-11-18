using Android.App;
using Android.Content.PM;
using AndroidX.Core.View;

namespace NextUpKids;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.SensorLandscape, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	// Immersive mode: re-applied every time focus returns (after dialogs, app switch...).
	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (!hasFocus || Window is null || WindowCompat.GetInsetsController(Window, Window.DecorView) is not { } bars) return;

		bars.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
		bars.Hide(WindowInsetsCompat.Type.SystemBars());
	}
}
