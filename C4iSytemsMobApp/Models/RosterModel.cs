using System;
using System.Collections.Generic;

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
    public class RosterShift
    {
        public int Id { get; set; }
        public string GuardName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
        public string Location { get; set; }
        public string Status { get; set; } // Friendly status or original status
        public int StatusCode { get; set; } // From API (0=Pushed, 1=Accepted, etc.)
        
        // New fields for live API integration
        public int? ReliefGuardId { get; set; }
        public string ReliefGuardName { get; set; }
        public string ReliefReason { get; set; }
        public string ShiftType { get; set; } // Regular, Adhoc, etc.
        public string CallsignName { get; set; }
        public string DurationHours { get; set; }
        public string GuardLicense { get; set; }
        public string ReliefGuardLicense { get; set; }
    }

    /// <summary>
    /// Represents a day in the roster with its associated shifts.
    /// Used for grouping and the auto-expand UI logic.
    /// </summary>
    public class RosterDay
    {
        public DateTime Date { get; set; }
        public string DayName => Date.ToString("ddd dd MMM");
        public bool IsToday => Date.Date == DateTime.Today.Date;
        public List<RosterShift> Shifts { get; set; } = new List<RosterShift>();
        
        // Holiday info
        public bool IsPublicHoliday { get; set; }
        public string HolidayReason { get; set; }

        // Property for UI expansion state
        public bool IsExpanded { get; set; }
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
