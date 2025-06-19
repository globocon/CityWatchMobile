using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Platforms.Android.Services;

namespace C4iSytemsMobApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    
    public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
    {
        var volumeService = MauiApplication.Current.Services.GetService<IVolumeButtonService>() as VolumeButtonService;
        if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
        {            
            if (volumeService != null && volumeService.OnKeyDown(keyCode, e))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return base.OnKeyDown(keyCode, e);
        }
    }        
    
}
