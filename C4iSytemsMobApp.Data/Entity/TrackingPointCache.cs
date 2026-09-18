using System;
using System.ComponentModel.DataAnnotations;

namespace C4iSytemsMobApp.Data.Entity
{
    /// <summary>
    /// Tracking feature pack: local ring buffer for GPS points awaiting upload.
    /// Follows the same offline-cache pattern as PatrolCarLogRequestCache — persist first,
    /// upload in batches, delete only on confirmed server acknowledgement. Capped at
    /// ~10,000 rows (a full 12-hour shift fully offline) by the store's own trim.
    /// </summary>
    public class TrackingPointCache
    {
        [Key]
        public int Id { get; set; }

        /// <summary>ClientSiteSmartWand.Id — the tracking unit key.</summary>
        public int UnitId { get; set; }

        /// <summary>Server-issued session id; points without a session are never captured.</summary>
        public Guid SessionId { get; set; }

        /// <summary>Device-assigned monotonic sequence; the server's dedupe key with unit+session.</summary>
        public int Seq { get; set; }

        public DateTime RecordedUtc { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public double? AccuracyM { get; set; }

        public double? SpeedKph { get; set; }

        public double? HeadingDeg { get; set; }

        public byte? BatteryPct { get; set; }

        public bool IsMock { get; set; }

        /// <summary>transit | live | duress (nfcAnchor points are server-derived from scans).</summary>
        public string Source { get; set; } = "transit";

        /// <summary>True once the point missed its first upload window and is being replayed —
        /// the server flags these Backfilled so they never animate the live map.</summary>
        public bool IsBackfill { get; set; }
    }
}
