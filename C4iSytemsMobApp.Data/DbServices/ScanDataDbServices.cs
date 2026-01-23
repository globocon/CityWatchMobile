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
        public Task RefreshPrePopulatedActivitesButtonList(List<ActivityModel> activites);
        public Task ClearPrePopulatedActivitesButtonList();
        public Task<List<ActivityModel>> GetPrePopulatedActivitesButtonList();
        public Task<bool> SaveLogActivityCacheData(PostActivityRequestLocalCache record);
        public Task<bool> SaveLogActivityDocumentsCacheData(OfflineFilesRecords record);
    }

    public class ScanDataDbServices : IScanDataDbServices
    {
        private readonly Func<AppDbContext> _dbFactory;        
        public ScanDataDbServices(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<ClientSiteSmartWandTagsHitLogCache>> GetAllSavedScanDataAsync()
        {
            using var _db = _dbFactory();
            return await _db.ClientSiteSmartWandTagsHitLogCache.AsNoTracking().ToListAsync();
        }

        public int GetCacheRecordsCount()
        {
            using var _db = _dbFactory();
            int cacheCount = _db.ClientSiteSmartWandTagsHitLogCache.Count();
            cacheCount += _db.PostActivityRequestLocalCache.Count();
            cacheCount += _db.OfflineFilesRecords.Count();
            return cacheCount;
        }

        public async Task SaveScanData(ClientSiteSmartWandTagsHitLogCache record)
        {
            using var newdb = _dbFactory();
            newdb.ClientSiteSmartWandTagsHitLogCache.Add(record);
            await newdb.SaveChangesAsync();
        }

        public async Task RefreshSmartWandTagsList(List<ClientSiteSmartWandTagsLocal> swtags)
        {
            using var _db = _dbFactory();
            var r = await _db.ClientSiteSmartWandTagsLocal.ToListAsync();
            _db.ClientSiteSmartWandTagsLocal.RemoveRange(r);
            await _db.AddRangeAsync(swtags);
            await _db.SaveChangesAsync();
        }

        public ClientSiteSmartWandTagsHitLogCache GetLastScannedTagDateTime(int siteId, string tagUid)
        {
            using var _db = _dbFactory();
            return _db.ClientSiteSmartWandTagsHitLogCache
                .Where(x => x.TagUId == tagUid && x.LoggedInClientSiteId == siteId)
                .OrderByDescending(x => x.HitUtcDateTime)
                .Take(1)
                .SingleOrDefault();
        }
        public ClientSiteSmartWandTagsLocal GetSmartWandTagDetailOfTag(string tagUid)
        {
            using var _db = _dbFactory();
            return _db.ClientSiteSmartWandTagsLocal.Where(x => x.UId == tagUid).FirstOrDefault();
        }

        public async Task RefreshPrePopulatedActivitesButtonList(List<ActivityModel> activites)
        {
            using var _db = _dbFactory();
            var r = await _db.ActivityModel.ToListAsync();
            if (r != null && r.Any())
                _db.ActivityModel.RemoveRange(r);

            await _db.AddRangeAsync(activites);
            await _db.SaveChangesAsync();
        }
        public async Task ClearPrePopulatedActivitesButtonList()
        {
            using var _db = _dbFactory();
            var r = await _db.ActivityModel.ToListAsync();
            _db.ActivityModel.RemoveRange(r);
            await _db.SaveChangesAsync();
        }
        public async Task<List<ActivityModel>> GetPrePopulatedActivitesButtonList()
        {
            using var _db = _dbFactory();
            var r = await _db.ActivityModel.AsNoTracking().ToListAsync();
            return r;
        }

        public async Task<bool> SaveLogActivityCacheData(PostActivityRequestLocalCache record)
        {
            var r = false;
            try
            {
                using var newdb = _dbFactory();
                newdb.PostActivityRequestLocalCache.Add(record);
                await newdb.SaveChangesAsync();
                r = true;
            }
            catch (Exception)
            {

            }

            return r;
        }

        public async Task<bool> SaveLogActivityDocumentsCacheData(OfflineFilesRecords record)
        {
            var r = false;
            try
            {
                using var newdb = _dbFactory();
                newdb.OfflineFilesRecords.Add(record);
                await newdb.SaveChangesAsync();
                r = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return r;
        }


    }
}
