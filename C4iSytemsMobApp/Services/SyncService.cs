//using Android.Net;
using C4iSytemsMobApp.Data;
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

            int _retryCount = 3;
            int _currentRetry = 0;

            try
            {                
                using var db = _dbFactory();
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

        public async Task<int> GetCurrentCacheCountAsync()
        {
            using var db = _dbFactory();
            var unsyncedCount = await db.ClientSiteSmartWandTagsHitLogCache.CountAsync();
            return unsyncedCount;
        }
    }
}
