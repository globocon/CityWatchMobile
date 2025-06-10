namespace C4iSytemsMobApp;

public partial class ToolsHome : ContentPage
{
	public ToolsHome()
	{
		InitializeComponent();
	}

    private async void OnButtonClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {

            string buttonText = btn.Text;
            Application.Current.MainPage = new ToolsPage(buttonText);
          
           
        }
    }


    private void OnBackClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage(true);
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        // Navigate to your app's home page
        // Example:
        Application.Current.MainPage = new MainPage();
    }
}