using AutoMapper;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace C4iSytemsMobApp;

public partial class CustomContractPage : ContentPage
{
    public ICommand EditCommand { get; }
    private readonly ILogBookServices _logBookServices;
    private readonly IMapper _mapper;
    private readonly IScanDataDbServices _scanDataDbService;
    private readonly ICustomLogEntryServices _customLogEntryServices;
    
    public ObservableCollection<DictionaryWrapper> Logs { get; set; } = new();

    public CustomContractPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BindingContext = this;
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
        _mapper = IPlatformApplication.Current.Services.GetService<IMapper>();
        _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
        _customLogEntryServices = IPlatformApplication.Current.Services.GetService<ICustomLogEntryServices>();
        
        EditCommand = new Command<DictionaryWrapper>(OnEditClicked);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            // Show loading overlay
            LoadingOverlay.IsVisible = true;
            List<Dictionary<string, string?>> rows = new List<Dictionary<string, string?>>();

            Logs.Clear();
            if (App.IsOnline)
            {
                rows = await _logBookServices.GetCustomFieldLogsAsync();
                if (rows != null && rows.Count > 0)
                {
                    await _customLogEntryServices.ProcessCustomFieldLogsOnlineDataToCache(rows);
                    rows.Clear();
                }
            }

            rows = await _customLogEntryServices.GetCustomFieldLogsFromCache();

            foreach (var dict in rows)
            {
                //if (dict.ContainsKey("timeSlot"))
                //{
                //    var value = dict["timeSlot"];
                //    dict.Remove("timeSlot");
                //    dict["Time Slot"] = value;
                //}
                Logs.Add(new DictionaryWrapper(dict));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading overlay
            LoadingOverlay.IsVisible = false;
        }

    }

    private async void OnEditClicked(DictionaryWrapper item)
    {
        var popup = new EditLogPopup(item);
        var result = await this.ShowPopupAsync(popup);

        if (result is DictionaryWrapper updated)
        {
            // Reflect the updated values in the CollectionView
            //int index = Logs.IndexOf(item);
            //if (index >= 0)
            //    Logs[index] = updated;

            var toupdate = updated.ToDictionary();

            if (App.IsOnline)
            {
                if (toupdate.ContainsKey("Time Slot"))
                {
                    var value = toupdate["Time Slot"];
                    toupdate.Remove("Time Slot");
                    toupdate["timeSlot"] = value;
                }

                var saveResult = await _logBookServices.SaveCustomFieldLogAsync(toupdate);
                if (!saveResult.isSuccess)
                {
                    await DisplayAlert("Error", $"Failed to save log: {saveResult.errorMessage}", "OK");
                    return;
                }
                else
                {
                    await LoadLogsAsync();
                    await DisplayAlert("Updated", "Custom log entry has been updated.", "OK");
                }
            }
            else
            {
                await _customLogEntryServices.SaveCustomFieldLogRequestHeadDataToCache(toupdate);
                await LoadLogsAsync();
                await DisplayAlert("Updated", "Custom log entry has been saved offline. The entry will be synced when online.", "OK");
            }
        }
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
        //Application.Current.MainPage = new MainPage();
    }

}

public class DictionaryWrapper
{
    public ObservableCollection<KeyValuePair<string, string>> KeyValues { get; }

    public DictionaryWrapper(Dictionary<string, string> dict)
    {
        KeyValues = new ObservableCollection<KeyValuePair<string, string>>(dict);
    }

    // Convert back to Dictionary<string,string>
    public Dictionary<string, string> ToDictionary()
    {
        return KeyValues.ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
