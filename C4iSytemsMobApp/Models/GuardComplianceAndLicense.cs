using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace C4iSytemsMobApp.Models
{
    public class GuardComplianceAndLicense
    {
        public int Id { get; set; }
        public int GuardId { get; set; }
        public string Description { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public HrGroup? HrGroup { get; set; }
        public string HrGroupText { get; set; }

        //[ForeignKey("GuardId")]
        //public Guard Guard { get; set; }

        public string CurrentDateTime { get; set; }
        public int Reminder1 { get; set; }
        public int Reminder2 { get; set; }
        public string LicenseNo { get; set; }

        [JsonIgnore]
        public bool DateType { get; set; }
        public bool dateType { get => DateType; set => DateType = value; }

        public bool IsDateFilterEnabledHidden { get; set; }
        public bool HRBanEdit { get; set; }
        
        [JsonIgnore]
        public int MasterDateType { get; set; }
        public int masterDateType { get => MasterDateType; set => MasterDateType = value; }
        public string IsLogin { get; set; }
        public string StatusColor { get; set; }

        [JsonIgnore]
        public string PendingDays
        {
            get
            {
                // Default
                var _pendingDays = "N/A";

                // If DateType is true → always green
                if (DateType)
                    return _pendingDays;

                // If ExpiryDate exists
                if (ExpiryDate.HasValue)
                {
                    var currentDate = DateTime.UtcNow.Date;
                    var expiryDate = ExpiryDate.Value.Date;

                    var daysDifference = (expiryDate - currentDate).TotalDays;

                    // Expired → red (highest priority)
                    if (expiryDate < currentDate && !DateType)
                    {
                        _pendingDays = "0 days";
                    }
                    // Expiring within 60 days → yellow
                    else if (daysDifference <= 60 && !DateType)
                    {
                        _pendingDays = daysDifference.ToString("N0");
                    }
                    else
                    {
                        _pendingDays = $"More than {(daysDifference -1 ).ToString("N0")} days";
                    }
                }

                return _pendingDays;
            }
        }

    }
}
