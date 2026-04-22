using System;

namespace C4iSytemsMobApp.Models
{
    public class RosterShift
    {
        public int Id { get; set; }
        public string ShiftStart { get; set; }
        public string ShiftEnd { get; set; }
        public int? GuardId { get; set; }
        public string GuardName { get; set; }
        public string GuardLicense { get; set; }
        public int? ReliefGuardId { get; set; }
        public string ReliefGuardName { get; set; }
        public string ReliefGuardLicense { get; set; }
        public string ReliefProviderName { get; set; }
        public string ReliefReason { get; set; }
        public string ShiftType { get; set; }
        public int Status { get; set; }
        public string CallsignName { get; set; }
        public string DurationHours { get; set; }
        public decimal SellRate { get; set; }
        public decimal BuyRate { get; set; }

        // Helper property to determine if the shift belongs to the current guard
        public bool IsMyShift(int currentGuardId) => GuardId == currentGuardId || ReliefGuardId == currentGuardId;
        
        // Helper to get effective guard name
        public string DisplayGuardName => !string.IsNullOrEmpty(ReliefGuardName) ? ReliefGuardName : GuardName;
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
