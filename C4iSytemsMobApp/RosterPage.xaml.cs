using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using System.Collections.ObjectModel;

namespace C4iSytemsMobApp
{
    public partial class RosterPage : ContentPage
    {
        private readonly IGuardApiServices _apiService;
        private DateTime _currentWeekStart;
        private int _selectedSiteId;

        public RosterPage()
        {
            InitializeComponent();
            
            // Resolve the API service from the DI container (registered in MauiProgram)
            _apiService = ServiceHelper.GetService<IGuardApiServices>();
            
            // Initialize to current week
            _currentWeekStart = GetStartOfWeek(DateTime.Today);
            
            // Retrieve the selected site from preferences (set during login/site selection)
            int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out _selectedSiteId);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadRosterData();
        }

        private async Task LoadRosterData()
        {
            if (_selectedSiteId <= 0)
            {
                await DisplayAlert("Error", "No site selected. Please select a site first.", "OK");
                return;
            }

            LoadingOverlay.IsVisible = true;
            try
            {
                string dateStr = _currentWeekStart.ToString("yyyy-MM-dd");
                var data = await _apiService.GetRosterAsync(_selectedSiteId, dateStr);

                if (data != null)
                {
                    // Map the List<List<RosterShift>> to a flattened or grouped structure for the UI
                    // For this simple version, we'll just display them in a list
                    var flattenedList = data.SelectMany(d => d).ToList();
                    RosterCollectionView.ItemsSource = flattenedList;
                    
                    DateRangeLabel.Text = $"{_currentWeekStart:dd MMM} - {_currentWeekStart.AddDays(6):dd MMM yyyy}";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load roster: {ex.Message}", "OK");
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
            }
        }

        private async void OnShiftTapped(object sender, EventArgs e)
        {
            var shift = (sender as Element)?.BindingContext as RosterShift;
            if (shift == null) return;

            int currentGuardId = int.Parse(Preferences.Get("GuardId", "0"));

            // Determine available actions based on current status
            if (shift.Status == (int)RosterShiftStatus.Pushed)
            {
                // Unassigned -> Accept
                bool accept = await DisplayAlert("Accept Shift", "Do you want to accept this shift?", "Yes", "No");
                if (accept)
                {
                    await UpdateStatus(shift, RosterShiftStatus.Accepted);
                }
            }
            else if (shift.Status == (int)RosterShiftStatus.Accepted && shift.IsMyShift(currentGuardId))
            {
                // Accepted & Mine -> Decline
                string reason = await DisplayPromptAsync("Decline Shift", "Please enter a reason for cancellation:", "Decline", "Cancel");
                if (reason != null)
                {
                    await UpdateStatus(shift, RosterShiftStatus.Declined, reason);
                }
            }
            else if (shift.Status == (int)RosterShiftStatus.Declined)
            {
                // Declined -> Anyone can pick up as Relief Guard
                bool acceptRelief = await DisplayAlert("Accept Relief", "This shift was declined. Do you want to accept it as a Relief Guard?", "Yes", "No");
                if (acceptRelief)
                {
                    await UpdateStatus(shift, RosterShiftStatus.Accepted);
                }
            }
        }

        private async Task UpdateStatus(RosterShift shift, RosterShiftStatus newStatus, string reason = "")
        {
            LoadingOverlay.IsVisible = true;
            try
            {
                var model = new RosterStatusUpdateModel
                {
                    ShiftId = shift.Id,
                    NewStatus = (int)newStatus,
                    ExpectedStatus = shift.Status, // Concurrency check value
                    Reason = reason
                };

                var (success, message) = await _apiService.UpdateShiftStatusAsync(model);
                
                if (success)
                {
                    await LoadRosterData(); // Refresh UI
                }
                else
                {
                    await DisplayAlert("Status Update Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
            }
        }

        private async void OnPrevWeekClicked(object sender, EventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(-7);
            await LoadRosterData();
        }

        private async void OnNextWeekClicked(object sender, EventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(7);
            await LoadRosterData();
        }

        private async void OnHomeClicked(object sender, EventArgs e)
        {
            Application.Current.MainPage = new MainPage();
        }

        private DateTime GetStartOfWeek(DateTime dt)
        {
            // Logic to match backend (Monday start by default)
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }

    // Helper to resolve services in code-behind if needed
    public static class ServiceHelper
    {
        public static T GetService<T>() => Current.GetService<T>();
        public static IServiceProvider Current => 
#if ANDROID
            MauiApplication.Current.Services;
#elif IOS || MACCATALYST
            MauiUIApplicationDelegate.Current.Services;
#else
            null;
#endif
    }
}
