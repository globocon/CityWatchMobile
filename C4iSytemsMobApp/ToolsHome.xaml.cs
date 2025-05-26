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
}