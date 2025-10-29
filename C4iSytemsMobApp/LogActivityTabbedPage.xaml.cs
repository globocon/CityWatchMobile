using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class LogActivityTabbedPage
{
    private readonly ILogBookServices _logBookServices;
    bool showCustomLogs = false;
    bool showPatrolCarLogs = false;
    public LogActivityTabbedPage()
	{
        InitializeComponent();
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
        
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTabsAsync();

        // Conditionally add tabs       
        if (showCustomLogs)
            Children.Add(new CustomContractPage { Title = "Custom Logs" });

        if (showPatrolCarLogs)
            Children.Add(new PatrolCarPage { Title = "Patrol Car Logs" });
    }

    private async Task LoadTabsAsync()
    {

        try
        {
            var CustomLogRrows = await _logBookServices.GetCustomFieldLogsAsync();
            showCustomLogs = (CustomLogRrows != null && CustomLogRrows.Count > 0);
        }
        catch (Exception)
        {
            //await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }

        try
        {
            var PatrolCarRows = await _logBookServices.GetPatrolCarLogsAsync();
            showPatrolCarLogs = (PatrolCarRows != null && PatrolCarRows.Count > 0);
        }
        catch (Exception)
        {
            //await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }
    }
}