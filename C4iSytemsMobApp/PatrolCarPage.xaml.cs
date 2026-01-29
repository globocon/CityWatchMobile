using AutoMapper;
using C4iSytemsMobApp.Data;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace C4iSytemsMobApp;


public partial class PatrolCarPage : ContentPage
{
    public ICommand EditCommand { get; }
    private readonly ILogBookServices _logBookServices;
    private readonly IMapper _mapper;
    private readonly IScanDataDbServices _scanDataDbService;
    private IDeviceInfoService infoService;
    private string devicename;
    private string deviceid;
    public ObservableCollection<PatrolCarLog> Logs { get; set; } = new();

    public PatrolCarPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BindingContext = this;
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
        _mapper = IPlatformApplication.Current.Services.GetService<IMapper>();
        _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
        infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();
        devicename = infoService?.GetDeviceName();
        deviceid = infoService?.GetDeviceId();
        EditCommand = new Command<PatrolCarLog>(OnEditClicked);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPatrolCarLogsAsync();
    }

    private async Task LoadPatrolCarLogsAsync()
    {

        try
        {
            // Show loading overlay
            LoadingOverlay.IsVisible = true;
            List<PatrolCarLog> rows = new List<PatrolCarLog>();

            // Clear old data
            Logs.Clear();
            if (App.IsOnline)
            {
                rows = await _logBookServices.GetPatrolCarLogsAsync();
                if (rows != null && rows.Count > 0)
                {
                    var cacheEntity = _mapper.Map<List<PatrolCarLogCache>>(rows);
                    // Update local DB cache
                    await _scanDataDbService.RefreshPatrolCarCacheList(cacheEntity);
                    rows.Clear();
                }
            }

            var rowscache = await _scanDataDbService.GetPatrolCarCacheList();
            rows = _mapper.Map<List<PatrolCarLog>>(rowscache);

            foreach (var log in rows)
            {
                log.PatrolCar = $"{log.ClientSitePatrolCar.Model} - {log.ClientSitePatrolCar.Rego} - KM @ 00:01 HRS";
                Logs.Add(log);
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

    private async void OnEditClicked(PatrolCarLog item)
    {
        var popup = new EditPatrolCarLogPopup(item);
        var result = await this.ShowPopupAsync(popup);

        if (result is PatrolCarLog updated)
        {
            // Reflect the updated values in the CollectionView
            //int index = Logs.IndexOf(item);
            //if (index >= 0)
            //    Logs[index] = updated;

            if (App.IsOnline)
            {
                var saveResult = await _logBookServices.SavePatrolCarLogAsync(updated);
                if (!saveResult.isSuccess)
                {
                    await DisplayAlert("Error", $"Failed to save log: {saveResult.errorMessage}", "OK");
                    return;
                }
                else
                {
                    await LoadPatrolCarLogsAsync();
                    await DisplayAlert("Updated", "Patrol Car log entry has been updated.", "OK");
                }
            }
            else
            {
                var cacheEntity = _mapper.Map<PatrolCarLogRequestCache>(updated);
                cacheEntity.SiteId = cacheEntity.ClientSiteId;
                cacheEntity.DeviceId = deviceid;
                cacheEntity.DeviceName = devicename;

                var r = await _scanDataDbService.SavePatrolCarLogRequestCacheData(cacheEntity);
                if (r)
                {
                    var cacheEntityPatrolCarLog = _mapper.Map<PatrolCarLogCache>(updated);
                    var res = await _scanDataDbService.UpdatePatrolCarLogCacheData(cacheEntityPatrolCarLog);
                    await LoadPatrolCarLogsAsync();
                    await DisplayAlert("Updated", "Patrol Car log entry has been saved offline. The entry will be synced when online.", "OK");
                }
            }
        }
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }

}


