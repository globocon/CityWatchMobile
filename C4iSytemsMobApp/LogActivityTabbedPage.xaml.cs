using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class LogActivityTabbedPage
{
    private readonly ILogBookServices _logBookServices;
    private readonly IScanDataDbServices _scanDataDbService;
    bool showCustomLogs = false;
    bool showPatrolCarLogs = false;
    //bool showBLEiBeaconTab = false;
    public LogActivityTabbedPage()
    {
        InitializeComponent();
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
        _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTabsAsync();

        // Conditionally add tabs       
        //if (showBLEiBeaconTab)
        //    Children.Add(new iBeaconScannerPage { Title = "iBeacon Scanner" });

        if (showCustomLogs)
            Children.Add(new CustomContractPage { Title = "Custom Logs" });

        if (showPatrolCarLogs)
            Children.Add(new PatrolCarPage { Title = "Patrol Car Logs" });
    }

    private async Task LoadTabsAsync()
    {
        //try
        //{            
        //    string isNfcEnabledForSiteLocalStored = Preferences.Get("iBeaconOnboarded", "");            
        //    if (!string.IsNullOrEmpty(isNfcEnabledForSiteLocalStored) && bool.TryParse(isNfcEnabledForSiteLocalStored, out showBLEiBeaconTab))
        //    {
        //    }
        //}
        //catch (Exception)
        //{

        //}

        try
        {
            if (App.IsOnline)
            {
                var CustomLogRows = await _logBookServices.GetCustomFieldLogsAsync();
                showCustomLogs = (CustomLogRows != null && CustomLogRows.Count > 0);
            }
            else
            {
                var CustomLogRowsCache = await _scanDataDbService.GetCustomFieldLogCacheList();
                showCustomLogs = (CustomLogRowsCache != null && CustomLogRowsCache.Count > 0);
            }
        }
        catch (Exception)
        {
            //await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }

        try
        {
            if (App.IsOnline)
            {
                var PatrolCarRows = await _logBookServices.GetPatrolCarLogsAsync();
                showPatrolCarLogs = (PatrolCarRows != null && PatrolCarRows.Count > 0);
            }
            else
            {
                var PatrolCarRowsCache = await _scanDataDbService.GetPatrolCarCacheList();
                showPatrolCarLogs = (PatrolCarRowsCache != null && PatrolCarRowsCache.Count > 0);
            }
            
        }
        catch (Exception)
        {
            //await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }
    }
}