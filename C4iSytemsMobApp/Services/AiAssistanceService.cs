using C4iSytemsMobApp.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace C4iSytemsMobApp.Services
{
    /// <summary>Raised when AI Assistance cannot complete; the message is safe to show the guard.</summary>
    public class AiAssistanceUnavailableException : Exception
    {
        public AiAssistanceUnavailableException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// AI Assistance for the Incident Report description: grammar check, professional rewrite and
    /// language converter - the same three operations as /Incident/Register on the web.
    ///
    /// Every operation goes through the CityWatch API, which calls the AI providers server-side.
    /// The app sends the guard's text and receives the result; no provider key or setting is ever
    /// on the device. Validation (empty text, maximum length, supported languages) is enforced by
    /// the server, and its guard-facing message is shown as-is.
    ///
    /// One instance belongs to one open WebIncidentReport page. <see cref="CancelPending"/> stops
    /// any request still running when the page closes.
    /// </summary>
    public class AiAssistanceService
    {
        public const string EmptyTextMessage = "Text field requires content for AI to scan";
        public const string UnavailableMessage = "AI assistance is temporarily unavailable. Please try again.";
        public const string GrammarUnavailableMessage = "Grammar checking is temporarily unavailable. Please try again.";
        public const string TimeoutMessage = "The AI request took too long. Please try again.";
        public const string OfflineMessage = "AI cannot work in offline mode.";

        /* The server's own provider timeouts are 30s (grammar) and 120s (AI); these leave headroom
           for the round trip so the server's clearer message wins over a client timeout. */
        private static readonly TimeSpan GrammarTimeout = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan AiTimeout = TimeSpan.FromSeconds(150);

        private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private CancellationTokenSource _pageCts = new();
        private List<AiLanguageOption> _languages;

        private static int CurrentGuardId =>
            int.TryParse(Preferences.Get("GuardId", "0"), out var guardId) ? guardId : 0;

        /// <summary>Target languages for the Language Converter, loaded once per page from the server.</summary>
        public async Task<List<AiLanguageOption>> GetLanguagesAsync()
        {
            if (_languages != null)
                return _languages;

            var result = await SendAsync<List<AiLanguageOption>>(
                HttpMethod.Get,
                $"GuardSecurityNumber/GetAiLanguages?guardId={CurrentGuardId}",
                null, GrammarTimeout, "The language list could not be loaded. Please try again.");

            _languages = result ?? new List<AiLanguageOption>();
            return _languages;
        }

        public Task<AiGrammarCheckResult> CheckGrammarAsync(string text) =>
            SendAsync<AiGrammarCheckResult>(HttpMethod.Post, "GuardSecurityNumber/AiGrammarCheck",
                new { guardId = CurrentGuardId, text }, GrammarTimeout, GrammarUnavailableMessage);

        public async Task<string> ImproveIncidentReportAsync(string text)
        {
            var result = await SendAsync<AiTextResult>(HttpMethod.Post, "GuardSecurityNumber/AiImproveIncident",
                new { guardId = CurrentGuardId, text }, AiTimeout, UnavailableMessage);
            return RequireText(result);
        }

        public async Task<string> TranslateAsync(string text, string targetLanguageCode)
        {
            var result = await SendAsync<AiTextResult>(HttpMethod.Post, "GuardSecurityNumber/AiTranslate",
                new { guardId = CurrentGuardId, text, targetLanguage = targetLanguageCode }, AiTimeout, UnavailableMessage);
            return RequireText(result);
        }

        /// <summary>Stops any request still running. Called when the page closes.</summary>
        public void CancelPending()
        {
            try { _pageCts.Cancel(); } catch (ObjectDisposedException) { }
            _pageCts.Dispose();
            _pageCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Rebuilds the text from the ticked corrections only. Walks the matches from the end
        /// backwards so earlier offsets stay valid, and every character between matches - line
        /// breaks, blank lines, bullets - survives untouched. Overlapping matches are skipped rather
        /// than corrupting the text. The same algorithm as ai-assistance.js on the web.
        /// </summary>
        public static string BuildCorrectedText(string original, IEnumerable<AiGrammarMatch> matches)
        {
            var text = original ?? string.Empty;
            var nextStart = text.Length;

            foreach (var match in matches
                         .Where(z => z.Applied && z.Replacements.Count > 0)
                         .OrderByDescending(z => z.Offset))
            {
                // Out of range (never expected - the server already filters) or overlapping the one already applied.
                if (match.Offset < 0 || match.Offset + match.Length > nextStart) continue;

                text = text.Substring(0, match.Offset)
                     + match.Replacements[0]
                     + text.Substring(match.Offset + match.Length);

                nextStart = match.Offset;
            }

            return text;
        }

        // -------------------------------------------------------------------------------------

        private static string RequireText(AiTextResult result) =>
            string.IsNullOrWhiteSpace(result?.ResultText)
                ? throw new AiAssistanceUnavailableException(UnavailableMessage)
                : result.ResultText;

        /// <summary>
        /// One API call. Returns the response data, or throws <see cref="AiAssistanceUnavailableException"/>
        /// carrying the server's message (or <paramref name="failureMessage"/> when there is none).
        /// </summary>
        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, TimeSpan timeout, string failureMessage)
        {
            if (CurrentGuardId <= 0)
                throw new AiAssistanceUnavailableException("Guard ID not found. Please log in again.");

            if (!App.IsOnline)   // the app-wide offline flag the Incident Report page uses
                throw new AiAssistanceUnavailableException(OfflineMessage);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_pageCts.Token);
            cts.CancelAfter(timeout);

            try
            {
                using var request = new HttpRequestMessage(method, $"{AppConfig.ApiBaseUrl}{path}");
                if (body != null)
                    request.Content = JsonContent.Create(body);

                using var response = await Http.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                    throw new AiAssistanceUnavailableException(failureMessage);

                var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, cts.Token);
                if (result == null || !result.isSuccess || result.data == null)
                    throw new AiAssistanceUnavailableException(
                        string.IsNullOrWhiteSpace(result?.message) ? failureMessage : result.message);

                return result.data;
            }
            catch (AiAssistanceUnavailableException) { throw; }
            catch (OperationCanceledException ex) when (!_pageCts.IsCancellationRequested)
            {
                throw new AiAssistanceUnavailableException(TimeoutMessage, ex);
            }
            catch (Exception ex)
            {
                throw new AiAssistanceUnavailableException(failureMessage, ex);
            }
        }
    }
}
