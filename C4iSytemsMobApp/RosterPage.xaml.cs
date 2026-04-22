using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System.Collections.ObjectModel;

namespace C4iSytemsMobApp
{
    /// <summary>
    /// [Roster Module] - Logic for the Roster view.
    /// Handles week navigation and the day-wise expandable roster list.
    /// </summary>
    public partial class RosterPage : ContentPage
    {
        private readonly IGuardApiServices _guardApiServices;
        private DateTime _currentWeekStart;
        private ObservableCollection<RosterDay> _days;

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
        }

        /// <summary>
        /// Fetches roster data from the service and updates the UI.
        /// [Auto-Expand]: Expands the current day by default.
        /// </summary>
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
                    // Auto-expand the current day
                    foreach (var day in roster.Days)
                    {
                        if (day.IsToday)
                        {
                            day.IsExpanded = true;
                        }
                    }

                    Days = new ObservableCollection<RosterDay>(roster.Days);
                    DaysList.IsVisible = true;
                }
                else
                {
                    NoRosterLabel.IsVisible = true;
                    await DisplayAlert("Roster", "this site has no roster assigned", "OK");
                    OnHomeClicked(null, null); // Redirect back to Home
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

        /// <summary>
        /// Logic to determine the first day (Monday) of the week for a given date.
        /// </summary>
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

        /// <summary>
        /// Toggles the expansion state of a day section.
        /// </summary>
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

        /// <summary>
        /// Handles click event on a shift card.
        /// Toggles between Accepted (1) and Pushed (0).
        /// Only the currently logged-in guard can update their own shifts.
        /// </summary>
        private async void OnShiftTapped(object sender, EventArgs e)
        {
            var shift = (e as TappedEventArgs)?.Parameter as RosterShift;
            if (shift == null) return;

            int currentGuardId = _guardApiServices.CurrentGuardId;

            // Security Check: Only allow if shift belongs to current guard (Primary or Relief)
            bool isPrimaryGuard = shift.GuardId == currentGuardId;
            bool isReliefGuard = shift.ReliefGuardId == currentGuardId;

            if (!isPrimaryGuard && !isReliefGuard)
            {
                await DisplayAlert("Roster", "You can only update your own roster shifts.", "OK");
                return;
            }

            // Toggle status logic
            // 1 = Accepted (Green), 0 = Pushed/Not Accepted (Orange)
            if (shift.StatusCode == 1)
            {
                shift.StatusCode = 0;
            }
            else
            {
                shift.StatusCode = 1;
            }

            // Note: Next step is to call the API to persist this change
            // await _guardApiServices.UpdateRosterStatusAsync(shift.Id, shift.StatusCode);
        }
    }
}
