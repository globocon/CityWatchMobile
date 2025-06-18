using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{

    public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
    {
        var volumeService = DependencyService.Get<IVolumeButtonService>() as VolumeButtonService;
        if (volumeService != null && volumeService.OnKeyDown(keyCode, e))
        {
            return true;
        }
        return base.OnKeyDown(keyCode, e);
    }

    public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
    {
        var volumeService = DependencyService.Get<IVolumeButtonService>() as VolumeButtonService;
        if (volumeService != null && volumeService.OnKeyDown(keyCode, e))
        {
            return true;
        }
        return base.OnKeyUp(keyCode, e);
    }
}
