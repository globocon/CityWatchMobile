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
        private ObservableCollection<SiteRoster> _siteRosters;
        private HubConnection _hubConnection;

        public ObservableCollection<SiteRoster> SiteRosters
        {
            get => _siteRosters;
            set
            {
                _siteRosters = value;
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

        private async Task LoadRoster(DateTime startDate)
        {
            GlobalLoading.IsRunning = true;
            
            if (SiteRosters == null || SiteRosters.Count == 0)
            {
                RosterContainer.IsVisible = false;
                NoRosterLabel.IsVisible = false;
            }

            try
            {
                int currentSiteId = int.Parse(Preferences.Get("SelectedClientSiteId", "0"));
                
                var linkedSitesResp = await _guardApiServices.GetLinkedSitesAsync(currentSiteId);
                List<SiteInfo> sitesToFetch = new List<SiteInfo>();
                bool isLinkedGroup = false;

                if (linkedSitesResp != null && linkedSitesResp.IsLinkedSiteGroup && linkedSitesResp.Sites.Count > 0)
                {
                    isLinkedGroup = true;
                    sitesToFetch = linkedSitesResp.Sites;
                }
                else
                {
                    // Fallback to current single site
                    sitesToFetch.Add(new SiteInfo { SiteId = currentSiteId, SiteName = "Current Site" });
                }

                DateTime endDate = startDate.AddDays(6);
                var newSiteRosters = new ObservableCollection<SiteRoster>();
                var validRosters = new List<(SiteInfo site, WeeklyRoster roster)>();

                foreach (var site in sitesToFetch)
                {
                    var roster = await _guardApiServices.GetGuardRosterAsync(startDate, endDate, site.SiteId);

                    if (roster != null && roster.Days.Count > 0)
                    {
                        validRosters.Add((site, roster));
                    }
                }

                if (isLinkedGroup)
                {
                    var filtered = validRosters.Where(vr => vr.roster.Days.Any(d => d.Shifts != null && d.Shifts.Count > 0)).ToList();
                    
                    if (filtered.Count > 0)
                    {
                        validRosters = filtered;
                    }
                    else
                    {
                        // Fallback if all sites are empty: show the main site empty
                        validRosters = validRosters.Where(vr => vr.site.SiteId == currentSiteId).ToList();
                    }
                }

                bool hasAnyRosters = validRosters.Count > 0;

                if (hasAnyRosters)
                {
                    // Set the overall status and week range to the first valid one we find
                    var firstRoster = validRosters[0].roster;
                    if (string.IsNullOrEmpty(WeekRangeLabel.Text) || WeekRangeLabel.Text == "Loading week...")
                    {
                        WeekRangeLabel.Text = firstRoster.WeekRange;
                        StatusLabel.Text = firstRoster.Status;
                        string status = (firstRoster.Status ?? "").ToLower();
                        if (status.Contains("paid")) StatusLabel.TextColor = Colors.Red;
                        else if (status.Contains("live")) StatusLabel.TextColor = Colors.Green;
                        else if (status.Contains("inv")) StatusLabel.TextColor = Colors.Blue;
                        else StatusLabel.TextColor = Colors.Gray;
                    }

                    foreach (var validItem in validRosters)
                    {
                        var site = validItem.site;
                        var roster = validItem.roster;

                        foreach (var day in roster.Days)
                        {
                            day.PendingCount = day.Shifts?.Count(s => s.StatusCode == 0) ?? 0;
                            day.AcceptedCount = day.Shifts?.Count(s => s.StatusCode == 1) ?? 0;
                            day.OpenCount = day.Shifts?.Count(s => s.StatusCode == 2) ?? 0;
                        }

                        bool showSiteBar = isLinkedGroup;
                        bool isExpanded = !isLinkedGroup || (isLinkedGroup && validRosters.Count == 1);

                        var siteRoster = new SiteRoster
                        {
                            SiteId = site.SiteId,
                            SiteName = site.SiteName,
                            ShowSiteBar = showSiteBar,
                            IsExpanded = isExpanded,
                            Days = new ObservableCollection<RosterDay>(roster.Days)
                        };
                        newSiteRosters.Add(siteRoster);
                    }
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (hasAnyRosters)
                    {
                        SiteRosters = newSiteRosters;
                        RosterContainer.IsVisible = true;
                        NoRosterLabel.IsVisible = false;
                    }
                    else
                    {
                        NoRosterLabel.IsVisible = true;
                        RosterContainer.IsVisible = false;
                    }
                });
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
            if (SiteRosters == null) return;
            foreach (var site in SiteRosters)
            {
                foreach (var day in site.Days)
                {
                    day.IsExpanded = true;
                }
            }
        }

        private void OnCollapseAllClicked(object sender, EventArgs e)
        {
            if (SiteRosters == null) return;
            foreach (var site in SiteRosters)
            {
                foreach (var day in site.Days)
                {
                    day.IsExpanded = false;
                }
            }
        }

        private void OnSiteBarTapped(object sender, EventArgs e)
        {
            var site = (e as TappedEventArgs)?.Parameter as SiteRoster;
            if (site != null)
            {
                site.IsExpanded = !site.IsExpanded;
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

            // Requirement: dont allow users to click if can't change thir shift
            if (!shift.IsEditable) return;

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
            else if (shift.StatusCode == (int)RosterShiftStatus.Accepted)
            {
                // Predefined reasons for cancellation
                string action = await DisplayActionSheet("Reason for Relief", "Cancel", null, "Sick", "AL", "RDO", "LWOP", "Other");
                
                if (action == "Cancel" || string.IsNullOrEmpty(action)) return;

                string reason = action;
                if (action == "Other")
                {
                    reason = await DisplayPromptAsync("Relief Details", "Please enter notes for 'Other' reason:", "Submit", "Cancel");
                    if (string.IsNullOrEmpty(reason)) return;
                }

                await UpdateStatus(shift, RosterShiftStatus.Declined, reason);
            }
            else if (shift.StatusCode == (int)RosterShiftStatus.Declined)
            {
                // Re-Acceptance or Relief Pickup
                string title = (shift.GuardId == currentGuardId) ? "Re-Accept Shift" : "Relief Pickup";
                string message = (shift.GuardId == currentGuardId) 
                    ? "This shift was declined. Do you want to re-accept it?" 
                    : "Do you want to pick up this shift as a Relief Guard?";

                bool accept = await DisplayAlert(title, message, "Yes", "No");
                if (accept)
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
                    // Immediate local update for better UX responsiveness
                    shift.StatusCode = (int)newStatus;
                    if (newStatus == RosterShiftStatus.Declined)
                    {
                        shift.ReliefGuardId = null;
                        shift.ReliefGuardName = null;
                    }
                    
                    // Brief delay to allow backend DB commit to settle
                    await Task.Delay(200);
                    await LoadRoster(_currentWeekStart); // Full Refresh
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
