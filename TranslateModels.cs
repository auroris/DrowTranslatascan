namespace DrowTranslatascan
{
    public class TranslateRequest
    {
        public string? Text { get; set; }
        public string? Lang { get; set; }
    }

    public class TranslateResponse
    {
        public string Translation { get; set; } = "";
        public string OriginalText { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = "";
    }
}
