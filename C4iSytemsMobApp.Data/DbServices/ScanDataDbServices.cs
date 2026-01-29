using C4iSytemsMobApp.Data.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public Task RefreshPatrolCarCacheList(List<PatrolCarLogCache> patrolCarLogs);
        public Task ClearPatrolCarCacheList();
        public Task<List<PatrolCarLogCache>> GetPatrolCarCacheList();
        public Task<bool> SavePatrolCarLogRequestCacheData(PatrolCarLogRequestCache record);
        public Task<bool> UpdatePatrolCarLogCacheData(PatrolCarLogCache record);

        public Task RefreshCustomFieldCacheList(List<CustomFieldLogHeadCache> customFieldLogs);
        public Task ClearCustomFieldLogCacheList();
        public Task<List<CustomFieldLogHeadCache>> GetCustomFieldLogCacheList();
        public Task<CustomFieldLogHeadCache> GetCustomFieldLogCacheListByKeyValue(string key, string keyvalue);
        public Task<bool> UpdateCustomFieldLogCacheList(CustomFieldLogHeadCache editedrecord);
        public Task<bool> SaveCustomFieldLogRequestCacheData(CustomFieldLogRequestHeadCache record);
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
            cacheCount += _db.CustomFieldLogRequestHeadCache.Count();
            cacheCount += _db.PatrolCarLogRequestCache.Count();
            // refer and modify in SyncService.GetCurrentCacheCountAsync()


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

        public async Task RefreshPatrolCarCacheList(List<PatrolCarLogCache> patrolCarLogs)
        {
            using var _db = _dbFactory();
            var r = await _db.PatrolCarLogCache.ToListAsync();
            if (r != null && r.Any())
                _db.PatrolCarLogCache.RemoveRange(r);

            var h = await _db.ClientSitePatrolCarCache.ToListAsync();
            if (h != null && h.Any())
                _db.ClientSitePatrolCarCache.RemoveRange(h);

            //if (patrolCarLogs != null)
            //{
            //    List<ClientSitePatrolCarCache> clientSitePatrolCarCaches = new List<ClientSitePatrolCarCache>();
            //    foreach (var pcl in patrolCarLogs)
            //    {
            //        clientSitePatrolCarCaches.Add(pcl.ClientSitePatrolCar);
            //    }
            //    if (clientSitePatrolCarCaches != null && clientSitePatrolCarCaches.Any())
            //    {
            //        await _db.AddRangeAsync(clientSitePatrolCarCaches);
            //    }
            //}

            await _db.AddRangeAsync(patrolCarLogs);
            await _db.SaveChangesAsync();
        }
        public async Task ClearPatrolCarCacheList()
        {
            using var _db = _dbFactory();
            var r = await _db.PatrolCarLogCache.ToListAsync();
            if (r != null && r.Any())
                _db.PatrolCarLogCache.RemoveRange(r);

            var h = await _db.ClientSitePatrolCarCache.ToListAsync();
            if (h != null && h.Any())
                _db.ClientSitePatrolCarCache.RemoveRange(h);

            await _db.SaveChangesAsync();
        }

        public async Task<List<PatrolCarLogCache>> GetPatrolCarCacheList()
        {
            using var _db = _dbFactory();
            var r = await _db.PatrolCarLogCache.Include(x => x.ClientSitePatrolCar).AsNoTracking().ToListAsync();
            return r;
        }

        public async Task<bool> SavePatrolCarLogRequestCacheData(PatrolCarLogRequestCache record)
        {
            var r = false;
            try
            {
                using var newdb = _dbFactory();
                newdb.PatrolCarLogRequestCache.Add(record);
                await newdb.SaveChangesAsync();
                r = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return r;
        }

        public async Task<bool> UpdatePatrolCarLogCacheData(PatrolCarLogCache record)
        {
            var r = false;
            try
            {
                using var newdb = _dbFactory();
                newdb.PatrolCarLogCache.Update(record);
                await newdb.SaveChangesAsync();
                r = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return r;
        }

        public async Task RefreshCustomFieldCacheList(List<CustomFieldLogHeadCache> customFieldLogs)
        {
            using var _db = _dbFactory();
            var r = await _db.CustomFieldLogDetailCache.ToListAsync();
            if (r != null && r.Any())
                _db.CustomFieldLogDetailCache.RemoveRange(r);

            var h = await _db.CustomFieldLogHeadCache.ToListAsync();
            if (h != null && h.Any())
                _db.CustomFieldLogHeadCache.RemoveRange(h);

            await _db.AddRangeAsync(customFieldLogs);
            await _db.SaveChangesAsync();
        }

        public async Task ClearCustomFieldLogCacheList()
        {
            using var _db = _dbFactory();
            var r = await _db.CustomFieldLogDetailCache.ToListAsync();
            if (r != null && r.Any())
                _db.CustomFieldLogDetailCache.RemoveRange(r);

            var h = await _db.CustomFieldLogHeadCache.ToListAsync();
            if (h != null && h.Any())
                _db.CustomFieldLogHeadCache.RemoveRange(h);

            await _db.SaveChangesAsync();
        }

        public async Task<List<CustomFieldLogHeadCache>> GetCustomFieldLogCacheList()
        {
            using var _db = _dbFactory();
            var r = await _db.CustomFieldLogHeadCache.Include(x => x.KeyValuePairs).AsNoTracking().ToListAsync();
            return r;
        }
        public async Task<CustomFieldLogHeadCache> GetCustomFieldLogCacheListByKeyValue(string key,string keyvalue)
        {
            using var _db = _dbFactory();
            var k = await _db.CustomFieldLogHeadCache
                    .Include(h => h.KeyValuePairs)
                    .Where(h => h.KeyValuePairs.Any(d => d.DictKey == key && d.DictValue == keyvalue))
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

            var r = await _db.CustomFieldLogHeadCache
                    .Include(h => h.KeyValuePairs)
                    .Where(h => h.Id == k.Id)
                    .FirstOrDefaultAsync();
            return r;
        }

        public async Task<bool> UpdateCustomFieldLogCacheList(CustomFieldLogHeadCache editedrecord)
        {
            var r = false;
            try
            {
                using var newdb = _dbFactory();
                newdb.CustomFieldLogHeadCache.Update(editedrecord);
                await newdb.SaveChangesAsync();
                r = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return r;
        }

        public async Task<bool> SaveCustomFieldLogRequestCacheData(CustomFieldLogRequestHeadCache record)
        {
            var r = false;
            try
            {
                using var newdb = _dbFactory();
                newdb.CustomFieldLogRequestHeadCache.Add(record);
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
