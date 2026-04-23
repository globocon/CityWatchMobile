using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace C4iSytemsMobApp
{
    public partial class RosterPage : ContentPage
    {
        private readonly IGuardApiServices _guardApiServices;
        private DateTime _currentWeekStart;
        private ObservableCollection<RosterDay> _days;
        private HubConnection _hubConnection;

        public ObservableCollection<RosterDay> Days
        {
            get => _days;
            set
            {
                _days = value;
                OnPropertyChanged();
            }
        }

        public RosterPage()
        {
            InitializeComponent();
            _guardApiServices = IPlatformApplication.Current.Services.GetService<IGuardApiServices>();
            
            BindingContext = this;

            // Start with the current week (Monday-based)
            _currentWeekStart = GetStartOfWeek(DateTime.Today);
            LoadRoster(_currentWeekStart);

            // Initialize SignalR for real-time updates
            InitializeSignalR();
        }

        private async void InitializeSignalR()
        {
            try
            {
                string hubUrl = $"{AppConfig.MobileSignalRBaseUrl}/MobileAppSignalRHub";
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<object>("RefreshRoster", (data) =>
                {
                    // Update UI when a broadcast is received
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LoadRoster(_currentWeekStart);
                    });
                });

                await _hubConnection.StartAsync();
                Debug.WriteLine("SignalR connected for Roster updates.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignalR connection failed: {ex.Message}");
            }
        }

        private async void LoadRoster(DateTime startDate)
        {
            GlobalLoading.IsRunning = true;
            DaysList.IsVisible = false;
            NoRosterLabel.IsVisible = false;

            try
            {
                DateTime endDate = startDate.AddDays(6);
                var roster = await _guardApiServices.GetGuardRosterAsync(startDate, endDate);

                if (roster != null)
                {
                    WeekRangeLabel.Text = roster.WeekRange;
                    
                    // Update Status Label and Color
                    StatusLabel.Text = roster.Status;
                    string status = (roster.Status ?? "").ToLower();
                    if (status.Contains("paid")) StatusLabel.TextColor = Colors.Red;
                    else if (status.Contains("live")) StatusLabel.TextColor = Colors.Green;
                    else if (status.Contains("inv")) StatusLabel.TextColor = Colors.Blue;
                    else StatusLabel.TextColor = Colors.Gray;
                }

                if (roster != null && roster.Days.Count > 0)
                {
                    Days = new ObservableCollection<RosterDay>(roster.Days);
                    DaysList.IsVisible = true;
                }
                else
                {
                    NoRosterLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Failed to load roster: " + ex.Message, "OK");
            }
            finally
            {
                GlobalLoading.IsRunning = false;
            }
        }

        private DateTime GetStartOfWeek(DateTime dt)
        {
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        private void OnPreviousWeekClicked(object sender, EventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(-7);
            LoadRoster(_currentWeekStart);
        }

        private void OnNextWeekClicked(object sender, EventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(7);
            LoadRoster(_currentWeekStart);
        }

        private void OnDayHeaderTapped(object sender, EventArgs e)
        {
            var day = (e as TappedEventArgs)?.Parameter as RosterDay;
            if (day != null)
            {
                day.IsExpanded = !day.IsExpanded;
            }
        }

        private void OnExpandAllClicked(object sender, EventArgs e)
        {
            if (Days == null) return;
            foreach (var day in Days)
            {
                day.IsExpanded = true;
            }
        }

        private void OnCollapseAllClicked(object sender, EventArgs e)
        {
            if (Days == null) return;
            foreach (var day in Days)
            {
                day.IsExpanded = false;
            }
        }

        private void OnHomeClicked(object sender, EventArgs e)
        {
            var volumeService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
            Application.Current.MainPage = new MainPage(volumeService);
        }

        private async void OnShiftTapped(object sender, EventArgs e)
        {
            var shift = (e as TappedEventArgs)?.Parameter as RosterShift;
            if (shift == null) return;

            int currentGuardId = int.Parse(Preferences.Get("GuardId", "0"));

            // Determine available actions based on current StatusCode
            if (shift.StatusCode == (int)RosterShiftStatus.Pushed)
            {
                bool accept = await DisplayAlert("Accept Shift", "Do you want to accept this shift?", "Yes", "No");
                if (accept)
                {
                    await UpdateStatus(shift, RosterShiftStatus.Accepted);
                }
            }
            else if (shift.StatusCode == (int)RosterShiftStatus.Accepted && 
                     (shift.GuardId == currentGuardId || shift.ReliefGuardId == currentGuardId))
            {
                // Predefined reasons for cancellation
                string action = await DisplayActionSheet("Reason for Relief", "Cancel", null, "Sick", "AL", "RDO", "LWOP", "Other");
                
                if (action == "Cancel" || string.IsNullOrEmpty(action)) return;

                string reason = action;
                if (action == "Other")
                {
                    reason = await DisplayPromptAsync("Decline Shift", "Please enter details for 'Other' reason:", "Decline", "Cancel");
                    if (string.IsNullOrEmpty(reason)) return;
                }

                await UpdateStatus(shift, RosterShiftStatus.Declined, reason);
            }
            else if (shift.StatusCode == (int)RosterShiftStatus.Declined)
            {
                bool acceptRelief = await DisplayAlert("Accept Relief", "This shift was declined. Do you want to accept it as a Relief Guard?", "Yes", "No");
                if (acceptRelief)
                {
                    await UpdateStatus(shift, RosterShiftStatus.Accepted);
                }
            }
        }

        private async Task UpdateStatus(RosterShift shift, RosterShiftStatus newStatus, string reason = "")
        {
            GlobalLoading.IsRunning = true;
            try
            {
                var model = new RosterStatusUpdateModel
                {
                    ShiftId = shift.Id,
                    NewStatus = (int)newStatus,
                    ExpectedStatus = shift.StatusCode,
                    Reason = reason
                };

                var (success, message) = await _guardApiServices.UpdateShiftStatusAsync(model);
                
                if (success)
                {
                    LoadRoster(_currentWeekStart); // Refresh
                    await DisplayAlert("Roster Update", "Shift status updated successfully.", "OK");
                }
                else
                {
                    await DisplayAlert("Roster Message", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Roster Error", "Unable to update shift. Please check your internet connection and try again.", "OK");
            }
            finally
            {
                GlobalLoading.IsRunning = false;
            }
        }
    }
}
