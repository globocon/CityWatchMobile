using AVFoundation;
using MediaPlayer;
using UIKit;
using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp.Platforms.iOS.Services
{
    public class VolumeButtonService : IVolumeButtonService
    {
        public event EventHandler VolumeUpPressed;
        public event EventHandler VolumeDownPressed;

        private float _lastVolume = 0.5f;

        public VolumeButtonService()
        {
            var volumeView = new MPVolumeView
            {
                ShowsVolumeSlider = false,
                ShowsRouteButton = false
            };

            UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                UIApplication.SharedApplication.KeyWindow?.AddSubview(volumeView);
            });

            foreach (var view in volumeView.Subviews)
            {
                if (view is UISlider slider)
                {
                    _lastVolume = slider.Value;

                    slider.ValueChanged += (sender, e) =>
                    {
                        float newVolume = slider.Value;

                        if (newVolume > _lastVolume)
                        {
                            VolumeUpPressed?.Invoke(this, EventArgs.Empty);
                        }
                        else if (newVolume < _lastVolume)
                        {
                            VolumeDownPressed?.Invoke(this, EventArgs.Empty);
                        }

                        _lastVolume = newVolume;
                    };
                }
            }
        }
    }
}
