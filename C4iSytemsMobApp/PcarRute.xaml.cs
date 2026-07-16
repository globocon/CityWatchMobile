using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using C4iSytemsMobApp.Helpers;
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
    private readonly IScannerControlServices _scannerControlServices;

    private VisitModel _selectedVisit;
    //private string deviceid;
    private int? _clientSiteId;
    private int? _userId;
    private int? _guardId;
    private DateTime _selectedDate;
    private int? _smartWandId;

    public int? GuardId => _guardId;
    public int? UserId => _userId;
    public int? ClientSiteId => _clientSiteId;
    public DateTime SelectedDate => _selectedDate;
    public int? PageSmartWandId => _smartWandId;

    public PcarRute()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
        _selectedDate = DateTime.Today;
        //deviceid = App.DeviceId;
        _viewModel = new PcarRouteViewModel();
        BindingContext = _viewModel;



        UpdateDayNavigationLabel();

        // Load secure storage FIRST
        _ = InitializePage();
    }

    private async Task InitializePage()
    {
        await LoadSecureData();   // ENSURES IDs exist before API call

        await GetSmartWand();

        // Now load real data properly ON UI THREAD
        await _viewModel.LoadRealData(App.DeviceId, _selectedDate);
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
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Error", "Guard ID not found. Please validate the License Number first.", "OK");
            });
            return (-1, -1, -1);
        }
        if (clientSiteId <= 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
            });
            return (-1, -1, -1);
        }
        if (userId <= 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
            });
            return (-1, -1, -1);
        }

        return (guardId, clientSiteId, userId);
    }

    private async Task GetSmartWand()
    {
        _smartWandId = null;
        try
        {
            var swid = await _scannerControlServices.GetSmartWandByDeviceIdAsync();
            if (swid > 0)
                _smartWandId = swid;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error smartwand: {ex.Message}");
        }
    }

    private void UpdateDayNavigationLabel()
    {
        var today = DateTime.Today;
        string middleText = _selectedDate.ToString("d MMM");

        string leftPart = "";
        if ((_selectedDate - today).Days == 1) leftPart = "Today";
        else if ((_selectedDate - today).Days == 0) leftPart = "Yesterday";
        else if ((_selectedDate - today).Days == 2) leftPart = "Tomorrow";

        string rightPart = "";
        if ((_selectedDate - today).Days == -1) rightPart = "Today";
        else if ((_selectedDate - today).Days == 0) rightPart = "Tomorrow";
        else if ((_selectedDate - today).Days == -2) rightPart = "Yesterday";

        if (!string.IsNullOrEmpty(leftPart) && !string.IsNullOrEmpty(rightPart))
        {
            DayNavigationLabel.Text = $"{leftPart} - {middleText} - {rightPart}";
        }
        else if (!string.IsNullOrEmpty(leftPart))
        {
            DayNavigationLabel.Text = $"{leftPart} - {middleText}";
        }
        else if (!string.IsNullOrEmpty(rightPart))
        {
            DayNavigationLabel.Text = $"{middleText} - {rightPart}";
        }
        else
        {
            DayNavigationLabel.Text = middleText;
        }
    }

    private async void OnPreviousDayClicked(object sender, EventArgs e)
    {
        if (_selectedDate > DateTime.Today.AddDays(-1))
        {
            _selectedDate = _selectedDate.AddDays(-1);
            UpdateDayNavigationLabel();
            await ReloadRouteAsync();
        }
    }

    private async void OnNextDayClicked(object sender, EventArgs e)
    {
        if (_selectedDate < DateTime.Today.AddDays(1))
        {
            _selectedDate = _selectedDate.AddDays(1);
            UpdateDayNavigationLabel();
            await ReloadRouteAsync();
        }
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
        return true;
    }

    // Called by ViewModel when user checks a visit
    public void OpenTimePopup(VisitModel visit)
    {
        _selectedVisit = visit;

        // Default current time
        TimeSpan now = DateTime.Now.TimeOfDay;
        TimeOnSitePicker.Time = now;
        TimeOffSitePicker.Time = now;

        if (_selectedDate.Date == DateTime.Today.AddDays(1))
        {
            // Tomorrow: only allow cancel/delegate (no time entry)
            TimeOnSiteLayout.IsVisible = false;
            TimeOffSiteLayout.IsVisible = false;
            CancelVisitIconLayout.IsVisible = true;
            SaveTimeButton.IsEnabled = false;
        }
        else
        {
            // Reset visibility layouts to initial time-entry state
            TimeOnSiteLayout.IsVisible = true;
            TimeOffSiteLayout.IsVisible = true;
            CancelVisitIconLayout.IsVisible = true;

            TimeOnSiteCheckBox.IsEnabled = true;
            TimeOffSiteCheckBox.IsEnabled = true;
            TimeOnSitePicker.IsEnabled = false;
            TimeOffSitePicker.IsEnabled = false;
            SaveTimeButton.IsEnabled = true;

            if (visit.Status == PcarVisitStatusEnum.InProgress)
            {
                CancelVisitIconLayout.IsVisible = false;

                if (!string.IsNullOrEmpty(visit.SavedTimeOnSite))
                {
                    TimeOnSiteCheckBox.IsChecked = true;
                    TimeOnSiteCheckBox.IsEnabled = false; // Cannot uncheck start time
                    TimeOnSitePicker.Time = TimeSpan.Parse(visit.SavedTimeOnSite);
                    TimeOnSitePicker.IsEnabled = false;
                }

                TimeOffSiteCheckBox.IsChecked = false;
                TimeOffSiteCheckBox.IsEnabled = true;
                TimeOffSitePicker.IsEnabled = false;
                SaveTimeButton.IsEnabled = true;
            }
            else if (visit.Status == PcarVisitStatusEnum.Completed || visit.Status == PcarVisitStatusEnum.CancelledOrDelegated)
            {
                if (!string.IsNullOrEmpty(visit.SavedTimeOnSite))
                {
                    TimeOnSiteCheckBox.IsChecked = true;
                    TimeOnSitePicker.Time = TimeSpan.Parse(visit.SavedTimeOnSite);
                }
                if (!string.IsNullOrEmpty(visit.SavedTimeOffSite))
                {
                    TimeOffSiteCheckBox.IsChecked = true;
                    TimeOffSitePicker.Time = TimeSpan.Parse(visit.SavedTimeOffSite);
                }
                CancelVisitIconLayout.IsVisible = false;

                // Disable editing for saved visits
                TimeOnSiteCheckBox.IsEnabled = false;
                TimeOffSiteCheckBox.IsEnabled = false;
                TimeOnSitePicker.IsEnabled = false;
                TimeOffSitePicker.IsEnabled = false;
                SaveTimeButton.IsEnabled = false;
            }
            else
            {
                TimeOnSiteCheckBox.IsChecked = true;
                TimeOffSiteCheckBox.IsChecked = false;
                SaveTimeButton.IsEnabled = true;
            }
        }

        EditTimePopupOverlay.IsVisible = true;
    }

    private async void OnCancelVisitTapped(object sender, EventArgs e)
    {
        if (_selectedVisit == null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Cancel Visit",
            "Are you sure you want to cancel this visit and send it to other PCARs?",
            "Yes", "No");

        if (confirm)
        {
            EditTimePopupOverlay.IsVisible = false;
            await _viewModel.CancelOrDelegatePcarVisitStateAsync(_selectedVisit);
        }
    }

    private void OnTimeOnSiteCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        TimeOnSitePicker.IsEnabled = e.Value;
    }

    private void OnTimeOffSiteCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        TimeOffSitePicker.IsEnabled = e.Value;
    }

    private async void OnTimePopupSaveClicked(object sender, EventArgs e)
    {
        if (_selectedVisit != null)
        {
            PcarVisitStatusEnum? status = PcarVisitStatusEnum.Completed;
            string timeOn = null;
            string timeOff = null;

            bool hasOn = TimeOnSiteCheckBox.IsChecked;
            bool hasOff = TimeOffSiteCheckBox.IsChecked;

            // 1. Can not input off site time without onsite time
            if (hasOff && !hasOn)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Validation Error", "Cannot input Off-Site time without On-Site time.", "OK");
                });
                return;
            }

            if (hasOn)
            {
                timeOn = TimeOnSitePicker.Time.ToString(@"hh\:mm");
            }

            if (hasOff)
            {
                timeOff = TimeOffSitePicker.Time.ToString(@"hh\:mm");

                if (hasOn && TimeOnSitePicker.Time >= TimeOffSitePicker.Time)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Validation Error", "Time On Site must be earlier than Time Off Site.", "OK");
                    });
                    return;
                }
            }

            // Visit status must change automatically:
            // Completed - when timeOn and timeOff is entered and saved
            // InProgress - When only timeOn is entered and saved
            if (hasOn && hasOff)
            {
                status = PcarVisitStatusEnum.Completed;
            }
            else if (hasOn)
            {
                status = PcarVisitStatusEnum.InProgress;
            }

            if (_guardId == null || _clientSiteId == null || _userId == null ||
                _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0)
                return;

            string gpsCoordinates = await PermissionService.GetGpsLocationWithOutCheckingPermissionAsync();

            _selectedVisit.TimeOnSite = TimeOnSiteCheckBox.IsChecked ? TimeOnSitePicker.Time : null;
            _selectedVisit.TimeOffSite = TimeOffSiteCheckBox.IsChecked ? TimeOffSitePicker.Time : null;
            _selectedVisit.Status = (PcarVisitStatusEnum)status;

            try
            {
                using var http = new HttpClient();
                var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SaveVisitTime";

                var payload = new
                {
                    VisitId = _selectedVisit.VisitId,
                    SmartWandId = _smartWandId,
                    SiteId = _selectedVisit.SiteId,
                    DayName = _selectedVisit.DayName,
                    PcarRouteId = _selectedVisit.PcarRouteId,
                    PcarRouteDetailsId = _selectedVisit.PcarRouteDetailsId,
                    VisitName = _selectedVisit.VisitName,
                    VisitNumber = _selectedVisit.VisitNumber,
                    GuardId = _guardId,
                    GpsCoordinates = gpsCoordinates,
                    LoginUserId = _userId,
                    LoginSiteId = _clientSiteId,
                    TimeOn = timeOn,
                    TimeOff = timeOff,
                    Status = status,
                    TargetDate = _selectedDate,
                    ParentVisitId = _selectedVisit.ParentVisitId,
                    EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                    EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                    EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                    EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                    EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
                    EventMobileUtcDateTime = TimeZoneHelper.GetCurrentUtcDateTime()
                };

                var response = await http.PostAsJsonAsync(url, payload);
                var result = await response.Content.ReadFromJsonAsync<ApiResponseNew>();

                if (result != null)
                {
                    // 2. all events appear in LB of Romeo
                    string activityText = "";
                    if (status == PcarVisitStatusEnum.Completed)
                    {
                        activityText = $"PCAR Route: Completed visit '{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}' (On-Site: {timeOn}, Off-Site: {timeOff})";
                    }
                    else if (status == PcarVisitStatusEnum.InProgress)
                    {
                        activityText = $"PCAR Route: Started visit '{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}' (On-Site: {timeOn})";
                    }
                    else if (status == PcarVisitStatusEnum.CancelledOrDelegated)
                    {
                        activityText = $"PCAR Route: Visit cancelled or delegated '{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}'";
                    }
                    else if (status == PcarVisitStatusEnum.Accepted)
                    {
                        activityText = $"PCAR Route: Visit accepted'{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}' (On-Site: {timeOn})";
                    }

                    var logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
                    if (logBookServices != null)
                    {
                        await logBookServices.LogActivityTask(activityText, _selectedVisit.SiteId);
                    }

                    string toastMessage =
                        $"Success: {result.Success}\n" +
                        $"Message: {result.Message}\n" +
                        $"Visit: {_selectedVisit.VisitName}";

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Toast.Make(toastMessage, ToastDuration.Long).Show();
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Toast.Make("Null response from API", ToastDuration.Long).Show();
                    });
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Toast.Make("Error: " + ex.Message, ToastDuration.Long).Show();
                });
            }
        }

        EditTimePopupOverlay.IsVisible = false;
        await ReloadRouteAsync();
    }

    public async Task ReloadRouteAsync()
    {
        await _viewModel.LoadRealData(App.DeviceId, _selectedDate);
    }

    private async void OnTimePopupCancelClicked(object sender, EventArgs e)
    {
        if (_selectedVisit != null)
            _selectedVisit.RevertCheckState();

        EditTimePopupOverlay.IsVisible = false;
        await ReloadRouteAsync();
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

    public Command<SiteModel> CardTappedCommand =>
        new Command<SiteModel>(async (site) =>
        {
            if (site == null || site.Visits == null || site.Visits.Count == 0) return;

            //bool hasCancelled = site.Visits.Any(v => v.Status == PcarVisitStatusEnum.CancelledOrDelegated);
            bool hasPending = site.Visits.Any(v => v.Status == null);

            if (hasPending)
            {
                bool accept = await Application.Current.MainPage.DisplayAlert(
                    "Accept All Visits",
                    $"Do you want to accept all pending visits for {site.SiteName}?",
                    "Accept", "Cancel");

                if (accept)
                {
                    var pendingVisits = site.Visits.Where(v => v.Status == PcarVisitStatusEnum.Assigned).ToList();
                    foreach (var visit in pendingVisits)
                    {
                        await SavePcarVisitStateAsync(visit, PcarVisitStatusEnum.Accepted);
                    }
                }
            }
        });

    protected void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    public async Task LoadRealData(string deviceId, DateTime targetDate)
    {
        try
        {
            using var http = new HttpClient();
            var dateStr = targetDate.ToString("yyyy-MM-dd");
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetPcarDetails?deviceId={deviceId}&date={dateStr}";

            var response = await http.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PcarRouteResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!response.IsSuccessStatusCode)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Toast.Make(result?.Message ?? "Something went wrong", ToastDuration.Long).Show();
                });
                return;
            }

            if (result != null && result.Success == false)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Toast.Make(result.Message, ToastDuration.Long).Show();
                });
                return;
            }

            if (result != null && result.Success == true && result.Data != null)
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

                        if (site.Visits != null)
                        {
                            foreach (var v in site.Visits)
                            {
                                var vm = new VisitModel(
                                    v.VisitId,
                                    v.VisitName,
                                    v.VisitDate,
                                    site.SmartWandId,
                                    site.SiteId,
                                    site.DayName,
                                    site.PcarRouteId,
                                    site.PcarRouteDetailsId,
                                    v.Status,
                                    v.IsCheckedToday,
                                    v.SavedTimeOnSite,
                                    v.SavedTimeOffSite,
                                    v.ParentVisitId
                                );
                                vm.SiteName = site.SiteName;
                                vm.PatrolCarId = site.PatrolCarId;
                                vm.VisitNumber = v.VisitNumber;
                                vm.OnChecked += Visit_OnChecked;
                                siteModel.Visits.Add(vm);
                            }
                        }

                        siteModel.UpdateStatus();
                        SiteList.Add(siteModel);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Toast.Make("Error: " + ex.Message, ToastDuration.Long).Show();
            });
        }
    }

    private void Visit_OnChecked(object sender, EventArgs e)
    {
        if (sender is VisitModel visit)
        {
            _currentVisit = visit;

            if (visit.Status == PcarVisitStatusEnum.Completed)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    visit.RevertCheckState();
                    await Application.Current.MainPage.DisplayAlert("Task Completed", "Task already completed", "OK");
                });
                return;
            }

            if (visit.Status == PcarVisitStatusEnum.CancelledOrDelegated)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    visit.RevertCheckState();
                    await Application.Current.MainPage.DisplayAlert("Task Cancelled", "Task already cancelled by you", "OK");
                });
                return;
            }

            //if (visit.Status == PcarVisitStatusEnum.Assigned)
            //{
            //    MainThread.BeginInvokeOnMainThread(async () =>
            //    {
            //        visit.RevertCheckState();
            //        await Application.Current.MainPage.DisplayAlert("Task Assigned", "Task Assigned to you", "OK");
            //    });
            //    return;
            //}

            if (visit.Status == null || visit.Status == PcarVisitStatusEnum.Assigned)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool accept = await Application.Current.MainPage.DisplayAlert(
                        "Accept Task",
                        "Do you want to accept this task?",
                        "Accept", "Cancel");

                    if (accept)
                    {
                        await SavePcarVisitStateAsync(visit, PcarVisitStatusEnum.Accepted);
                    }
                    else
                    {
                        visit.RevertCheckState();
                    }
                });
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var page = (PcarRute)Application.Current.MainPage.Navigation.NavigationStack.Last();
                page.OpenTimePopup(visit);
            });
        }
    }

    public async Task SavePcarVisitStateAsync(VisitModel visit, PcarVisitStatusEnum status)
    {
        try
        {
            var gpsCoordinates = string.Empty;
            try
            {
                var location = await Geolocation.Default.GetLastKnownLocationAsync();
                if (location == null)
                {
                    location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
                }
                if (location != null)
                {
                    gpsCoordinates = $"{location.Latitude},{location.Longitude}";
                }
            }
            catch (Exception) { }

            var page = (PcarRute)Application.Current.MainPage.Navigation.NavigationStack.Last();

            using var http = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SaveVisitTime";

            var payload = new
            {
                VisitId = visit.VisitId,
                SmartWandId = page.PageSmartWandId,
                SiteId = visit.SiteId,
                DayName = visit.DayName,
                PcarRouteId = visit.PcarRouteId,
                PcarRouteDetailsId = visit.PcarRouteDetailsId,
                VisitName = visit.VisitName,
                VisitNumber = visit.VisitNumber,
                GuardId = page.GuardId,
                GpsCoordinates = gpsCoordinates,
                LoginUserId = page.UserId,
                LoginSiteId = page.ClientSiteId,
                TimeOn = (string)null,
                TimeOff = (string)null,
                Status = status,
                TargetDate = page.SelectedDate,
                ParentVisitId = visit.ParentVisitId,
                EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
                EventMobileUtcDateTime = TimeZoneHelper.GetCurrentUtcDateTime()
            };

            var response = await http.PostAsJsonAsync(url, payload);
            var json = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<ApiResponseNew>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response.IsSuccessStatusCode && apiResponse != null && apiResponse.Success)
            {                
                await page.ReloadRouteAsync();
            }
            else if (response.IsSuccessStatusCode && apiResponse != null && apiResponse.Success == false && apiResponse.RefreshData)
            {
                await Application.Current.MainPage.DisplayAlert("Error", apiResponse?.Message ?? "Visit status already changed.", "OK");
                await page.ReloadRouteAsync();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Error", apiResponse?.Message ?? "Failed to save visit status", "OK");
                visit.RevertCheckState();
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            visit.RevertCheckState();
        }
    }

    public async Task CancelOrDelegatePcarVisitStateAsync(VisitModel visit)
    {
        try
        {
            var gpsCoordinates = string.Empty;
            try
            {
                var location = await Geolocation.Default.GetLastKnownLocationAsync();
                if (location == null)
                {
                    location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
                }
                if (location != null)
                {
                    gpsCoordinates = $"{location.Latitude},{location.Longitude}";
                }
            }
            catch (Exception) { }
            var page = (PcarRute)Application.Current.MainPage.Navigation.NavigationStack.Last();

            if (visit.VisitId > 0)
            {
                if(visit.SmartWandId != page.PageSmartWandId)
                {
                    // Do not allow to cancel other smardwand visit
                    await Application.Current.MainPage.DisplayAlert("Error", "You cannot cancel others visit task.", "OK");
                    visit.RevertCheckState();
                    return;
                }

                using var http = new HttpClient();
                var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/CancelOrDelegateVisit";

                var payload = new
                {
                    VisitId = visit.VisitId,
                    SmartWandId = page.PageSmartWandId,
                    SiteId = visit.SiteId,
                    DayName = visit.DayName,
                    PcarRouteId = visit.PcarRouteId,
                    PcarRouteDetailsId = visit.PcarRouteDetailsId,
                    VisitName = visit.VisitName,
                    VisitNumber = visit.VisitNumber,
                    GuardId = page.GuardId,
                    GpsCoordinates = gpsCoordinates,
                    LoginUserId = page.UserId,
                    LoginSiteId = page.ClientSiteId,
                    TimeOn = (string)null,
                    TimeOff = (string)null,
                    Status = PcarVisitStatusEnum.CancelledOrDelegated,
                    TargetDate = page.SelectedDate,
                    ParentVisitId = visit.ParentVisitId,
                    EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                    EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                    EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                    EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                    EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
                    EventMobileUtcDateTime = TimeZoneHelper.GetCurrentUtcDateTime()
                };

                var response = await http.PostAsJsonAsync(url, payload);
                var json = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<ApiResponseNew>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && apiResponse != null && apiResponse.Success)
                {
                    await page.ReloadRouteAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", apiResponse?.Message ?? "Failed to cancel visit.", "OK");
                    visit.RevertCheckState();
                }
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            visit.RevertCheckState();
        }

    }
}

/* =============================================================
   MODELS
   ============================================================= */

public class SiteModel : INotifyPropertyChanged
{
    private string _siteName;
    private string _address;
    private string _gpsLocation;
    private ObservableCollection<VisitModel> _visits;

    public string SiteName
    {
        get => _siteName;
        set { _siteName = value; Raise(nameof(SiteName)); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; Raise(nameof(Address)); }
    }

    public string GPSLocation
    {
        get => _gpsLocation;
        set { _gpsLocation = value; Raise(nameof(GPSLocation)); }
    }

    public ObservableCollection<VisitModel> Visits
    {
        get => _visits;
        set
        {
            _visits = value;
            Raise(nameof(Visits));
            UpdateStatus();
        }
    }

    public Brush CardBackground
    {
        get
        {
            if (Visits == null || Visits.Count == 0)
                return new SolidColorBrush(Colors.Orange);

            // Black Card: if any visit is cancelled
            if (Visits.Any(v => v.Status == PcarVisitStatusEnum.CancelledOrDelegated))
            {
                return new SolidColorBrush(Color.FromArgb("#212121"));
            }

            // Green Gradient Card: if any visits are accepted, in progress, or completed
            if (Visits.Any(v => v.Status == PcarVisitStatusEnum.Completed ||
                              v.Status == PcarVisitStatusEnum.Accepted ||
                              v.Status == PcarVisitStatusEnum.InProgress))
            {
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb("#90EE90"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb("#32CD32"), Offset = 1.0f }
                    }
                };
            }

            // Orange Gradient Card: otherwise (if there are still unaccepted/pending visits)
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = Color.FromArgb("#FFB74D"), Offset = 0.1f },
                    new GradientStop { Color = Color.FromArgb("#FB8C00"), Offset = 1.0f }
                }
            };
        }
    }

    public Color BorderColor
    {
        get
        {
            if (Visits != null && Visits.Any(v => v.Status == PcarVisitStatusEnum.CancelledOrDelegated))
            {
                return Color.FromArgb("#FB8C00");
            }
            return Colors.Transparent;
        }
    }

    public Color GpsIconColor
    {
        get
        {
            if (Visits != null && Visits.Any(v => v.Status == PcarVisitStatusEnum.CancelledOrDelegated))
            {
                return Colors.White;
            }
            return Colors.Black;
        }
    }

    public void UpdateStatus()
    {
        Raise(nameof(CardBackground));
        Raise(nameof(BorderColor));
        Raise(nameof(GpsIconColor));
    }

    public Command OpenMapsCommand =>
        new Command(async () =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(GPSLocation))
                    return;

                var query = GPSLocation.Trim();
                var url = $"https://www.google.com/maps/search/?api=1&query={query}";
                await Launcher.OpenAsync(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Map Open Error: " + ex.Message);
            }
        });

    public event PropertyChangedEventHandler PropertyChanged;
    protected void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public class VisitModel : INotifyPropertyChanged
{
    private bool _isChecked;
    private PcarVisitStatusEnum _status;
    public int? VisitId { get; set; }
    public bool IsCheckedToday { get; set; } = false;
    public int PcarRouteId { get; set; }
    public int PcarRouteDetailsId { get; set; }
    public int? PatrolCarId { get; set; }
    public int? ParentVisitId { get; set; }
    public string VisitName { get; set; }
    public DateTime VisitDate { get; set; }
    public int VisitNumber { get; set; }
    public string SiteName { get; set; }
    public int SmartWandId { get; set; }
    public int SiteId { get; set; }
    public string DayName { get; set; }
    public TimeSpan? TimeOnSite { get; set; }
    public TimeSpan? TimeOffSite { get; set; }
    public string SavedTimeOnSite { get; set; }
    public string SavedTimeOffSite { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler OnChecked;

    public VisitModel(
        int? visitId,
        string visitName,
        DateTime visitdate,
        int smartWandId,
        int siteId,
        string dayName,
        int pcarRouteId,
        int pcarRouteDetailsId,
        PcarVisitStatusEnum status,
        bool isCheckedToday = false,
        string savedTimeOnSite = null,
        string savedTimeOffSite = null,
        int? parentVisitId = null)
    {
        VisitId = visitId;
        VisitName = visitName;
        VisitDate = visitdate;
        SmartWandId = smartWandId;
        SiteId = siteId;
        DayName = dayName;
        PcarRouteId = pcarRouteId;
        PcarRouteDetailsId = pcarRouteDetailsId;

        _status = status;
        ParentVisitId = parentVisitId;
        SavedTimeOnSite = savedTimeOnSite;
        SavedTimeOffSite = savedTimeOffSite;

        IsCheckedToday = isCheckedToday;
        _isChecked = isCheckedToday;
        IsChecked = isCheckedToday;

        ToggleCheckCommand = new Command(() => Toggle());
    }

    public PcarVisitStatusEnum Status
    {
        get => _status;
        set
        {
            _status = value;
            Raise(nameof(Status));
            Raise(nameof(StatusColor));
            Raise(nameof(CheckboxText));
            Raise(nameof(CheckboxBackground));
            Raise(nameof(CheckboxTextColor));
            Raise(nameof(BoxBorderColor));
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            Raise(nameof(IsChecked));
            Raise(nameof(StatusColor));
            Raise(nameof(CheckboxText));
            Raise(nameof(CheckboxBackground));
            Raise(nameof(CheckboxTextColor));
            Raise(nameof(BoxBorderColor));
        }
    }

    public string StatusColor =>
        IsChecked ? "#A8E6A1" : "#EFEFEF";

    public string CheckboxText
    {
        get
        {
            if (Status == PcarVisitStatusEnum.Completed)
                return "✔";
            if (Status == PcarVisitStatusEnum.CancelledOrDelegated)
                return "🗙";
            if (Status == PcarVisitStatusEnum.InProgress)
                return "P";
            return "";
        }
    }

    public Color CheckboxBackground
    {
        get
        {
            if (Status == PcarVisitStatusEnum.CancelledOrDelegated)
                return Color.FromArgb("#ffebee"); // Light red for Cancelled
            if (Status == PcarVisitStatusEnum.Completed)
                return Color.FromArgb("#e8f5e9"); // Light green for Completed
            if (Status == PcarVisitStatusEnum.InProgress)
                return Color.FromArgb("#ffe0b2"); // Light orange for InProgress (P)
            if (Status == PcarVisitStatusEnum.Accepted)
                return Color.FromArgb("#e8f5e9"); // Light green for Accepted
            if (Status == PcarVisitStatusEnum.Assigned)
                return Colors.Transparent;
            return Colors.Transparent;
        }
    }

    public Color CheckboxTextColor
    {
        get
        {
            if (Status == PcarVisitStatusEnum.CancelledOrDelegated)
                return Color.FromArgb("#c62828"); // Red color for "🗙"
            if (Status == PcarVisitStatusEnum.Completed)
                return Color.FromArgb("#2e7d32"); // Green color for "✔"
            if (Status == PcarVisitStatusEnum.InProgress)
                return Color.FromArgb("#e65100"); // Dark orange color for "P"
            if (Status == PcarVisitStatusEnum.Accepted)
                return Color.FromArgb("#2e7d32"); // Green color for "A" style checkbox
            if (Status == PcarVisitStatusEnum.Assigned)
                return Colors.Black;
            return Colors.Black;
        }
    }

    public Color BoxBorderColor
    {
        get
        {
            if (Status == PcarVisitStatusEnum.CancelledOrDelegated)
                return Color.FromArgb("#c62828");
            if (Status == PcarVisitStatusEnum.Completed)
                return Color.FromArgb("#2e7d32");
            if (Status == PcarVisitStatusEnum.InProgress)
                return Color.FromArgb("#e65100");
            if (Status == PcarVisitStatusEnum.Accepted)
                return Color.FromArgb("#2e7d32");
            if (Status == PcarVisitStatusEnum.Assigned)
                return Color.FromArgb("#C2C2C2");
            return Color.FromArgb("#C2C2C2");
        }
    }

    public Command ToggleCheckCommand { get; }

    private bool _previousCheckedState;

    private void Toggle()
    {
        _previousCheckedState = IsChecked;

        if (IsChecked)
        {
            OnChecked?.Invoke(this, EventArgs.Empty);
            return;
        }

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
    public int? PatrolCarId { get; set; }
    public int SiteId { get; set; }
    public int PcarRouteId { get; set; }
    public int PcarRouteDetailsId { get; set; }

    public string GPSLocation { get; set; }
    public string DayName { get; set; }
    public string SiteName { get; set; }
    public string Address { get; set; }
    public int VisitCount { get; set; }
    public List<VisitDto> Visits { get; set; }
}

public class VisitDto
{
    public int? VisitId { get; set; }
    public string VisitName { get; set; }
    public int VisitNumber { get; set; }
    public DateTime VisitDate { get; set; }
    public int SiteId { get; set; }
    public bool IsCheckedToday { get; set; }
    public string SavedTimeOnSite { get; set; }
    public string SavedTimeOffSite { get; set; }
    public PcarVisitStatusEnum Status { get; set; }
    public int? ParentVisitId { get; set; }
}

public class ApiResponseNew
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public bool RefreshData { get; set; }
    public object Data { get; set; }
}

public class PcarRouteDto
{
    public int Id { get; set; }
    public string Pcarroutename { get; set; }
}

public class PcarRoutesResult
{
    public bool Success { get; set; }
    public List<PcarRouteDto> Data { get; set; }
}
