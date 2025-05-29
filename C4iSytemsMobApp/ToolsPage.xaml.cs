using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace C4iSytemsMobApp;

public partial class ToolsPage : ContentPage
{

    public ObservableCollection<StaffTool> StaffTools { get; } = new();
    public ToolsPage(string type)
	{

        
       InitializeComponent();
        TitleLabel.Text = type;
        BindingContext = this;
        LoadStaffTools(type);

    }

    private async void LoadStaffTools(string type)
    {
        try
        {
            var httpClient = new HttpClient();

            // Your base API url, adjust as needed
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetStaffTools?type={Uri.EscapeDataString(type)}";

            var tools = await httpClient.GetFromJsonAsync<List<StaffTool>>(url);

            if (tools != null)
            {
                StaffTools.Clear();

                foreach (var tool in tools)
                {
                    // If you want to construct full URL for Hyperlink or other processing, do it here
                    // e.g. tool.Hyperlink = $"https://yourdomain.com/somedir/{Uri.EscapeDataString(tool.Hyperlink)}";

                    StaffTools.Add(tool);
                }
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load tools: {ex.Message}", "OK");
        }
    }



    private async void OnHyperlinkTapped(object sender, EventArgs e)
    {
        if (sender is Label label && Uri.TryCreate(label.Text, UriKind.Absolute, out var uri))
        {
            await Launcher.OpenAsync(uri);
        }
    }


  

    private async void OnHyperlinkIconClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton btn && btn.CommandParameter is string url && !string.IsNullOrWhiteSpace(url))
        {
            await Launcher.Default.OpenAsync(url);
        }
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new ToolsHome();
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        // Navigate to your app's home page
        // Example:
        Application.Current.MainPage = new MainPage();
    }
}

public class StaffTool
{
    public int Id { get; set; }
    public int ClientSiteLinksTypeId { get; set; }
    public string Title { get; set; }
    public string Hyperlink { get; set; }
    public string State { get; set; }

    // You can add extra properties if needed
}