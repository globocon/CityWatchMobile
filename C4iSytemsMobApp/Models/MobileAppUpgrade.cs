

namespace C4iSytemsMobApp.Models
{
    public class MobileAppUpgrade
    {
        public int Id { get; set; }
        public string AppType { get; set; }
        public int AppVersionMajor { get; set; }
        public int AppVersionMinor { get; set; }
        public int AppVersionPatch { get; set; }
        public string? AppDownloadUrl { get; set; }
        public string? AppVersionNotes { get; set; }
        public DateTime RecordCreateDTM { get; set; }
        public int TotalDownloadCount { get; set; } 
        public bool IsActive { get; set; }
        public string FileName { get; set; }
    }
}
