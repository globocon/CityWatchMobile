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
            // Activate audio session (required for volume monitoring to work)
            //var audioSession = AVAudioSession.SharedInstance();
            //audioSession.SetActive(true, out _);

            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.MixWithOthers);
            session.SetActive(true);

            var volumeView = new MPVolumeView
            {
                ShowsVolumeSlider = false,
                ShowsRouteButton = false
            };

            // Attach to the current window (not KeyWindow!)
            UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                var window = UIApplication.SharedApplication
                    .ConnectedScenes
                    .OfType<UIWindowScene>()
                    .SelectMany(s => s.Windows)
                    .FirstOrDefault(w => w.IsKeyWindow);

                window?.AddSubview(volumeView);
            });

            foreach (var view in volumeView.Subviews)
            {
                if (view is UISlider slider)
                {
                    _lastVolume = slider.Value;

                    slider.ValueChanged += (sender, e) =>
                    {
                        var newVolume = slider.Value;

                        if (newVolume > _lastVolume)
                            VolumeUpPressed?.Invoke(this, EventArgs.Empty);
                        else if (newVolume < _lastVolume)
                            VolumeDownPressed?.Invoke(this, EventArgs.Empty);

                        _lastVolume = newVolume;
                    };
                }
            }
        }
    }
}
