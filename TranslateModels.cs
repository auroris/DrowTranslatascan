namespace DrowTranslatascan
{
    /// <summary>Request body for <c>POST /api/TranslateJson</c>.</summary>
    public class TranslateRequest
    {
        /// <summary>The text to translate.</summary>
        public string? Text { get; set; }

        /// <summary>Target language: <c>"Drow"</c> (English → Drow) or <c>"Common"</c> (Drow → English).</summary>
        public string? Lang { get; set; }
    }

    /// <summary>Successful response from <c>POST /api/TranslateJson</c>.</summary>
    public class TranslateResponse
    {
        /// <summary>The translated text.</summary>
        public string Translation { get; set; } = "";

        /// <summary>The original input text, echoed back.</summary>
        public string OriginalText { get; set; } = "";

        /// <summary>The target language that was requested (<c>"Drow"</c> or <c>"Common"</c>).</summary>
        public string TargetLanguage { get; set; } = "";
    }

    /// <summary>Error response returned when a request cannot be fulfilled.</summary>
    public class ErrorResponse
    {
        /// <summary>Human-readable description of the error.</summary>
        public string Error { get; set; } = "";
    }
}
