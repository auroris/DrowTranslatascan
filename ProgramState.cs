// Extends the compiler-generated partial Program class to expose DbPath,
// which is set at startup and used by TranslateFunction.
internal partial class Program
{
    public static string DbPath { get; set; } = "";
}
