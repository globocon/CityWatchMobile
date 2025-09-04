using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Platforms.Android.Services;
using Plugin.NFC;

namespace C4iSytemsMobApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        // Plugin NFC: Initialization before base.OnCreate(...) (Important on .NET MAUI)
        CrossNFC.Init(this);

        base.OnCreate(savedInstanceState);
        //ZXing.Net.Maui.Controls.Platform.Init();
    }
    protected override void OnResume()
    {
        base.OnResume();

        // Plugin NFC: Restart NFC listening on resume (needed for Android 10+) 
        CrossNFC.OnResume();
    }
    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);

        // Plugin NFC: Tag Discovery Interception
        CrossNFC.OnNewIntent(intent);
    }

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
