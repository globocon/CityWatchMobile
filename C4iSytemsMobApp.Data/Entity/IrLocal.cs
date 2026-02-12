using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Data.Entity
{
    public class IrNotifiedByLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string NotifiedBy { get; set; }
    }

    public class IrFeedbackTemplateViewModelLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Text { get; set; }
        public int? Type { get; set; }
        public string FeedbackTypeName { get; set; }
        public string BackgroundColour { get; set; }
        public string TextColor { get; set; }
        public int DeleteStatus { get; set; }
        public bool SendtoRC { get; set; }
    }

    public class AudioAndMultimediaLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int AudioType { get; set; }
        public string Label { get; set; }
        public string ServerUrl { get; set; }
        public string LocalFilePath { get; set; }
    }

    public class irOfflineFilesAttachmentsCache
    {
        [Key]
        public Guid UniqueRecordId { get; set; } = Guid.NewGuid();
        public string IrId { get; set; }
        public string FileNameActual { get; set; }
        public string FileNameCache { get; set; }
        public string FileNameWithPathCache { get; set; }
        public DateTime? EventDateTimeLocal { get; set; } 
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; }
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
        public bool IsSynced { get; set; } = false;        
        public int guardId { get; set; }
        public int clientsiteId { get; set; }
        public int userId { get; set; }
        public string gps { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }

    }

    public class irOfflineCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string IrId { get; set; }        
        public string IncidentRequest { get; set; }
        public DateTime? EventDateTimeLocal { get; set; }
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; } 
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
        public bool IsSynced { get; set; } = false;
        public Guid UniqueRecordId { get; set; } = Guid.NewGuid();
        public int guardId { get; set; }
        public int clientsiteId { get; set; }
        public int userId { get; set; }
        public string gps { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
    }

    


}
