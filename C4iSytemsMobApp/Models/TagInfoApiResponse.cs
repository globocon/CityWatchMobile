
namespace C4iSytemsMobApp.Models
{
    public class TagInfoApiResponse
    {
        public bool IsSuccess { get; set; }
        public bool tagFound { get; set; }
        public string message { get; set; }
        public string tagInfoLabel { get; set; }
        public int ScannedFromLinkedSite { get; set; }
        public int RowIdInServer { get; set; }
        public int TagSiteId { get; set; }
        public string TagSiteName { get; set; }

        /// <summary>
        /// P1#375: set by ScanDuplicateGuard when the scan was refused before any request was
        /// made - the same tag within 5 minutes, or the same tag still being recorded. The
        /// server never sets it. Call sites use it to tell a refused repeat apart from a real
        /// failure, so continuous BLE scanning does not spend the window shouting at the guard.
        /// </summary>
        public bool IsDuplicateScan { get; set; }

        /// <summary>
        /// Patrol Randomisation: the next checkpoint and the acknowledgement of this strike. Null
        /// unless the site has randomisation set up (older servers never send it).
        /// </summary>
        public PatrolRandomisationInstruction PatrolRandomisation { get; set; }
    }
}
