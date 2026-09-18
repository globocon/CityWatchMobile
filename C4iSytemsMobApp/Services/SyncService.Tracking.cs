using C4iSytemsMobApp.Services.Tracking;

namespace C4iSytemsMobApp.Services
{
    /// <summary>
    /// Tracking feature pack half of SyncService. Kept in its own file so SyncService.cs
    /// itself changed by two lines (the partial keyword and one call).
    /// </summary>
    public partial class SyncService
    {
        /// <summary>
        /// Replays buffered tracking points. Runs LAST in SyncAsync and swallows everything:
        /// a tracking failure must never abort the six existing syncs (ADD §4.4, RT7), and a
        /// tracking backlog must never delay NFC replay — different endpoint, different queue.
        /// </summary>
        private async Task SyncTrackingPointsCache()
        {
            try
            {
                await TrackingService.Instance.SyncOfflineBacklogAsync();
            }
            catch
            {
                // By design. The points stay cached; the next sync retries.
            }
        }
    }
}
