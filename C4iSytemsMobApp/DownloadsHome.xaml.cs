namespace C4iSytemsMobApp;

public partial class DownloadsHome : ContentPage
{
	public DownloadsHome()
	{
		InitializeComponent();
	}

    private async void OnCompanySopClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new DownloadsPage(1);
       
    }

    private async void OnTrainingClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new DownloadsPage(2);
        
    }

    private async void OnFormsClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new DownloadsPage(3);
        
    }
    private void OnBackClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage(true);
    }


    private async void OnHomeClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage();
    }
}