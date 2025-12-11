using C4iSytemsMobApp.Interface;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Plugin.BLE.Abstractions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;


namespace C4iSytemsMobApp;

public partial class PcarRute : ContentPage
{
    private readonly PcarRouteViewModel _viewModel;
    private readonly IDeviceInfoService infoService;

    private VisitModel _selectedVisit;
    private string deviceid;
    private int? _clientSiteId;
    private int? _userId;
    private int? _guardId;

    //public PcarRute()
    //{
    //    InitializeComponent();

    //    LoadSecureData();
    //    NavigationPage.SetHasNavigationBar(this, false);

    //    infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();

    //    deviceid = infoService?.GetDeviceId();
    //    // Show deviceId in the label
    //    DeviceIdLabel.Text = $"Device ID: {deviceid}";
    //    _viewModel = new PcarRouteViewModel();
    //    BindingContext = _viewModel;
    //    CurrentDateLabel.Text = DateTime.Now.ToString("ddd d MMM yyyy");
    //    Task.Run(async () => await _viewModel.LoadRealData(deviceid));
    //}

    public PcarRute()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _viewModel = new PcarRouteViewModel();
        BindingContext = _viewModel;

        infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();
        deviceid = infoService?.GetDeviceId();
      
        CurrentDateLabel.Text = DateTime.Now.ToString("ddd d MMM yyyy");

        // Load secure storage FIRST
        _ = InitializePage();
    }

    private async Task InitializePage()
    {
        await LoadSecureData();   // ENSURES IDs exist before API call

        // Now load real data properly ON UI THREAD
        await _viewModel.LoadRealData(deviceid);
    }

    private async Task LoadSecureData()
    {
        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        _clientSiteId = clientSiteId;
        _guardId = guardId;
        _userId = userId;
    }

    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(Preferences.Get("GuardId", ""), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", ""), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", ""), out int userId);

        if (guardId <= 0)
        {
            await DisplayAlert("Error", "Guard ID not found. Please validate the License Number first.", "OK");
            return (-1, -1, -1);
        }
        if (clientSiteId <= 0)
        {
            await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
            return (-1, -1, -1);
        }
        if (userId <= 0)
        {
            await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
            return (-1, -1, -1);
        }

        return (guardId, clientSiteId, userId);
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }

    protected override bool OnBackButtonPressed()
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
        return true;


    }

    // Called by ViewModel when user checks a visit
    public void OpenTimePopup(VisitModel visit)
    {
        _selectedVisit = visit;

        // Reset first
        TimeOnSitePicker.IsEnabled = true;
        TimeOffSitePicker.IsEnabled = true;
        SaveTimeButton.IsEnabled = true;  // Enable by default

        // Default current time
        TimeSpan now = DateTime.Now.TimeOfDay;
        TimeOnSitePicker.Time = now;
        TimeOffSitePicker.Time = now;

        if (visit.IsCheckedToday)
        {
            // Show saved values
            if (!string.IsNullOrEmpty(visit.SavedTimeOnSite))
                TimeOnSitePicker.Time = TimeSpan.Parse(visit.SavedTimeOnSite);

            if (!string.IsNullOrEmpty(visit.SavedTimeOffSite))
                TimeOffSitePicker.Time = TimeSpan.Parse(visit.SavedTimeOffSite);

            // Disable editing
            TimeOnSitePicker.IsEnabled = false;
            TimeOffSitePicker.IsEnabled = false;

            // Disable save button
            SaveTimeButton.IsEnabled = false;
        }
        else
        {
            SaveTimeButton.IsEnabled = true;
        }

        EditTimePopupOverlay.IsVisible = true;
    }



    private async void OnTimePopupSaveClicked(object sender, EventArgs e)
    {
        if (_selectedVisit != null)
        {

            // Validate time
            if (TimeOnSitePicker.Time >= TimeOffSitePicker.Time)
            {
                await DisplayAlert("Validation Error", "Time On Site must be earlier than Time Off Site.", "OK");
                return;
            }

            if (_guardId == null || _clientSiteId == null || _userId == null ||
                _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0)
                return;

            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");

            _selectedVisit.TimeOnSite = TimeOnSitePicker.Time;
            _selectedVisit.TimeOffSite = TimeOffSitePicker.Time;

            try
            {
                using var http = new HttpClient();
                var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SaveVisitTime";

                var payload = new
                {
                    SmartWandId = _selectedVisit.SmartWandId,
                    SiteId = _selectedVisit.SiteId,
                    DayName = _selectedVisit.DayName,
                    PcarRouteId = _selectedVisit.PcarRouteId,
                    PcarRouteDetailsId = _selectedVisit.PcarRouteDetailsId,

                    VisitName = _selectedVisit.VisitName,
                    VisitNumber = 1,

                    GuardId = _guardId,

                    //Added fields
                    GpsCoordinates = gpsCoordinates,
                    LoginUserId = _userId,
                    LoginSiteId = _clientSiteId,

                    TimeOn = _selectedVisit.TimeOnSite?.ToString(@"hh\:mm"),
                    TimeOff = _selectedVisit.TimeOffSite?.ToString(@"hh\:mm")
                };

                var response = await http.PostAsJsonAsync(url, payload);
                var result = await response.Content.ReadFromJsonAsync<ApiResponseNew>();

                if (result != null)
                {
                    string toastMessage =
                        $"Success: {result.Success}\n" +
                        $"Message: {result.Message}\n" +
                        $"Visit: {_selectedVisit.VisitName}";

                    await Toast.Make(toastMessage, ToastDuration.Long).Show();
                }
                else
                {
                    await Toast.Make("Null response from API", ToastDuration.Long).Show();
                }
            }
            catch (Exception ex)
            {
                await Toast.Make("Error: " + ex.Message, ToastDuration.Long).Show();
            }
        }

        EditTimePopupOverlay.IsVisible = false;
        await ReloadRouteAsync();
        // <- refresh UI
    }



    private async void OnTimePopupCancelClicked(object sender, EventArgs e)
    {
        if (_selectedVisit != null)
            _selectedVisit.RevertCheckState();

        EditTimePopupOverlay.IsVisible = false;

        // Fix UI state mismatch
        await ReloadRouteAsync();
    }


    private async Task ReloadRouteAsync()
    {
        await _viewModel.LoadRealData(deviceid);
    }
}



/* =============================================================
   VIEWMODEL
   ============================================================= */

public class PcarRouteViewModel : INotifyPropertyChanged
{
    private VisitModel _currentVisit;
    public ObservableCollection<SiteModel> SiteList { get; set; } = new();
    public event PropertyChangedEventHandler PropertyChanged;

    protected void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    public async Task LoadRealData(string deviceId)
    {
        try
        {
           
            using var http = new HttpClient();
            // Pass the real deviceId from the method parameter
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetPcarDetails?deviceId={deviceId}";
            //var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetPcarDetails?deviceId=7ea8b144337cb38c";

            var response = await http.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PcarRouteResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!response.IsSuccessStatusCode)
            {
                await Toast.Make(result?.Message ?? "Something went wrong", ToastDuration.Long).Show();
                return;
            }

            if (result != null && result.Success == false)
            {
                await Toast.Make(result.Message, ToastDuration.Long).Show();
                return;
            }

            if (result != null && result.Success == true)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SiteList.Clear();

                    foreach (var site in result.Data)
                    {
                        var siteModel = new SiteModel
                        {
                            SiteName = site.SiteName,
                            Address = site.Address,
                            GPSLocation = site.GPSLocation,
                            Visits = new ObservableCollection<VisitModel>()
                        };

                        foreach (var v in site.Visits)
                        {
                            var vm = new VisitModel(
     v.VisitName,
     site.SmartWandId,
     site.SiteId,
     site.DayName,
     site.PcarRouteId,
     site.PcarRouteDetailsId,
     v.IsCheckedToday,
     v.SavedTimeOnSite,
     v.SavedTimeOffSite
 );
                            vm.OnChecked += Visit_OnChecked;  //  Attach popup event
                            siteModel.Visits.Add(vm);
                        }

                        SiteList.Add(siteModel);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            await Toast.Make("Error: " + ex.Message, ToastDuration.Long).Show();
        }
    }

    // Fires when a checkbox is clicked

  
    private void Visit_OnChecked(object sender, EventArgs e)
    {
        if (sender is VisitModel visit)
        {
           

            _currentVisit = visit;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var page = (PcarRute)Application.Current.MainPage.Navigation.NavigationStack.Last();
                page.OpenTimePopup(visit);
            });
        }
    }
}



/* =============================================================
   MODELS
   ============================================================= */

public class SiteModel
{
    public string SiteName { get; set; }
    public string Address { get; set; }
    public string GPSLocation { get; set; }
    public ObservableCollection<VisitModel> Visits { get; set; }

    public Command OpenMapsCommand =>
    new Command(async () =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(GPSLocation))
                return;

            var query = GPSLocation.Trim(); // "12.3456, 76.5432"

            // Opens Google Maps app if available, otherwise browser
            var url = $"https://www.google.com/maps/search/?api=1&query={query}";
            await Launcher.OpenAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Map Open Error: " + ex.Message);
        }
    });


}



public class VisitModel : INotifyPropertyChanged
{
    private bool _isChecked;

    public bool IsCheckedToday { get; set; } = false;
    public int PcarRouteId { get; set; }          // Master route ID
    public int PcarRouteDetailsId { get; set; }   // Route details ID
    public string VisitName { get; set; }
    public int SmartWandId { get; set; }
    public int SiteId { get; set; }
    public string DayName { get; set; }
    public TimeSpan? TimeOnSite { get; set; }
    public TimeSpan? TimeOffSite { get; set; }

    public string  SavedTimeOnSite { get; set; }
    public string SavedTimeOffSite { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler OnChecked;

    public VisitModel(
     string visitName,
     int smartWandId,
     int siteId,
     string dayName,
     int pcarRouteId,
     int pcarRouteDetailsId,
     bool isCheckedToday = false,
     string savedTimeOnSite = null,
     string savedTimeOffSite = null)
    {
        VisitName = visitName;
        SmartWandId = smartWandId;
        SiteId = siteId;
        DayName = dayName;
        PcarRouteId = pcarRouteId;
        PcarRouteDetailsId = pcarRouteDetailsId;

        // IMPORTANT: assign API value first
        IsCheckedToday = isCheckedToday;

        // Initialize checkbox state
        _isChecked = isCheckedToday;
        IsChecked = isCheckedToday;

        SavedTimeOnSite = savedTimeOnSite;
        SavedTimeOffSite = savedTimeOffSite;

        ToggleCheckCommand = new Command(() => Toggle());
    }


    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            Raise(nameof(IsChecked));
            Raise(nameof(StatusColor));
        }
    }

    public string StatusColor =>
        IsChecked ? "#A8E6A1" : "#EFEFEF";

    public Command ToggleCheckCommand { get; }

    private bool _previousCheckedState;

    private void Toggle()
    {
        // Save previous state before modifying
        _previousCheckedState = IsChecked;

        // If already checked -> open popup (readonly mode)
        if (IsChecked)
        {
            OnChecked?.Invoke(this, EventArgs.Empty);
            return;
        }

        // If not checked -> check and open popup
        IsChecked = true;
        OnChecked?.Invoke(this, EventArgs.Empty);
    }
    public void RevertCheckState()
    {
        IsChecked = _previousCheckedState;
    }
    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}




/* =============================================================
   API RESPONSE MODELS
   ============================================================= */

public class PcarRouteResult
{
    public bool? Success { get; set; }
    public string Message { get; set; }
    public List<PcarRouteResponse> Data { get; set; }
}

public class PcarRouteResponse
{
    public int SmartWandId { get; set; }
    public int SiteId { get; set; }
    public int PcarRouteId { get; set; }           // New
    public int PcarRouteDetailsId { get; set; }    // New

    public string GPSLocation { get; set; }  // ADD THIS
    public string DayName { get; set; }
    public string SiteName { get; set; }
    public string Address { get; set; }
    public int VisitCount { get; set; }
    public List<VisitDto> Visits { get; set; }
}

public class VisitDto
{
    public string VisitName { get; set; }
    public bool IsCheckedToday { get; set; }  // <-- New
    public string SavedTimeOnSite { get; set; }
    public string SavedTimeOffSite { get; set; }
}

    public class ApiResponseNew
{
    public bool Success { get; set; }      // true if save succeeded
    public string Message { get; set; }    // any info or error message
    public object Data { get; set; }       // optional, can hold additional returned info
}
