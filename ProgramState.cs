using DrowTranslatascan;

// Extends the compiler-generated partial Program class to expose DbPath and Config,
// which are set at startup and used by TranslateFunction and HomeFunction.
internal partial class Program
{
    /// <summary>
    /// Absolute path to the SQLite database file.
    /// Set once at startup before the host is built.
    /// </summary>
    public static string DbPath { get; set; } = "";

    /// <summary>
    /// Language configuration loaded from <c>Data/language.json</c>.
    /// Controls language names, pluralization rules, algorithmic conversion,
    /// contractions, and UI theming.
    /// </summary>
    public static LanguageConfig Config { get; set; } = new();
}
