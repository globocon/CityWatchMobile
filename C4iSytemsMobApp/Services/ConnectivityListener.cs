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
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                // Device came online → Push pending sync
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncState.SyncingStatus = "(Syncing offline records in progress..)";
                });
                await _syncService.SyncAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncState.SyncingStatus = " ";
                });

                var _ChaceCount = await _syncService.GetCurrentCacheCountAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncState.SyncedCount = _ChaceCount;
                });
            }
        }
    }
}
