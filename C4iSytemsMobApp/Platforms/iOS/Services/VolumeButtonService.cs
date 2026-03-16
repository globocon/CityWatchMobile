using AVFoundation;
using C4iSytemsMobApp.Interface;
using CoreFoundation;
using Foundation;
using MediaPlayer;
using UIKit;

namespace C4iSytemsMobApp.Platforms.iOS.Services
{
    public class VolumeButtonService : IVolumeButtonService
    {
        public event EventHandler? VolumeUpPressed;
        public event EventHandler? VolumeDownPressed;

        private float _lastVolume = 0.5f;
        private NSTimer? _volumePollTimer;

        // Suppress duplicate events when both the MPVolumeView callback and poll timer detect the same change.
        private DateTime _lastVolumeEventUtc = DateTime.MinValue;
        private static readonly TimeSpan VolumeEventSuppressDuration = TimeSpan.FromMilliseconds(80);

        public VolumeButtonService()
        {
            // Activate audio session (required for volume monitoring to work)
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.MixWithOthers);
            session.SetActive(true);

            // Note: iOS only fires ValueChanged when the volume slider value actually changes.
            // To reliably capture repeated volume button presses, we keep the slider present but hidden.
            // MPVolumeView needs the slider present so ValueChanged can fire.
            // We keep it offscreen and nearly invisible so it doesn't affect the UI.
            var volumeView = new MPVolumeView
            {
                Alpha = 0.01f,            // make the view effectively invisible
                Frame = new CoreGraphics.CGRect(-1000, -1000, 0, 0) // keep offscreen
            };

            // Attach to the active window. Using KeyWindow (deprecated in iOS 13+) is still the most reliable
            // way to ensure the MPVolumeView is in a visible hierarchy for value change events.
            UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                // We access KeyWindow/Windows via reflection to avoid compile-time obsoleted APIs.
                var shared = UIApplication.SharedApplication;
                UIWindow? window = null;

                var keyWindowProp = shared.GetType().GetProperty("KeyWindow");
                if (keyWindowProp != null)
                {
                    window = keyWindowProp.GetValue(shared) as UIWindow;
                }

                if (window == null)
                {
                    var windowsProp = shared.GetType().GetProperty("Windows");
                    if (windowsProp != null)
                    {
                        var windows = windowsProp.GetValue(shared) as IEnumerable<UIWindow>;
                        window = windows?.FirstOrDefault();
                    }
                }

                window?.AddSubview(volumeView);
            });

            UISlider? slider = null;

            foreach (var view in volumeView.Subviews)
            {
                if (view is UISlider foundSlider)
                {
                    slider = foundSlider;

                    // Initialize at midpoint
                    slider.Value = 0.5f;
                    _lastVolume = 0.5f;

                    slider.ValueChanged += (sender, e) =>
                    {
                        var newVolume = slider.Value;
                        var isUp = newVolume > _lastVolume;
                        var isDown = newVolume < _lastVolume;

                        // Ignore when we're simply resetting to midpoint.
                        if (!isUp && !isDown)
                        {
                            _lastVolume = newVolume;
                            return;
                        }

                        if (isUp)
                            RaiseVolumeEvent(true);
                        else if (isDown)
                            RaiseVolumeEvent(false);

                        // Reset quickly so rapid presses can still register.
                        slider.SetValue(0.5f, false);
                        _lastVolume = 0.5f;
                    };

                    break;
                }
            }

            // Poll output volume to catch very fast button presses, since MPVolumeView events can be coalesced.
            if (slider != null)
            {
                // Poll output volume to catch very fast button presses, since MPVolumeView events can be coalesced.
                _volumePollTimer = NSTimer.CreateRepeatingScheduledTimer(0.005, _ =>
                {
                    var currentVolume = AVAudioSession.SharedInstance().OutputVolume;
                    if (currentVolume == _lastVolume)
                        return;

                    var isUp = currentVolume > _lastVolume;
                    var isDown = currentVolume < _lastVolume;
                    _lastVolume = currentVolume;

                    if (isUp)
                        RaiseVolumeEvent(true);
                    else if (isDown)
                        RaiseVolumeEvent(false);

                    // Reset to midpoint for the next press.
                    UIApplication.SharedApplication.InvokeOnMainThread(() =>
                    {
                        slider.SetValue(0.5f, false);
                        _lastVolume = 0.5f;
                    });
                });
            }
        }

        private void RaiseVolumeEvent(bool isUp)
        {
            var now = DateTime.UtcNow;
            if (now - _lastVolumeEventUtc < VolumeEventSuppressDuration)
                return;

            _lastVolumeEventUtc = now;

            if (isUp)
                VolumeUpPressed?.Invoke(this, EventArgs.Empty);
            else
                VolumeDownPressed?.Invoke(this, EventArgs.Empty);
        }
    }
}
