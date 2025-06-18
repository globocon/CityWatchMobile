using Android.Views;
using C4iSytemsMobApp.Interface;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;

namespace C4iSytemsMobApp.Platforms.Android.Services
{
    public class VolumeButtonService : IVolumeButtonService
    {
        public event EventHandler VolumeUpPressed;
        public event EventHandler VolumeDownPressed;

        public bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.VolumeDown)
            {
                VolumeDownPressed?.Invoke(this, EventArgs.Empty);
                return true;
            }
            if (keyCode == Keycode.VolumeUp)
            {
                VolumeUpPressed?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }
                
    }
}