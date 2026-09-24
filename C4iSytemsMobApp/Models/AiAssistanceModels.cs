namespace C4iSytemsMobApp.Models
{
    /* AI Assistance contracts for the Incident Report page. They mirror the types the
       api/GuardSecurityNumber AI endpoints return (CityWatch.Web/Models/AiAssistanceModels.cs).
       The app never talks to an AI provider itself, so no provider detail or key appears here. */

    public class AiLanguageOption
    {
        /// <summary>Sent back to the server, which validates it against its own list.</summary>
        public string Code { get; set; }

        /// <summary>What the guard sees, e.g. "Australian English".</summary>
        public string Name { get; set; }
    }

    public class AiGrammarCheckResult
    {
        /// <summary>The text the server checked (trimmed). Match offsets point into this string.</summary>
        public string OriginalText { get; set; }

        /// <summary>What was detected, e.g. "English (Australian)". Never assumed.</summary>
        public string DetectedLanguage { get; set; }

        public List<AiGrammarMatch> Matches { get; set; } = new();
    }

    public class AiGrammarMatch
    {
        public string Message { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }

        /// <summary>The text the offset/length points at, so the popup can show "was" and "suggested".</summary>
        public string Original { get; set; }

        public List<string> Replacements { get; set; } = new();

        /// <summary>App-side only: whether the guard has this correction ticked. Suggestions start accepted.</summary>
        public bool Applied { get; set; } = true;
    }

    /// <summary>Result of a rewrite or a conversion. The guard decides whether to keep it.</summary>
    public class AiTextResult
    {
        public string OriginalText { get; set; }
        public string ResultText { get; set; }
    }
}
