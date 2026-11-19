namespace NextUpKids.Features.ChildPlayer;

/// <summary>Honours the OS "remove animations" / "reduce motion" accessibility setting.</summary>
public static class Motion
{
	public static bool IsReduced
	{
		get
		{
#if ANDROID
			var resolver = Android.App.Application.Context.ContentResolver;
			return Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#elif IOS
			return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
			return false;
#endif
		}
	}
}
