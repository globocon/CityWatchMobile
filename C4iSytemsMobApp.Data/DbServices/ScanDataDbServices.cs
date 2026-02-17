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
        public Task<ClientSiteSmartWandTagsLocal> GetSmartWandTagDetailOfTagAsync(string tagUid);
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

        public Task ClearRCLinkedDuressClientSitesList();
        public Task RefreshRCLinkedDuressClientSitesList(List<RCLinkedDuressClientSitesCache> rcLinkedDuressClientSites);
        public Task<List<RCLinkedDuressClientSitesCache>> GetRCLinkedDuressClientSitesListBySiteId(int siteId);

        public Task ClearIrClientSitesTypesLocalList();
        public Task RefreshIrClientSitesTypesLocalList(List<ClientSiteTypeLocal> irClientTypes);
        public Task<List<ClientSiteTypeLocal>> GetIrClientSitesTypesLocalList();

        public Task ClearIrClientSitesLocalList();
        public Task RefreshIrClientSitesLocalList(List<ClientSitesLocal> irClientSites);
        public Task<List<ClientSitesLocal>> GetIrClientSitesLocalList();
        public Task<List<ClientSitesLocal>> GetIrClientSitesLocalListByTypeId(int typeId);
        public Task<ClientSitesLocal> GetIrClientSitesLocalListByName(string sitename);

        public Task ClearIrFeedbackTemplateLocalList();
        public Task RefreshIrFeedbackTemplateLocalList(List<IrFeedbackTemplateViewModelLocal> irFeedbackTemplates);
        public Task<List<IrFeedbackTemplateViewModelLocal>> GetIrFeedbackTemplateLocalList();
        public Task<IrFeedbackTemplateViewModelLocal> GetIrFeedbackTemplateLocalListByTypeId(int templateId);

        public Task ClearIrNotifiedByLocalList();
        public Task RefreshIrNotifiedByLocalList(List<IrNotifiedByLocal> irNotifiedBy);
        public Task<List<IrNotifiedByLocal>> GetIrNotifiedByLocalList();

        public Task ClearIrAreasLocalList();
        public Task RefreshIrAreasLocalList(List<ClientSiteAreaLocal> irAreas);
        public Task<List<ClientSiteAreaLocal>> GetIrAreasLocalList();
        public Task<List<ClientSiteAreaLocal>> GetIrAreasLocalList(int clientSiteId);

        public Task<List<AudioAndMultimediaLocal>> GetMultimediaLocalList(int audioType);
        public Task RefreshAudioAndMultimediaLocalList(List<AudioAndMultimediaLocal> _audioVideoFiles);

        public Task SaveIrReportAttachmentsToLocalCache(irOfflineFilesAttachmentsCache _irOfflineFilesAttachmentsCache);
        public Task<string> DeleteIrOfflineFile(string IrID, string filename);
        public Task SaveIrReportToLocalCache(irOfflineCache _irOfflineCache);
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
            //cacheCount += _db.irOfflineFilesAttachmentsCache.Count();
            cacheCount += _db.irOfflineCache.Count();
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

        public async Task<ClientSiteSmartWandTagsLocal> GetSmartWandTagDetailOfTagAsync(string tagUid)
        {
            using var _db = _dbFactory();
            return await _db.ClientSiteSmartWandTagsLocal.Where(x => x.UId == tagUid).FirstOrDefaultAsync();
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
        public async Task<CustomFieldLogHeadCache> GetCustomFieldLogCacheListByKeyValue(string key, string keyvalue)
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

        public async Task ClearRCLinkedDuressClientSitesList()
        {
            using var _db = _dbFactory();
            var r = await _db.RCLinkedDuressClientSitesCache.ToListAsync();
            if (r != null && r.Any())
                _db.RCLinkedDuressClientSitesCache.RemoveRange(r);
            await _db.SaveChangesAsync();
        }
        public async Task RefreshRCLinkedDuressClientSitesList(List<RCLinkedDuressClientSitesCache> rcLinkedDuressClientSites)
        {
            using var _db = _dbFactory();
            var r = await _db.RCLinkedDuressClientSitesCache.ToListAsync();
            if (r != null && r.Any())
                _db.RCLinkedDuressClientSitesCache.RemoveRange(r);
            await _db.AddRangeAsync(rcLinkedDuressClientSites);
            await _db.SaveChangesAsync();
        }

        public async Task<List<RCLinkedDuressClientSitesCache>> GetRCLinkedDuressClientSitesListBySiteId(int siteId)
        {
            using var _db = _dbFactory();

            var result = await _db.RCLinkedDuressClientSitesCache
                .AsNoTracking()
                .Where(x =>
                    _db.RCLinkedDuressClientSitesCache
                        .Where(s => s.ClientSiteId == siteId)
                        .Select(s => s.RCLinkedId)
                        .Contains(x.RCLinkedId))
                .ToListAsync();

            return result;
        }

        public async Task ClearIrClientSitesTypesLocalList()
        {
            using var _db = _dbFactory();
            var r = _db.ClientSiteTypeLocal.ToList();
            if (r != null && r.Any())
                _db.ClientSiteTypeLocal.RemoveRange(r);
            await _db.SaveChangesAsync();
        }
        public async Task RefreshIrClientSitesTypesLocalList(List<ClientSiteTypeLocal> irClientTypes)
        {
            using var _db = _dbFactory();
            var r = _db.ClientSiteTypeLocal.ToList();
            if (r != null && r.Any())
                _db.ClientSiteTypeLocal.RemoveRange(r);
            _db.ClientSiteTypeLocal.AddRange(irClientTypes);
            await _db.SaveChangesAsync();
        }
        public async Task<List<ClientSiteTypeLocal>> GetIrClientSitesTypesLocalList()
        {
            using var _db = _dbFactory();
            var r = await _db.ClientSiteTypeLocal.AsNoTracking().ToListAsync();
            return r;
        }

        public async Task ClearIrClientSitesLocalList()
        {
            using var _db = _dbFactory();
            var r = _db.ClientSitesLocal.ToList();
            if (r != null && r.Any())
                _db.ClientSitesLocal.RemoveRange(r);
            await _db.SaveChangesAsync();
        }
        public async Task RefreshIrClientSitesLocalList(List<ClientSitesLocal> irClientSites)
        {
            using var _db = _dbFactory();
            var r = _db.ClientSitesLocal.ToList();
            if (r != null && r.Any())
                _db.ClientSitesLocal.RemoveRange(r);
            _db.ClientSitesLocal.AddRange(irClientSites);
            await _db.SaveChangesAsync();
            return;
        }
        public async Task<List<ClientSitesLocal>> GetIrClientSitesLocalList()
        {
            using var _db = _dbFactory();
            var r = await _db.ClientSitesLocal.AsNoTracking().ToListAsync();
            return r;
        }
        public async Task<List<ClientSitesLocal>> GetIrClientSitesLocalListByTypeId(int typeId)
        {
            using var _db = _dbFactory();
            var r = await _db.ClientSitesLocal.Where(x => x.TypeId == typeId).AsNoTracking().ToListAsync();
            return r;
        }

        public async Task<ClientSitesLocal> GetIrClientSitesLocalListByName(string sitename)
        {
            using var _db = _dbFactory();
            var r = await _db.ClientSitesLocal.Where(x => x.Name == sitename).AsNoTracking().FirstOrDefaultAsync();
            return r;
        }

        public async Task ClearIrFeedbackTemplateLocalList()
        {
            //using var _db = _dbFactory();
            //var r = _db.IrFeedbackTemplateViewModelLocal.ToList();
            //if (r != null && r.Any())
            //    _db.IrFeedbackTemplateViewModelLocal.RemoveRange(r);
            //await _db.SaveChangesAsync();

            await TruncateSqliteAsync(nameof(IrFeedbackTemplateViewModelLocal));
        }
        public async Task RefreshIrFeedbackTemplateLocalList(List<IrFeedbackTemplateViewModelLocal> irFeedbackTemplates)
        {
            using var _db = _dbFactory();
            var r = _db.IrFeedbackTemplateViewModelLocal.ToList();
            if (r != null && r.Any())
                _db.IrFeedbackTemplateViewModelLocal.RemoveRange(r);
            _db.IrFeedbackTemplateViewModelLocal.AddRange(irFeedbackTemplates);
            await _db.SaveChangesAsync();
        }
        public async Task<List<IrFeedbackTemplateViewModelLocal>> GetIrFeedbackTemplateLocalList()
        {
            using var _db = _dbFactory();
            return await _db.IrFeedbackTemplateViewModelLocal.AsNoTracking().ToListAsync();
        }
        public async Task<IrFeedbackTemplateViewModelLocal> GetIrFeedbackTemplateLocalListByTypeId(int templateId)
        {
            using var _db = _dbFactory();
            return await _db.IrFeedbackTemplateViewModelLocal.AsNoTracking().FirstOrDefaultAsync(x => x.TemplateId == templateId);
        }

        public async Task ClearIrNotifiedByLocalList()
        {
            //using var _db = _dbFactory();
            //var r = _db.IrNotifiedByLocal.ToList();
            //if (r != null && r.Any())
            //    _db.IrNotifiedByLocal.RemoveRange(r);
            //await _db.SaveChangesAsync();

            await TruncateSqliteAsync(nameof(IrNotifiedByLocal));
        }
        public async Task RefreshIrNotifiedByLocalList(List<IrNotifiedByLocal> irNotifiedBy)
        {
            using var _db = _dbFactory();
            var r = _db.IrNotifiedByLocal.ToList();
            if (r != null && r.Any())
                _db.IrNotifiedByLocal.RemoveRange(r);
            _db.IrNotifiedByLocal.AddRange(irNotifiedBy);
            await _db.SaveChangesAsync();
        }
        public async Task<List<IrNotifiedByLocal>> GetIrNotifiedByLocalList()
        {
            using var _db = _dbFactory();
            return await _db.IrNotifiedByLocal.AsNoTracking().ToListAsync();
        }

        public async Task ClearIrAreasLocalList()
        {
            //using var _db = _dbFactory();
            //var r = _db.ClientSiteAreaLocal.ToList();
            //if (r != null && r.Any())
            //    _db.ClientSiteAreaLocal.RemoveRange(r);
            //await _db.SaveChangesAsync();

            await TruncateSqliteAsync(nameof(ClientSiteAreaLocal));
        }
        public async Task RefreshIrAreasLocalList(List<ClientSiteAreaLocal> irAreas)
        {
            using var _db = _dbFactory();
            var r = _db.ClientSiteAreaLocal.ToList();
            if (r != null && r.Any())
                _db.ClientSiteAreaLocal.RemoveRange(r);
            _db.ClientSiteAreaLocal.AddRange(irAreas);
            await _db.SaveChangesAsync();
        }
        public async Task<List<ClientSiteAreaLocal>> GetIrAreasLocalList()
        {
            using var _db = _dbFactory();
            return await _db.ClientSiteAreaLocal.AsNoTracking().ToListAsync();
        }

        public async Task<List<ClientSiteAreaLocal>> GetIrAreasLocalList(int clientSiteId)
        {
            using var _db = _dbFactory();
            return await _db.ClientSiteAreaLocal.Where(x => x.ClientSiteId == -1 || x.ClientSiteId == clientSiteId)
                .AsNoTracking().ToListAsync();
        }

        public async Task<List<AudioAndMultimediaLocal>> GetMultimediaLocalList(int audioType)
        {
            using var _db = _dbFactory();
            return await _db.AudioAndMultimediaLocal.AsNoTracking().Where(x => x.AudioType == audioType && x.LocalFilePath.Trim() != "").ToListAsync();
        }

        //public async Task RefreshAudioAndMultimediaLocalList(List<AudioAndMultimediaLocal> _audioVideoFiles)
        //{
        //    using var _db = _dbFactory();
        //    var _fileType = _audioVideoFiles.FirstOrDefault().AudioType;
        //    var existingFiles = await _db.AudioAndMultimediaLocal.Where(x => x.AudioType == _fileType).AsNoTracking().ToListAsync();

        //    // Remove files that are no longer present
        //    foreach (var existingFile in existingFiles)
        //    {
        //        if (!_audioVideoFiles.Any(x => x.Id == existingFile.Id))
        //        {
        //            _db.AudioAndMultimediaLocal.Remove(existingFile);
        //        }
        //    }

        //    // Add or update files
        //    foreach (var newFile in _audioVideoFiles)
        //    {
        //        var existingFile = existingFiles.FirstOrDefault(x => x.Id == newFile.Id);
        //        if (existingFile != null)
        //        {
        //            _db.Entry(existingFile).CurrentValues.SetValues(newFile);
        //        }
        //        else
        //        {
        //            await _db.AudioAndMultimediaLocal.AddAsync(newFile);
        //        }
        //    }

        //    await _db.SaveChangesAsync();
        //}

        public async Task RefreshAudioAndMultimediaLocalList(List<AudioAndMultimediaLocal> audioVideoFiles)
        {
            using var db = _dbFactory();

            var fileType = audioVideoFiles.FirstOrDefault()?.AudioType;
            if (fileType == null)
                return;

            var existingFiles = await db.AudioAndMultimediaLocal
                .Where(x => x.AudioType == fileType)
                .ToListAsync();

            // Remove deleted files
            var toRemove = existingFiles
                .Where(e => !audioVideoFiles.Any(n => n.Id == e.Id))
                .ToList();

            db.AudioAndMultimediaLocal.RemoveRange(toRemove);

            // Add or update
            foreach (var newFile in audioVideoFiles)
            {
                var existing = existingFiles.FirstOrDefault(x => x.Id == newFile.Id);

                if (existing != null)
                {
                    db.Entry(existing).CurrentValues.SetValues(newFile);
                }
                else
                {
                    await db.AudioAndMultimediaLocal.AddAsync(newFile);
                }
            }

            await db.SaveChangesAsync();
        }


        public async Task SaveIrReportAttachmentsToLocalCache(irOfflineFilesAttachmentsCache _irOfflineFilesAttachmentsCache)
        {
            using var _db = _dbFactory();
            await _db.irOfflineFilesAttachmentsCache.AddAsync(_irOfflineFilesAttachmentsCache);
            await _db.SaveChangesAsync();
        }

        public async Task<string> DeleteIrOfflineFile(string IrID, string filename)
        {
            string rtnfilename = "";
            using var _db = _dbFactory();
            var existingFile = await _db.irOfflineFilesAttachmentsCache.Where(x => x.IrId == IrID && x.FileNameCache == filename).FirstOrDefaultAsync();
            if (existingFile != null)
            {
                rtnfilename = existingFile.FileNameWithPathCache;
                _db.Remove(existingFile);
                await _db.SaveChangesAsync();
            }
            return rtnfilename;
        }

        public async Task SaveIrReportToLocalCache(irOfflineCache _irOfflineCache)
        {
            using var _db = _dbFactory();
            await _db.irOfflineCache.AddAsync(_irOfflineCache);
            await _db.SaveChangesAsync();
        }

        public async Task TruncateSqliteAsync(string tableName)
        {
            // Clear and reset sequence for SQLite (since it doesn't support TRUNCATE)
            using var _db = _dbFactory();
            await _db.Database.ExecuteSqlRawAsync($"DELETE FROM {tableName};");

            await _db.Database.ExecuteSqlRawAsync($"DELETE FROM sqlite_sequence WHERE name='{tableName}';");
        }

    }
}
