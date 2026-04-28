using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

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
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        } 

        private int? _reliefGuardId;
        public int? ReliefGuardId 
        { 
            get => _reliefGuardId; 
            set
            {
                if (_reliefGuardId != value)
                {
                    _reliefGuardId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(DisplayLicense));
                    OnPropertyChanged(nameof(DisplayIcon));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }
        public string ReliefGuardName { get; set; }
        public string ReliefReason { get; set; }
        private string _shiftType;
        public string ShiftType 
        { 
            get => _shiftType; 
            set
            {
                if (_shiftType != value)
                {
                    _shiftType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        } 
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

        public Brush StatusBrush
        {
            get
            {
                // 1. Declined Status (Black) - Match the header dot color #212121
                if (StatusCode == 2)
                {
                    // Using a very subtle gradient for black to ensure rendering stability on mobile
                    return new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop { Color = Color.FromArgb("#212121"), Offset = 0.1f },
                            new GradientStop { Color = Color.FromArgb("#1a1a1a"), Offset = 1.0f }
                        }
                    };
                }

                // 2. Accepted Status (Green)
                if (StatusCode == 1)
                {
                    bool isAdhoc = ShiftType == "Adhoc";
                    return new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop { Color = Color.FromArgb(isAdhoc ? "#1B5E20" : "#90EE90"), Offset = 0.1f },
                            new GradientStop { Color = Color.FromArgb(isAdhoc ? "#2E7D32" : "#32CD32"), Offset = 1.0f }
                        }
                    };
                }

                // 3. Relief Shifts (Purple) - Pushed Status (Not Accepted) for Normal Shifts
                if (ReliefGuardId != null && ReliefGuardId > 0 && ShiftType != "Adhoc")
                {
                    return new LinearGradientBrush
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

                // 4. Not Accepted/Pushed Status (Orange)
                bool isAdhocPushed = ShiftType == "Adhoc";
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(isAdhocPushed ? "#FF8F00" : "#FFB74D"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb(isAdhocPushed ? "#E65100" : "#FB8C00"), Offset = 1.0f }
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

    public class RosterDay : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        public string DayName => Date.ToString("ddd dd MMM");
        public bool IsToday => Date.Date == DateTime.Today.Date;
        private List<RosterShift> _shifts = new List<RosterShift>();
        public List<RosterShift> Shifts 
        { 
            get => _shifts; 
            set { _shifts = value; OnPropertyChanged(); } 
        }

        private int _pendingCount;
        public int PendingCount 
        { 
            get => _pendingCount; 
            set { _pendingCount = value; OnPropertyChanged(); } 
        }

        private int _acceptedCount;
        public int AcceptedCount 
        { 
            get => _acceptedCount; 
            set { _acceptedCount = value; OnPropertyChanged(); } 
        }

        private int _openCount;
        public int OpenCount 
        { 
            get => _openCount; 
            set { _openCount = value; OnPropertyChanged(); } 
        }
        
        private bool _isPublicHoliday;
        public bool IsPublicHoliday 
        { 
            get => _isPublicHoliday; 
            set { _isPublicHoliday = value; OnPropertyChanged(); } 
        }

        private string _holidayReason;
        public string HolidayReason 
        { 
            get => _holidayReason; 
            set { _holidayReason = value; OnPropertyChanged(); } 
        }

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
