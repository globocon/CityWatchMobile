using Microsoft.Maui.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Services
{
    public class ConnectivityListener
    {
        private readonly SyncService _syncService;

        public ConnectivityListener(SyncService syncService)
        {
            _syncService = syncService;

            // Subscribe to network changes
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
        }

        private async void Connectivity_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            await SyncOfflineRecords();
        }

        public async Task CheckAndSyncOnStartupAsync()
        {
            await SyncOfflineRecords();
        }


        private async Task SyncOfflineRecords()
        {
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncState.SyncingStatus = "(Syncing offline records in progress..)";
                });

                await _syncService.SyncAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncState.SyncingStatus = " ";
                });

                var count = await _syncService.GetCurrentCacheCountAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncState.SyncedCount = count;
                });
            }
        }

    }
}
