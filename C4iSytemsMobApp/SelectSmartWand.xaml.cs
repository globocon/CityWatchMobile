using C4iSytemsMobApp.Interface;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace C4iSytemsMobApp;

public partial class SelectSmartWand : ContentPage
{
    public const string ALERT_TITLE = "Smart Wand";
    private readonly IScannerControlServices _scannerControlServices;
    private IDeviceInfoService infoService;
    public ObservableCollection<DropdownItem> ClientSiteSmartWands { get; set; } = new();
    private int savedClientSiteId;
    private string savedSmartWandIdKeyName;
    private string savedSmartWandNameKeyName;
    private string devicename;
    private string deviceid;
    private string deviceType = "Unknown";

    public SelectSmartWand()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
        pickerSmartWand.ItemsSource = ClientSiteSmartWands;
        infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();
        devicename = infoService?.GetDeviceName();
        deviceid = infoService?.GetDeviceId();

#if ANDROID
        deviceType = "Android";
#elif IOS
        deviceType = "iOS";
#elif WINDOWS
        deviceType = "Windows";
#elif MACCATALYST
        deviceType = "MacCatalyst";
#elif TIZEN
        deviceType = "Tizen";
#endif

    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;
        savedClientSiteId = clientSiteId;
        savedSmartWandIdKeyName = $"{savedClientSiteId}_SavedSmartWandId";
        savedSmartWandNameKeyName = $"{savedClientSiteId}_SavedSmartWandName";
        await LoadClientSites();               
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private async Task LoadClientSites()
    {
        if (savedClientSiteId == 0) return;

        try
        {
            var response = await _scannerControlServices.GetClientSiteSmartWandsAsync(savedClientSiteId.ToString());
            ClientSiteSmartWands.Clear();
            // If response is null, add a single "Select" item
            var items = response ?? new List<DropdownItem> { new DropdownItem { Id = 0, Name = "Select" } };

            foreach (var sw in items)
                ClientSiteSmartWands.Add(sw);

            var savedId = Preferences.Get(savedSmartWandIdKeyName, 0);

            // find the actual instance in the collection
            var match = ClientSiteSmartWands.FirstOrDefault(x => x.Id == savedId);

            if (match != null)
                pickerSmartWand.SelectedItem = match;
            else
                pickerSmartWand.SelectedIndex = 0; // or -1 if you want nothing selected

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading client sites smartwands: {ex.Message}");
        }
    }

    private void OnSmartWandSelectionChanged(object sender, EventArgs e)
    {
        
    }
    private async void OnSaveTagClicked(object sender, EventArgs e)
    {
        var _selectedSmartWand = (DropdownItem)pickerSmartWand.SelectedItem;

        //check if already registered in db
        try
        {
            var isRegistered = await _scannerControlServices.CheckAndRegisterSmartWandAsync(_selectedSmartWand.Id, deviceid, devicename, deviceType);
            if (!isRegistered.IsSuccess)
            {
                await DisplayAlert(ALERT_TITLE, isRegistered.Message, "OK");
                return;
            }

            Preferences.Set(savedSmartWandIdKeyName, _selectedSmartWand.Id);
            Preferences.Set(savedSmartWandNameKeyName, _selectedSmartWand.Name);
            await ShowToastMessage(isRegistered.Message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error registering smartwand: {ex.Message}");
            await DisplayAlert(ALERT_TITLE, "An error occurred while registering the Smart Wand. Please try again later.", "OK");
        }
        
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MenuSettingsPage();
    }


    private async Task ShowToastMessage(string message)
    {
        await Toast.Make(message, ToastDuration.Long).Show();

    }
    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(Preferences.Get("GuardId", "0"), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", "0"), out int userId);

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
}