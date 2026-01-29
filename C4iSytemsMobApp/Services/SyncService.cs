//using Android.Net;
using C4iSytemsMobApp.Data;
using C4iSytemsMobApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Services
{
    public class SyncService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly ISyncApiService _api;


        public SyncService(Func<AppDbContext> dbFactory, ISyncApiService api)
        {
            _dbFactory = dbFactory;
            _api = api;
        }

        public async Task SyncAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            await SyncSmartWandTagsHitLogCache();
            await SyncLogbookCache();
            await SyncLogbookDocumentsCache();
            await SyncPatrolCarLogsCache();
            await SyncCustomFieldLogsCache();


        }

        public async Task<int> GetCurrentCacheCountAsync()
        {
            using var db = _dbFactory();
            var unsyncedCount = await db.ClientSiteSmartWandTagsHitLogCache.CountAsync();
            unsyncedCount += await db.PostActivityRequestLocalCache.CountAsync();
            unsyncedCount += await db.OfflineFilesRecords.CountAsync();
            unsyncedCount += await db.CustomFieldLogRequestHeadCache.CountAsync();
            unsyncedCount += await db.PatrolCarLogRequestCache.CountAsync();
            // refer and modify in ScanDataDbServices.GetCacheRecordsCount()

            return unsyncedCount;
        }

        private async Task SyncSmartWandTagsHitLogCache()
        {
            int _retryCount = 3;
            int _currentRetry = 0;
            using var db = _dbFactory();

            try
            {
                var unsynced = await db.ClientSiteSmartWandTagsHitLogCache.Where(o => !o.IsSynced).ToListAsync();
                if (unsynced.Count == 0) return;

                while (_currentRetry <= _retryCount)
                {
                    _currentRetry += 1;
                    try
                    {
                        var ok = await _api.PushSmartWandTagsHitLogCacheAsync(unsynced);
                        if (ok == null)
                        {
                            if (_currentRetry < _retryCount)
                            {
                                await Task.Delay(2000);
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }


                        foreach (var o in unsynced)
                        {
                            var r = ok.Where(x => x.Id == o.Id && x.IsSynced && x.UniqueRecordId == o.UniqueRecordId).FirstOrDefault();
                            if (r != null)
                            {
                                db.ClientSiteSmartWandTagsHitLogCache.Remove(o);
                            }
                        }
                        await db.SaveChangesAsync();
                        _currentRetry = _retryCount + 1; // exit retry loop 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // throw;
            }            
        }

        private async Task SyncLogbookCache()
        {
            int _retryCount = 3;
            int _currentRetry = 0;
            using var db = _dbFactory();

            try
            {
                var unsynced = await db.PostActivityRequestLocalCache.Where(o => !o.IsSynced).ToListAsync();
                if (unsynced.Count == 0) return;

                while (_currentRetry <= _retryCount)
                {
                    _currentRetry += 1;
                    try
                    {
                        var ok = await _api.PushActivityLogCacheAsync(unsynced);
                        if (ok == null)
                        {
                            if (_currentRetry < _retryCount)
                            {
                                await Task.Delay(2000);
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }


                        foreach (var o in unsynced)
                        {
                            var r = ok.Where(x => x.Id == o.Id && x.IsSynced && x.UniqueRecordId == o.UniqueRecordId).FirstOrDefault();
                            if (r != null)
                            {
                                db.PostActivityRequestLocalCache.Remove(o);
                            }
                        }
                        await db.SaveChangesAsync();
                        _currentRetry = _retryCount + 1; // exit retry loop 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // throw;
            }
        }

        private async Task SyncLogbookDocumentsCache()
        {
            int _retryCount = 3;
            int _currentRetry = 0;
            using var db = _dbFactory();

            try
            {
                var unsynced = await db.OfflineFilesRecords.Where(o => !o.IsSynced && o.RecordLabel == "LBACTIVITYNEW").ToListAsync();
                if (unsynced.Count == 0) return;

                while (_currentRetry <= _retryCount)
                {
                    _currentRetry += 1;
                    try
                    {
                        var ok = await _api.PushActivityLogDocumentsCacheAsync(unsynced);
                        if (ok == null)
                        {
                            if (_currentRetry < _retryCount)
                            {
                                await Task.Delay(2000);
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }


                        foreach (var o in unsynced)
                        {
                            var r = ok.Where(x => x.Id == o.Id && x.IsSynced && x.UniqueRecordId == o.UniqueRecordId).FirstOrDefault();
                            if (r != null)
                            {
                                // delete cache file also
                                if (File.Exists(o.FileNameWithPathCache))
                                    File.Delete(o.FileNameWithPathCache);

                                db.OfflineFilesRecords.Remove(o);
                            }
                        }
                        await db.SaveChangesAsync();
                        _currentRetry = _retryCount + 1; // exit retry loop 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // throw;
            }
        }

        private async Task SyncPatrolCarLogsCache()
        {
            int _retryCount = 3;
            int _currentRetry = 0;
            using var db = _dbFactory();

            try
            {
                var unsynced = await db.PatrolCarLogRequestCache.Where(o => !o.IsSynced).ToListAsync();
                if (unsynced.Count == 0) return;

                while (_currentRetry <= _retryCount)
                {
                    _currentRetry += 1;
                    try
                    {
                        var ok = await _api.PushPatrolCarCacheAsync(unsynced);
                        if (ok == null)
                        {
                            if (_currentRetry < _retryCount)
                            {
                                await Task.Delay(2000);
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }


                        foreach (var o in unsynced)
                        {
                            var r = ok.Where(x => x.Id == o.Id && x.IsSynced && x.UniqueRecordId == o.UniqueRecordId).FirstOrDefault();
                            if (r != null)
                            {
                                db.PatrolCarLogRequestCache.Remove(o);
                            }
                        }
                        await db.SaveChangesAsync();
                        _currentRetry = _retryCount + 1; // exit retry loop 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // throw;
            }
        }

        private async Task SyncCustomFieldLogsCache()
        {
            int _retryCount = 3;
            int _currentRetry = 0;
            using var db = _dbFactory();

            try
            {
                var unsynced = await db.CustomFieldLogRequestHeadCache.Include(i=> i.Details).Where(o => !o.IsSynced).ToListAsync();
                if (unsynced.Count == 0) return;

                //foreach(var r in unsynced)
                //{
                //    foreach(var d in r.Details)
                //    {
                //        d.Head = null;
                //    }
                //}

                var dto = unsynced.Select(h => new CustomFieldLogRequestHeadDto
                {
                    Id = h.Id,
                    SiteId = h.SiteId,
                    EventDateTimeLocal = h.EventDateTimeLocal,
                    EventDateTimeLocalWithOffset = h.EventDateTimeLocalWithOffset,
                    EventDateTimeZone = h.EventDateTimeZone,
                    EventDateTimeZoneShort = h.EventDateTimeZoneShort,
                    EventDateTimeUtcOffsetMinute = h.EventDateTimeUtcOffsetMinute,
                    IsSynced = h.IsSynced,
                    UniqueRecordId = h.UniqueRecordId,
                    DeviceId = h.DeviceId,
                    DeviceName = h.DeviceName,
                    Details = h.Details.Select(d => new CustomFieldLogRequestDetailDto
                    {
                        Id = d.Id,
                        HeadId = d.HeadId,
                        DictKey = d.DictKey,
                        DictValue = d.DictValue
                    }).ToList()
                }).ToList();


                while (_currentRetry <= _retryCount)
                {
                    _currentRetry += 1;
                    try
                    {
                        var ok = await _api.PushCustomFieldLogCacheAsync(dto);
                        if (ok == null)
                        {
                            if (_currentRetry < _retryCount)
                            {
                                await Task.Delay(2000);
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }


                        foreach (var o in unsynced)
                        {
                            var r = ok.Where(x => x.Id == o.Id && x.IsSynced && x.UniqueRecordId == o.UniqueRecordId).FirstOrDefault();
                            if (r != null)
                            {
                                db.CustomFieldLogRequestHeadCache.Remove(o);
                            }
                        }
                        await db.SaveChangesAsync();
                        _currentRetry = _retryCount + 1; // exit retry loop 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // throw;
            }
        }
    }
}
