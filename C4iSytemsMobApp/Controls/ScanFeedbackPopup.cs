using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;

namespace C4iSytemsMobApp.Controls
{
    public enum ScanFeedbackKind
    {
        Success,
        Error,
        Offline
    }

    /// <summary>
    /// P4#153: scan confirmation the guard can actually read - big tick, the site the
    /// tag belongs to as the headline, tag label under it. Replaces the 35-char toasts
    /// for NFC/BLE scans ONLY; every other toast in the app is untouched.
    /// Auto-closes after the final state; the Close button and tapping outside work at
    /// any time, so it can never hold up a patrol.
    /// </summary>
    public class ScanFeedbackPopup : Popup
    {
        private const string SuccessColor = "#1D9E75";
        private const string ErrorColor = "#D2453B";
        private const string OfflineColor = "#BA7517";

        private readonly Label _statusLabel;
        private readonly Border _iconCircle;
        private readonly Label _iconGlyph;
        private bool _closed;

        public ScanFeedbackPopup(string headline, string detail, string status, ScanFeedbackKind kind)
        {
            // Toolkit v15 popups: surface transparency and tap-outside dismissal are
            // defaults / PopupOptions concerns now, not popup properties.
            _iconGlyph = new Label
            {
                Text = kind == ScanFeedbackKind.Error ? "✕" : "✓",
                TextColor = Colors.White,
                FontSize = 34,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            _iconCircle = new Border
            {
                WidthRequest = 64,
                HeightRequest = 64,
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb(KindColor(kind)),
                StrokeShape = new RoundRectangle { CornerRadius = 32 },
                HorizontalOptions = LayoutOptions.Center,
                Content = _iconGlyph
            };

            var headlineLabel = new Label
            {
                Text = headline,
                TextColor = Color.FromArgb("#212529"),
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap
            };

            _statusLabel = new Label
            {
                Text = status,
                TextColor = Color.FromArgb("#6C757D"),
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { _iconCircle, headlineLabel }
            };

            if (!string.IsNullOrWhiteSpace(detail))
            {
                stack.Children.Add(new Label
                {
                    Text = detail,
                    TextColor = Color.FromArgb("#495057"),
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                });
            }

            stack.Children.Add(_statusLabel);

            var closeButton = new Button
            {
                Text = "Close",
                BackgroundColor = Color.FromArgb("#E9ECEF"),
                TextColor = Color.FromArgb("#212529"),
                CornerRadius = 8,
                HeightRequest = 44,
                Margin = new Thickness(0, 6, 0, 0)
            };
            closeButton.Clicked += (s, e) => SafeClose();
            stack.Children.Add(closeButton);

            Content = new Border
            {
                BackgroundColor = Colors.White,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(24, 26, 24, 18),
                WidthRequest = 300,
                Content = stack
            };
        }

        private static string KindColor(ScanFeedbackKind kind) => kind switch
        {
            ScanFeedbackKind.Error => ErrorColor,
            ScanFeedbackKind.Offline => OfflineColor,
            _ => SuccessColor
        };

        /// <summary>Update the status line and, on failure, the icon - then auto-close.</summary>
        public void Complete(bool success, string statusText, int autoCloseMs = 2000)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _statusLabel.Text = statusText;
                    if (!success)
                    {
                        _iconCircle.BackgroundColor = Color.FromArgb(ErrorColor);
                        _iconGlyph.Text = "✕";
                    }
                }
                catch { }
            });
            AutoCloseAfter(autoCloseMs);
        }

        public void AutoCloseAfter(int ms)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(ms);
                SafeClose();
            });
        }

        private void SafeClose()
        {
            if (_closed) return;
            _closed = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { _ = CloseAsync(); } catch { }
            });
        }
    }

    /// <summary>
    /// One entry point for scan feedback: beep + vibrate + popup. A new scan replaces
    /// the previous popup so rapid consecutive scans never stack.
    /// </summary>
    public static class ScanFeedback
    {
        private static ScanFeedbackPopup _current;

        public static ScanFeedbackPopup Show(Page page, string headline, string detail, string status, ScanFeedbackKind kind, int? autoCloseMs = null)
        {
            var popup = new ScanFeedbackPopup(headline, detail, status, kind);
            var previous = _current;
            _current = popup;

            PlayCue(kind);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    previous?.AutoCloseAfter(0);
                    page.ShowPopup(popup);
                }
                catch { }
            });
            if (autoCloseMs.HasValue) popup.AutoCloseAfter(autoCloseMs.Value);
            return popup;
        }

        /// <summary>The scan reply label reads "[Site Name] Tag label [PCAR NFC]" when the
        /// tag belongs to another site - the popup shows the site as the headline, so the
        /// duplicate prefix comes off the detail line.</summary>
        public static string DetailWithoutSitePrefix(string tagInfoLabel, string tagSiteName)
        {
            if (string.IsNullOrWhiteSpace(tagInfoLabel)) return string.Empty;
            if (string.IsNullOrWhiteSpace(tagSiteName)) return tagInfoLabel;
            var prefix = $"[{tagSiteName}] ";
            return tagInfoLabel.StartsWith(prefix) ? tagInfoLabel.Substring(prefix.Length) : tagInfoLabel;
        }

        private static void PlayCue(ScanFeedbackKind kind)
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(kind == ScanFeedbackKind.Error ? 250 : 100));
            }
            catch { }

#if ANDROID
            try
            {
                var tone = kind == ScanFeedbackKind.Error ? Android.Media.Tone.SupError : Android.Media.Tone.PropBeep;
                var generator = new Android.Media.ToneGenerator(Android.Media.Stream.Notification, 80);
                generator.StartTone(tone, 150);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    try { generator.Release(); generator.Dispose(); } catch { }
                });
            }
            catch { }
#endif
        }
    }
}
