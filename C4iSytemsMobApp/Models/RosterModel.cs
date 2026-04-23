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
                    UpdateBackgroundBrush();
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
        public string ReliefProviderName { get; set; }
        public decimal SellRate { get; set; }
        public decimal BuyRate { get; set; }

        // UI Helpers
        public bool IsEditable { get; set; } // Whether the current guard can edit this shift
        public string DisplayName => (ReliefGuardId != null && ReliefGuardId > 0) ? ReliefGuardName : GuardName;
        public string DisplayLicense => (ReliefGuardId != null && ReliefGuardId > 0) ? ReliefGuardLicense : GuardLicense;
        public string DisplayIcon => (ReliefGuardId != null && ReliefGuardId > 0) ? "R" : "\uf007"; // \uf007 is user icon

        private Brush _backgroundBrush;
        public Brush BackgroundBrush
        {
            get => _backgroundBrush;
            set
            {
                _backgroundBrush = value;
                OnPropertyChanged();
            }
        }

        public void UpdateBackgroundBrush()
        {
            // 1. Declined Status (Black)
            if (StatusCode == 2)
            {
                BackgroundBrush = new SolidColorBrush(Colors.Black);
            }
            // 2. Relief Shifts (Purple) - Highest priority if accepted or pending relief
            else if (ReliefGuardId != null && ReliefGuardId > 0)
            {
                BackgroundBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb("#6f42c1"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb("#4a148c"), Offset = 1.0f }
                    }
                };
            }
            // 3. Accepted Status (Green)
            else if (StatusCode == 1)
            {
                bool isAdhoc = ShiftType == "Adhoc";
                BackgroundBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        // Light Green for Regular, Dark Green for Adhoc
                        new GradientStop { Color = Color.FromArgb(isAdhoc ? "#1B5E20" : "#90EE90"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb(isAdhoc ? "#2E7D32" : "#32CD32"), Offset = 1.0f }
                    }
                };
            }
            // 4. Not Accepted (Orange)
            else
            {
                bool isAdhoc = ShiftType == "Adhoc";
                BackgroundBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(isAdhoc ? "#FF8F00" : "#FFB74D"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb(isAdhoc ? "#E65100" : "#FB8C00"), Offset = 1.0f }
                    }
                };
            }
        }

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
        public int AvailableShiftsCount { get; set; } // Count of declined shifts available for relief
        
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

    /// <summary>
    /// Model for updating roster shift status from mobile.
    /// </summary>
    public class RosterStatusUpdateModel
    {
        public int ShiftId { get; set; }
        public int NewStatus { get; set; }
        public int ExpectedStatus { get; set; }
        public int CallingGuardId { get; set; }
        public string Reason { get; set; }
    }
}
