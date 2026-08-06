using Android.App;
using Android.Content.PM;

namespace ForumCenter;

[Activity(
    MainLauncher = true,
    Theme = "@style/Maui.SplashTheme",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
