using Android.App;
using Android.Runtime;

namespace ForumCenter;

[Application(
    UsesCleartextTraffic = true,
    Label = "论坛中心")]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
