using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace C4iSytemsMobApp.Models
{
    public class Holiday
    {
        public int id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool RepeatYearly { get; set; }
        public string Reason { get; set; }
        public List<string> States { get; set; } = new List<string>();
    }

    public class RosterShift : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int? GuardId { get; set; }
        public string GuardName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
        public string Location { get; set; }
        public string Status { get; set; } 
        
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
                }
            }
        } 
        
        public int? ReliefGuardId { get; set; }
        public string ReliefGuardName { get; set; }
        public string ReliefReason { get; set; }
        public string ShiftType { get; set; } 
        public string CallsignName { get; set; }
        public string DurationHours { get; set; }
        public string GuardLicense { get; set; }
        public string ReliefGuardLicense { get; set; }
        public string ReliefProviderName { get; set; }
        public decimal SellRate { get; set; }
        public decimal BuyRate { get; set; }

        public bool IsEditable { get; set; } 
        public string DisplayName => (ReliefGuardId != null && ReliefGuardId > 0) ? ReliefGuardName : GuardName;
        public string DisplayLicense => (ReliefGuardId != null && ReliefGuardId > 0) ? ReliefGuardLicense : GuardLicense;
        public string DisplayIcon => (ReliefGuardId != null && ReliefGuardId > 0) ? "R" : "\uf007";

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RosterDay : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        public string DayName => Date.ToString("ddd dd MMM");
        public bool IsToday => Date.Date == DateTime.Today.Date;
        public List<RosterShift> Shifts { get; set; } = new List<RosterShift>();
        public int PendingCount { get; set; }  
        public int AcceptedCount { get; set; } 
        public int OpenCount { get; set; }     
        
        public bool IsPublicHoliday { get; set; }
        public string HolidayReason { get; set; }

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

    public class WeeklyRoster
    {
        public string WeekRange { get; set; }
        public string SiteName { get; set; }
        public string ClientTypeName { get; set; }
        public string Status { get; set; } 
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<RosterDay> Days { get; set; } = new List<RosterDay>();
        public List<Holiday> Holidays { get; set; } = new List<Holiday>();
    }

    public class RosterStatusUpdateModel
    {
        public int ShiftId { get; set; }
        public int NewStatus { get; set; }
        public int ExpectedStatus { get; set; }
        public int CallingGuardId { get; set; }
        public string Reason { get; set; }
    }
}
