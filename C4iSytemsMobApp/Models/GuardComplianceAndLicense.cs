
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public bool DateType { get; set; }
        public bool IsDateFilterEnabledHidden { get; set; }
        public bool HRBanEdit { get; set; }
        public string IsLogin { get; set; }
        public string StatusColor { get; set; }

        //public string StatusColor
        //{
        //    get
        //    {
        //        // Default
        //        var statusColor = "green";

        //        // If DateType is true → always green
        //        if (DateType)
        //            return "green";

        //        // If ExpiryDate exists
        //        if (ExpiryDate.HasValue)
        //        {
        //            var currentDate = DateTime.UtcNow.Date;
        //            var expiryDate = ExpiryDate.Value.Date;

        //            var daysDifference = (expiryDate - currentDate).TotalDays;

        //            // Expired → red (highest priority)
        //            if (expiryDate < currentDate && !DateType)
        //            {
        //                statusColor = "red";
        //            }
        //            // Expiring within 45 days → yellow
        //            else if (daysDifference <= 45)
        //            {
        //                statusColor = "yellow";
        //            }
        //        }

        //        return statusColor;
        //    }
        //}

    }
}
