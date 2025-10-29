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
    public ObservableCollection<PatrolCarLog> Logs { get; set; } = new();

    public PatrolCarPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BindingContext = this;
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
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

            // Clear old data
            Logs.Clear();

            var rows = await _logBookServices.GetPatrolCarLogsAsync();

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


            var saveResult = await _logBookServices.SavePatrolCarLogAsync(updated);
            if(!saveResult.isSuccess)
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
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }
     
}


