using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Services
{
    public class TagStatusService
    {
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
        private readonly List<Action<int?>> _subscribers = new();
        private CancellationTokenSource? _cts;
        private Task? _pollingTask;

        private int? _clientSiteId;

        // Singleton
        private static readonly Lazy<TagStatusService> _instance = new(() => new TagStatusService());
        public static TagStatusService Instance => _instance.Value;

        private TagStatusService() { }

        public void StartPolling(int? clientSiteId)
        {
            _clientSiteId = clientSiteId;

            if (_pollingTask != null && !_pollingTask.IsCompleted)
                return;

            _cts = new CancellationTokenSource();
            _pollingTask = PollTagStatusAsync(_cts.Token);
        }

        public void StopPolling()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _pollingTask = null;
        }

        public void Subscribe(Action<int?> callback)
        {
            if (!_subscribers.Contains(callback))
                _subscribers.Add(callback);
        }

        public void Unsubscribe(Action<int?> callback)
        {
            _subscribers.Remove(callback);
        }

        private async Task PollTagStatusAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(_pollInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (_clientSiteId != null)
                    {
                        foreach (var callback in _subscribers.ToList())
                        {
                            try
                            {
                                callback(_clientSiteId);
                            }
                            catch { /* Ignore subscriber errors */ }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }


}
