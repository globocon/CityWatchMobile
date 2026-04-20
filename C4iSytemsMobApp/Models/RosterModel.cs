using System;
using System.Collections.Generic;

namespace C4iSytemsMobApp.Models
{
    /// <summary>
    /// Represents a single shift within the roster.
    /// [Isolation]: Specifically for the Roster module.
    /// </summary>
    public class RosterShift
    {
        public string GuardName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
        public string Location { get; set; }
        public string Status { get; set; } // e.g., Normal, Relief
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
        
        // Property for UI expansion state
        public bool IsExpanded { get; set; }
    }

    /// <summary>
    /// Wrapper for the weekly roster view.
    /// </summary>
    public class WeeklyRoster
    {
        public string WeekRange { get; set; }
        public List<RosterDay> Days { get; set; } = new List<RosterDay>();
    }
}
