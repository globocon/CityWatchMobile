using Plugin.Maui.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Services
{

    public class AudioPlaybackService
    {
        private static readonly Lazy<AudioPlaybackService> _instance = new(() => new AudioPlaybackService());
        public static AudioPlaybackService Instance => _instance.Value;

        private readonly IAudioManager _audioManager = new AudioManager();
        private IAudioPlayer _player;
        private List<string> _queue = new List<string>();
        private int _currentIndex = 0;
        private bool _loop = false;
        private CancellationTokenSource _cts;

        public bool IsPlaying { get; private set; } = false;

        public event EventHandler<PlaybackState> PlaybackStateChanged;

        /// <summary>
        /// Raised as each queued file actually begins, carrying that file's path.
        ///
        /// PlaybackStateChanged cannot answer "which file just started": it carries no file
        /// identity, it fires once for the session before any file begins, and subscribers can
        /// be attached more than once. Anything that needs per-file timing - the logbook's
        /// "Played file ..." entry - listens here instead.
        /// </summary>
        public event EventHandler<string> FileStarted;

        public enum PlaybackState
        {
            Stopped,
            Playing
        }

        private void RaisePlaybackStateChanged(PlaybackState state)
        {
            PlaybackStateChanged?.Invoke(this, state);
        }

        /* A subscriber that throws must not kill playback, so this swallows. */
        private void RaiseFileStarted(string filePath)
        {
            try
            {
                FileStarted?.Invoke(this, filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileStarted subscriber failed: {ex.Message}");
            }
        }

        private AudioPlaybackService() { }

        /// <summary>
        /// Enqueue files to play and optionally loop.
        /// </summary>
        public void EnqueueFiles(List<string> files, bool loop = false)
        {
            _queue = files ?? new List<string>();
            _currentIndex = 0;
            _loop = loop;
        }

        /// <summary>
        /// Starts playback with optional silence delay in minutes.
        /// </summary>
        public async Task StartPlayback(int silenceMinutes = 0)
        {
            if (_queue == null || _queue.Count == 0)
                return;

            Stop(); // Stop any current playback
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsPlaying = true;
            RaisePlaybackStateChanged(PlaybackState.Playing);

            try
            {
                do
                {
                    if (_currentIndex >= _queue.Count)
                    {
                        if (_loop)
                        {
                            _currentIndex = 0;
                        }
                        else
                        {
                            break;
                        }
                    }

                    var fileUrl = _queue[_currentIndex];

                    //await PlayFile(fileUrl, token);
                    await PlayFileFromlocal(fileUrl, token);

                    // Wait optional silence
                    if (silenceMinutes > 0 && !token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(silenceMinutes), token);
                    }

                    _currentIndex++;

                } while (_loop && !token.IsCancellationRequested);

            }
            catch (TaskCanceledException)
            {
                // Normal stop
            }
            finally
            {
                IsPlaying = false;
                RaisePlaybackStateChanged(PlaybackState.Stopped);
            }
        }

        private async Task PlayFile(string url, CancellationToken token)
        {
            _player?.Stop();
            _player?.Dispose();
            _player = null;

            using var client = new HttpClient();
            using var remoteStream = await client.GetStreamAsync(url, token);
            using var memoryStream = new MemoryStream();
            await remoteStream.CopyToAsync(memoryStream, token);
            memoryStream.Position = 0;

            _player = _audioManager.CreatePlayer(memoryStream);

            var tcs = new TaskCompletionSource<bool>();

            _player.PlaybackEnded += (s, e) => tcs.TrySetResult(true);
            _player.Play();

            RaisePlaybackStateChanged(PlaybackState.Playing);
            RaiseFileStarted(url);

            await tcs.Task; // Wait for playback to finish
        }

        private async Task PlayFileFromlocal(string filePath, CancellationToken token)
        {
            _player?.Stop();
            _player?.Dispose();
            _player = null;

            
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Audio file not found", filePath);

            // Open file stream (do NOT wrap in using – player needs it alive)
            var fileStream = File.OpenRead(filePath);

            _player = _audioManager.CreatePlayer(fileStream);

            var tcs = new TaskCompletionSource<bool>();

            _player.PlaybackEnded += (s, e) =>
            {
                fileStream.Dispose(); // Clean up after playback
                tcs.TrySetResult(true);
            };

            _player.Play();
            RaisePlaybackStateChanged(PlaybackState.Playing);
            RaiseFileStarted(filePath);

            await tcs.Task; // Wait until playback completes
        }


        /// <summary>
        /// Stops playback immediately.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();

            if (_player != null)
            {
                try { _player.Stop(); } catch { }
                try { _player.Dispose(); } catch { }
                _player = null;
            }

            IsPlaying = false;
            RaisePlaybackStateChanged(PlaybackState.Stopped);
        }
    }

    


    }
