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

                if (roster != null && roster.Days.Count > 0)
                {
                    WeekRangeLabel.Text = roster.WeekRange;
                    
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
                
                // Trigger UI refresh for this item since RosterDay isn't INPC (it's a simple model)
                // We recreate the collection or use a more advanced approach if needed.
                // For simplicity with this demo, we can just refresh the BindableLayout.
                var index = Days.IndexOf(day);
                if (index != -1)
                {
                    Days[index] = null; // Forces refresh
                    Days[index] = day;
                }
            }
        }

        private void OnHomeClicked(object sender, EventArgs e)
        {
            Application.Current.MainPage = new MainPage();
        }
    }
}
