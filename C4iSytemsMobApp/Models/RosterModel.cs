using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace C4iSytemsMobApp.Models
{
    /// <summary>
    /// Represents a public holiday.
    /// </summary>
    public class Holiday
    {
        public int id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool RepeatYearly { get; set; }
        public string Reason { get; set; }
        public List<string> States { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a single shift within the roster.
    /// [Isolation]: Specifically for the Roster module.
    /// </summary>
    public class RosterShift : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int? GuardId { get; set; } // The ID of the guard assigned to this shift
        public string GuardName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
        public string Location { get; set; }
        public string Status { get; set; } // Friendly status or original status
        
        private int _statusCode;
        public int StatusCode 
        { 
            get => _statusCode; 
            set
            {
                if (_statusCode != value)
                {
                    _statusCode = value;
                    OnPropertyChanged();
                    // Notify that the whole object changed to refresh bindings to '.' (like the color converter)
                    OnPropertyChanged(string.Empty);
                }
            }
        } // From API (0=Pushed, 1=Accepted, etc.)
        
        // New fields for live API integration
        public int? ReliefGuardId { get; set; }
        public string ReliefGuardName { get; set; }
        public string ReliefReason { get; set; }
        public string ShiftType { get; set; } // Regular, Adhoc, etc.
        public string CallsignName { get; set; }
        public string DurationHours { get; set; }
        public string GuardLicense { get; set; }
        public string ReliefGuardLicense { get; set; }

        // UI Helpers
        public bool IsEditable { get; set; } // Whether the current guard can edit this shift
        public string DisplayName => ReliefGuardId != null ? ReliefGuardName : GuardName;
        public string DisplayLicense => ReliefGuardId != null ? ReliefGuardLicense : GuardLicense;
        public string DisplayIcon => ReliefGuardId != null ? "R" : "\uf007"; // \uf007 is user icon

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a day in the roster with its associated shifts.
    /// Used for grouping and the auto-expand UI logic.
    /// </summary>
    public class RosterDay : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        public string DayName => Date.ToString("ddd dd MMM");
        public bool IsToday => Date.Date == DateTime.Today.Date;
        public List<RosterShift> Shifts { get; set; } = new List<RosterShift>();
        
        // Holiday info
        public bool IsPublicHoliday { get; set; }
        public string HolidayReason { get; set; }

        // Property for UI expansion state
        private bool _isExpanded;
        public bool IsExpanded 
        { 
            get => _isExpanded; 
            set 
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Wrapper for the weekly roster view.
    /// </summary>
    public class WeeklyRoster
    {
        public string WeekRange { get; set; }
        public string SiteName { get; set; }
        public string ClientTypeName { get; set; }
        public string Status { get; set; } // Roster status (e.g., Live)
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<RosterDay> Days { get; set; } = new List<RosterDay>();
        public List<Holiday> Holidays { get; set; } = new List<Holiday>();
    }
}
