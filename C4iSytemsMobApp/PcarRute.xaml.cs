using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
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
    private DateTime _selectedDate;
    private bool _isPushingTask;

    public PcarRute()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _selectedDate = DateTime.Today;

        _viewModel = new PcarRouteViewModel();
        BindingContext = _viewModel;

        infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();
        deviceid = infoService?.GetDeviceId();
      
        UpdateDayNavigationLabel();

        // Load secure storage FIRST
        _ = InitializePage();
    }

    private async Task InitializePage()
    {
        await LoadSecureData();   // ENSURES IDs exist before API call

        // Now load real data properly ON UI THREAD
        await _viewModel.LoadRealData(deviceid, _selectedDate);
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
        _isPushingTask = false;

        // Reset visibility layouts to initial time-entry state
        TimeOnSiteLayout.IsVisible = true;
        TimeOffSiteLayout.IsVisible = true;
        TargetPcarLayout.IsVisible = false;
        TargetPcarPicker.SelectedItem = null;
        PushTaskIconLayout.IsVisible = true;

        TimeOnSiteCheckBox.IsEnabled = true;
        TimeOffSiteCheckBox.IsEnabled = true;
        TimeOnSitePicker.IsEnabled = false;
        TimeOffSitePicker.IsEnabled = false;
        SaveTimeButton.IsEnabled = true;

        // Default current time
        TimeSpan now = DateTime.Now.TimeOfDay;
        TimeOnSitePicker.Time = now;
        TimeOffSitePicker.Time = now;

        if (visit.Status == PcarVisitStatusEnum.InProgress)
        {
            PushTaskIconLayout.IsVisible = false;

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
        else if (visit.Status == PcarVisitStatusEnum.Completed || visit.Status == PcarVisitStatusEnum.PushedToPcar)
        {
            if (visit.Status == PcarVisitStatusEnum.PushedToPcar)
            {
                // Task was pushed out
                PushTaskIconLayout.IsVisible = false;
                TimeOnSiteLayout.IsVisible = false;
                TimeOffSiteLayout.IsVisible = false;
            }
            else
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
                PushTaskIconLayout.IsVisible = false;
            }

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

            // Load list of target PCARs asynchronously
            _ = LoadPcarRoutesList();
        }

        EditTimePopupOverlay.IsVisible = true;
    }

    private List<PcarRouteDto> _pcarRoutesList;

    private async Task LoadPcarRoutesList()
    {
        try
        {
            if (_clientSiteId == null || _clientSiteId <= 0)
                return;

            using var http = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetPcarRoutes?clientSiteId={_clientSiteId.Value}";
            var response = await http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PcarRoutesResult>();
                if (result != null && result.Success)
                {
                    _pcarRoutesList = result.Data;
                    
                    // Exclude the current route's PatrolCarId so we don't delegate to ourselves
                    int? currentPatrolCarId = _selectedVisit.PatrolCarId;
                    var otherRoutes = _pcarRoutesList;
                    if (currentPatrolCarId.HasValue)
                    {
                        otherRoutes = _pcarRoutesList.Where(r => r.Id != currentPatrolCarId.Value).ToList();
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        TargetPcarPicker.ItemsSource = otherRoutes;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load PCAR routes list: {ex.Message}");
        }
    }

    private void OnPushTaskIconClicked(object sender, EventArgs e)
    {
        _isPushingTask = true;
        TargetPcarLayout.IsVisible = true;
        TimeOnSiteLayout.IsVisible = false;
        TimeOffSiteLayout.IsVisible = false;
    }

    private void OnCancelPushClicked(object sender, EventArgs e)
    {
        _isPushingTask = false;
        TargetPcarLayout.IsVisible = false;
        TimeOnSiteLayout.IsVisible = true;
        TimeOffSiteLayout.IsVisible = true;
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

            int? targetPcarId = null;
            if (_isPushingTask)
            {
                var selectedPcar = TargetPcarPicker.SelectedItem as PcarRouteDto;
                if (selectedPcar == null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Validation Error", "Please select a target PCAR to push the task to.", "OK");
                    });
                    return;
                }
                
                status = PcarVisitStatusEnum.PushedToPcar;
                targetPcarId = selectedPcar.Id;
            }
            else
            {
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
            }

            if (_guardId == null || _clientSiteId == null || _userId == null ||
                _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0)
                return;

            string gpsCoordinates = await PermissionService.GetGpsLocationWithOutCheckingPermissionAsync();

            if (_isPushingTask)
            {
                _selectedVisit.TimeOnSite = null;
                _selectedVisit.TimeOffSite = null;
                _selectedVisit.Status = PcarVisitStatusEnum.PushedToPcar;
                _selectedVisit.PushedTo = targetPcarId;
            }
            else
            {
                _selectedVisit.TimeOnSite = TimeOnSiteCheckBox.IsChecked ? TimeOnSitePicker.Time : null;
                _selectedVisit.TimeOffSite = TimeOffSiteCheckBox.IsChecked ? TimeOffSitePicker.Time : null;
                _selectedVisit.Status = status;
            }

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
                    VisitNumber = _selectedVisit.VisitNumber,
                    GuardId = _guardId,
                    GpsCoordinates = gpsCoordinates,
                    LoginUserId = _userId,
                    LoginSiteId = _clientSiteId,
                    TimeOn = timeOn,
                    TimeOff = timeOff,
                    Status = status,
                    PushedTo = targetPcarId
                };

                var response = await http.PostAsJsonAsync(url, payload);
                var result = await response.Content.ReadFromJsonAsync<ApiResponseNew>();

                if (result != null)
                {
                    // 2. all events appear in LB of Romeo
                    string activityText = "";
                    if (_isPushingTask)
                    {
                        var selectedPcar = TargetPcarPicker.SelectedItem as PcarRouteDto;
                        activityText = $"PCAR Route: Visit '{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}' PUSHED to PCAR '{selectedPcar.Pcarroutename}'";
                    }
                    else if (status == PcarVisitStatusEnum.Completed)
                    {
                        activityText = $"PCAR Route: Completed visit '{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}' (On-Site: {timeOn}, Off-Site: {timeOff})";
                    }
                    else if (status == PcarVisitStatusEnum.InProgress)
                    {
                        activityText = $"PCAR Route: Started visit '{_selectedVisit.VisitName}' at '{_selectedVisit.SiteName}' (On-Site: {timeOn})";
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
        _isPushingTask = false;
        await ReloadRouteAsync();
    }

    private async Task ReloadRouteAsync()
    {
        await _viewModel.LoadRealData(deviceid, _selectedDate);
    }

    private async void OnTimePopupCancelClicked(object sender, EventArgs e)
    {
        if (_selectedVisit != null)
            _selectedVisit.RevertCheckState();

        _isPushingTask = false;
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
                                    v.VisitName,
                                    site.SmartWandId,
                                    site.SiteId,
                                    site.DayName,
                                    site.PcarRouteId,
                                    site.PcarRouteDetailsId,
                                    v.IsCheckedToday,
                                    v.SavedTimeOnSite,
                                    v.SavedTimeOffSite,
                                    v.Status,
                                    v.PushedTo
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

            // Black Gradient Card: if any visits is pushed out
            if (Visits.Any(v => v.Status == PcarVisitStatusEnum.PushedToPcar))
            {
                return new SolidColorBrush(Color.FromArgb("#212121"));
            }

            // Green Gradient Card: if all visits are fully completed or pushed out
            if (Visits.All(v => v.Status == PcarVisitStatusEnum.Completed || v.Status == PcarVisitStatusEnum.PushedToPcar))
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

            // Orange Gradient Card: if there are still unfinished/pending or in progress visits
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
            // Thin gold/orange border around Black cards
            if (Visits != null && Visits.Any(v => v.Status == PcarVisitStatusEnum.PushedToPcar))
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
            // Black GPS icon on Orange and Green cards; White GPS icon on Black cards
            if (Visits != null && Visits.Any(v => v.Status == PcarVisitStatusEnum.PushedToPcar))
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
    private PcarVisitStatusEnum? _status;

    public bool IsCheckedToday { get; set; } = false;
    public int PcarRouteId { get; set; }
    public int PcarRouteDetailsId { get; set; }
    public int? PatrolCarId { get; set; }
    public int? PushedTo { get; set; }
    public string VisitName { get; set; }
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
        string visitName,
        int smartWandId,
        int siteId,
        string dayName,
        int pcarRouteId,
        int pcarRouteDetailsId,
        bool isCheckedToday = false,
        string savedTimeOnSite = null,
        string savedTimeOffSite = null,
        PcarVisitStatusEnum? status = null,
        int? pushedTo = null)
    {
        VisitName = visitName;
        SmartWandId = smartWandId;
        SiteId = siteId;
        DayName = dayName;
        PcarRouteId = pcarRouteId;
        PcarRouteDetailsId = pcarRouteDetailsId;

        IsCheckedToday = isCheckedToday;
        _isChecked = isCheckedToday;
        IsChecked = isCheckedToday;

        SavedTimeOnSite = savedTimeOnSite;
        SavedTimeOffSite = savedTimeOffSite;
        _status = status;
        PushedTo = pushedTo;

        ToggleCheckCommand = new Command(() => Toggle());
    }

    public PcarVisitStatusEnum? Status
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
            if (Status == PcarVisitStatusEnum.PushedToPcar)
                return "P";
            if (Status == PcarVisitStatusEnum.Completed)
                return "✔";
            if (Status == PcarVisitStatusEnum.InProgress)
                return "I";
            return "";
        }
    }

    public Color CheckboxBackground
    {
        get
        {
            if (Status == PcarVisitStatusEnum.PushedToPcar)
                return Color.FromArgb("#e8dbff"); // Light purple background
            if (Status == PcarVisitStatusEnum.Completed)
                return Colors.White;
            if (Status == PcarVisitStatusEnum.InProgress)
                return Color.FromArgb("#ffe0b2"); // Light orange background for InProgress
            return Colors.Transparent;
        }
    }

    public Color CheckboxTextColor
    {
        get
        {
            if (Status == PcarVisitStatusEnum.PushedToPcar)
                return Color.FromArgb("#6f42c1"); // Purple text for "P"
            if (Status == PcarVisitStatusEnum.InProgress)
                return Color.FromArgb("#e65100"); // Dark orange text for "I"
            return Colors.Black;
        }
    }

    public Color BoxBorderColor
    {
        get
        {
            if (Status == PcarVisitStatusEnum.PushedToPcar)
                return Color.FromArgb("#6f42c1");
            if (Status == PcarVisitStatusEnum.InProgress)
                return Color.FromArgb("#e65100");
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
    public string VisitName { get; set; }
    public int VisitNumber { get; set; }
    public bool IsCheckedToday { get; set; }
    public string SavedTimeOnSite { get; set; }
    public string SavedTimeOffSite { get; set; }
    public PcarVisitStatusEnum? Status { get; set; }
    public int? PushedTo { get; set; }
}

public class ApiResponseNew
{
    public bool Success { get; set; }
    public string Message { get; set; }
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
