using Android.Views;
using C4iSytemsMobApp.Interface;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;

[assembly: Dependency(typeof(VolumeButtonService))]
public class VolumeButtonService : IVolumeButtonService
{    
    public event EventHandler VolumeUpPressed;
    public event EventHandler VolumeDownPressed;

    public bool OnKeyDown(Keycode keyCode, KeyEvent e)
    {
        if (keyCode == Keycode.VolumeUp || keyCode == Keycode.VolumeDown)
        {
            VolumeDownPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    public bool OnKeyUp(Keycode keyCode, KeyEvent e)
    {
        if (keyCode == Keycode.VolumeUp || keyCode == Keycode.VolumeDown)
        {
            VolumeUpPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }
}