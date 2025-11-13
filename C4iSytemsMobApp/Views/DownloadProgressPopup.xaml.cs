
namespace C4iSytemsMobApp.Views;

public partial class DownloadProgressPopup : ContentPage
{
    public DownloadProgressPopup()
    {
        InitializeComponent();
    }

    public void UpdateProgress(double percent)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProgressBarDownload.Progress = percent / 100;
            LblPercent.Text = $"{percent:F0}%";
        });
    }
}
