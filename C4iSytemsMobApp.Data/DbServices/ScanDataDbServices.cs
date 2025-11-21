using C4iSytemsMobApp.Data.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Data.DbServices
{
    public interface IScanDataDbServices
    {
        public Task<List<ClientSiteSmartWandTagsHitLogCache>> GetAllSavedScanDataAsync();
        public int GetCacheRecordsCount();
        public Task SaveScanData(ClientSiteSmartWandTagsHitLogCache record);

        public Task RefreshSmartWandTagsList(List<ClientSiteSmartWandTagsLocal> swtags);
        public ClientSiteSmartWandTagsHitLogCache GetLastScannedTagDateTime(int siteId, string tagUid);
        public ClientSiteSmartWandTagsLocal GetSmartWandTagDetailOfTag(string tagUid);
    }

    public class ScanDataDbServices : IScanDataDbServices
    {
        private readonly AppDbContext _db;
        public ScanDataDbServices(AppDbContext db) {
            _db = db;
        }

        public async Task<List<ClientSiteSmartWandTagsHitLogCache>> GetAllSavedScanDataAsync() => await _db.ClientSiteSmartWandTagsHitLogCache.AsNoTracking().ToListAsync();

        public int GetCacheRecordsCount() => _db.ClientSiteSmartWandTagsHitLogCache.Count();

        public async Task SaveScanData(ClientSiteSmartWandTagsHitLogCache record)
        {
            _db.ClientSiteSmartWandTagsHitLogCache.Add(record);
            await _db.SaveChangesAsync();
        }

        public async Task RefreshSmartWandTagsList(List<ClientSiteSmartWandTagsLocal> swtags) {
            var r = await _db.ClientSiteSmartWandTagsLocal.ToListAsync();
            _db.ClientSiteSmartWandTagsLocal.RemoveRange(r);
            await _db.AddRangeAsync(swtags);
            await _db.SaveChangesAsync();
        }

        public ClientSiteSmartWandTagsHitLogCache GetLastScannedTagDateTime(int siteId, string tagUid)
        {
            return _db.ClientSiteSmartWandTagsHitLogCache
                .Where(x => x.TagUId == tagUid && x.LoggedInClientSiteId == siteId)
                .OrderByDescending(x => x.HitUtcDateTime)
                .Take(1)
                .SingleOrDefault();
        }
        public ClientSiteSmartWandTagsLocal GetSmartWandTagDetailOfTag(string tagUid)
        {
            return _db.ClientSiteSmartWandTagsLocal.Where(x=> x.UId == tagUid).FirstOrDefault();
        }
    }
}
