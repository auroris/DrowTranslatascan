// Extends the compiler-generated partial Program class to expose DbPath,
// which is set at startup and used by TranslateFunction.
internal partial class Program
{
    /// <summary>
    /// Absolute path to the SQLite database file (<c>Data/drow_dictionary.db</c>).
    /// Set once at startup before the host is built.
    /// </summary>
    public static string DbPath { get; set; } = "";
}
