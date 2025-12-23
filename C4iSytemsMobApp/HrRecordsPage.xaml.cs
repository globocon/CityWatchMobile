using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace C4iSytemsMobApp;


public partial class HrRecordsPage : ContentPage
{
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    private readonly IGuardApiServices _guardApiServices;
    public ObservableCollection<GuardComplianceAndLicense> HrRecords { get; set; } = new();

    public HrRecordsPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BindingContext = this;
        _guardApiServices = IPlatformApplication.Current.Services.GetService<IGuardApiServices>();
        EditCommand = new Command<GuardComplianceAndLicense>(OnEditClicked);

        //EditCommand = new Command<PatrolCarLog>(OnEdit);
        DeleteCommand = new Command<GuardComplianceAndLicense>(OnDelete);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHrRecordsAsync();
    }

    private async Task LoadHrRecordsAsync()
    {

        try
        {
            // Show loading overlay
            LoadingOverlay.IsVisible = true;

            // Clear old data
            HrRecords.Clear();

            var rows = await _guardApiServices.GetHrRecordsOfGuard();
            if (rows != null && rows.Count > 0 && rows.Any())
            {

                foreach (var log in rows)
                {
                    //log.PatrolCar = $"{log.ClientSitePatrolCar.Model}";
                    HrRecords.Add(log);
                }
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

    void OnEdit(GuardComplianceAndLicense item)
    {
        // Edit logic
    }

    void OnDelete(GuardComplianceAndLicense item)
    {
        // Delete logic
    }

    private async void OnEditClicked(GuardComplianceAndLicense item)
    {
        //var popup = new EditPatrolCarLogPopup(item);
        //var result = await this.ShowPopupAsync(popup);

        //if (result is PatrolCarLog updated)
        //{
        //    // Reflect the updated values in the CollectionView
        //    //int index = Logs.IndexOf(item);
        //    //if (index >= 0)
        //    //    Logs[index] = updated;


        //    var saveResult = await _logBookServices.SavePatrolCarLogAsync(updated);
        //    if(!saveResult.isSuccess)
        //    {
        //        await DisplayAlert("Error", $"Failed to save log: {saveResult.errorMessage}", "OK");
        //        return;
        //    }
        //    else
        //    {
        //        await LoadPatrolCarLogsAsync();
        //        await DisplayAlert("Updated", "Patrol Car log entry has been updated.", "OK");
        //    }
        //}
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }

    private void OnAddComplianceClicked(object sender, TappedEventArgs e)
    {

    }

    private async void OnHrRecordClicked(object sender, TappedEventArgs e)
    {
        string fileName = string.Empty;
        if (sender is Frame frame && frame.BindingContext != null)
        {
            var item = frame.BindingContext as GuardComplianceAndLicense; // replace with your actual model

            if (item != null)
            {
                fileName = item.FileName;

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    //"/Uploads/Guards/License/4068435/01c_Licence - Private Agent-exp 20 MAY 26.png"

                    var savedLicenseNumber = Preferences.Get("LicenseNumber", "");

                    var encodedBlobName = Uri.EscapeDataString(fileName);
                    var fileUrl = $"{AppConfig.MobileSignalRBaseUrl}/Uploads/Guards/License/{savedLicenseNumber}/{fileName}";

                    if (!string.IsNullOrWhiteSpace(fileUrl))
                    {
                        try
                        {
                            await Browser.Default.OpenAsync(fileUrl, BrowserLaunchMode.SystemPreferred);
                        }
                        catch
                        {
                            await DisplayAlert("Error", "Unable to open document file.", "OK");
                        }
                    }
                }
            }
        }
    }
}


