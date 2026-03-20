using Microsoft.Data.Sqlite;
using DrowTranslatascan;

// Load language config so the AlgorithmicConverter tables are available.
string configPath = Path.Combine("..", "Data", "language.json");
Program.Config = LanguageConfig.Load(configPath);

string dbPath = "../Data/drow_dictionary.db";

int passed = 0;
int failed = 0;

void Assert(string label, string expected, string actual)
{
    if (actual == expected)
    {
        Console.WriteLine($"  PASS  {label}");
        passed++;
    }
    else
    {
        Console.WriteLine($"  FAIL  {label}");
        Console.WriteLine($"        expected: {expected}");
        Console.WriteLine($"        actual:   {actual}");
        failed++;
    }
}

void AssertNotEmpty(string label, string actual)
{
    if (!string.IsNullOrEmpty(actual))
    {
        Console.WriteLine($"  PASS  {label} => \"{actual}\"");
        passed++;
    }
    else
    {
        Console.WriteLine($"  FAIL  {label} (was empty)");
        failed++;
    }
}

// ── AlgorithmicConverter: cipher mappings ──────────────────────────────────
Console.WriteLine("\n=== AlgorithmicConverter: cipher mappings ===");

// Digraph rules (short words: no apostrophe inserted)
Assert("the  → zae   (th→z, e→ae)",     "zae",    AlgorithmicConverter.ConvertToDrow("the"));
Assert("she  → ssae  (sh→ss, e→ae)",    "ssae",   AlgorithmicConverter.ConvertToDrow("she"));
Assert("chat → xat   (ch→x)",           "xat",    AlgorithmicConverter.ConvertToDrow("chat"));
Assert("whip → khip  (wh→kh)",          "khip",   AlgorithmicConverter.ConvertToDrow("whip"));
Assert("sing → sink  (ng→nk)",          "sink",   AlgorithmicConverter.ConvertToDrow("sing"));
Assert("phone→ faunae → faun'ae (ph→f, o→au, e→ae)", "faun'ae", AlgorithmicConverter.ConvertToDrow("phone"));

// Vowel substitutions
Assert("be   → bae   (e→ae)",           "bae",    AlgorithmicConverter.ConvertToDrow("be"));
Assert("go   → gau   (o→au)",           "gau",    AlgorithmicConverter.ConvertToDrow("go"));
Assert("yes  → iiaes (y→ii, e→ae)",     "iiaes",  AlgorithmicConverter.ConvertToDrow("yes"));

// Consonant substitutions
Assert("we   → jae   (w→j, e→ae)",      "jae",    AlgorithmicConverter.ConvertToDrow("we"));
Assert("jar  → jhar  (j→jh)",           "jhar",   AlgorithmicConverter.ConvertToDrow("jar"));
Assert("cat  → kat   (c→k)",            "kat",    AlgorithmicConverter.ConvertToDrow("cat"));
Assert("zap  → zzap  (z→zz)",           "zzap",   AlgorithmicConverter.ConvertToDrow("zap"));

// Apostrophe: inserted before second vowel group in words ≥ 5 encoded chars
Assert("thunder → zund'aer",            "zund'aer",  AlgorithmicConverter.ConvertToDrow("thunder"));
Assert("dragon  → drag'aun",            "drag'aun",  AlgorithmicConverter.ConvertToDrow("dragon"));
Assert("phone   → faun'ae",             "faun'ae",   AlgorithmicConverter.ConvertToDrow("phone"));
Assert("echo    → aex'au",              "aex'au",    AlgorithmicConverter.ConvertToDrow("echo"));

// No apostrophe when there is no second vowel group (all-consonant tail)
Assert("strength → straenkz (no apostrophe)", "straenkz", AlgorithmicConverter.ConvertToDrow("strength"));

// ── AlgorithmicConverter: round-trips ──────────────────────────────────────
Console.WriteLine("\n=== AlgorithmicConverter: round-trips ===");

// Perfect round-trips: all cipher rules are bijective for these words
foreach (var word in new[] { "thunder", "dragon", "echo", "shadow", "champion" })
{
    string drow    = AlgorithmicConverter.ConvertToDrow(word);
    string decoded = AlgorithmicConverter.ConvertToCommon(drow);
    if (decoded == word)
    {
        Console.WriteLine($"  PASS  round-trip \"{word}\" → \"{drow}\" → \"{decoded}\"");
        passed++;
    }
    else
    {
        Console.WriteLine($"  FAIL  round-trip \"{word}\" → \"{drow}\" → \"{decoded}\" (expected \"{word}\")");
        failed++;
    }
}

// Known lossy mappings (intentional): ph→f and c→k both lose their origin letter
Assert("ph→f is lossy: phone decodes as fone",        "fone",    AlgorithmicConverter.ConvertToCommon(AlgorithmicConverter.ConvertToDrow("phone")));
Assert("c/k merge: welcome decodes as welkome",        "welkome", AlgorithmicConverter.ConvertToCommon(AlgorithmicConverter.ConvertToDrow("welcome")));

// ── AlgorithmicConverter: general properties ──────────────────────────────
Console.WriteLine("\n=== AlgorithmicConverter: general properties ===");

// Determinism
string r1 = AlgorithmicConverter.ConvertToDrow("dragon");
string r2 = AlgorithmicConverter.ConvertToDrow("dragon");
Assert("determinism: dragon == dragon", r1, r2);

// Different words produce different output
string rA = AlgorithmicConverter.ConvertToDrow("sword");
string rB = AlgorithmicConverter.ConvertToDrow("shield");
if (rA != rB)
{
    Console.WriteLine($"  PASS  different words → different output (\"{rA}\" vs \"{rB}\")");
    passed++;
}
else
{
    Console.WriteLine($"  FAIL  different words produced identical output: \"{rA}\"");
    failed++;
}

// Capitalization (use "computer" — not in dictionary)
string lower = AlgorithmicConverter.ConvertToDrow("computer");
char lowerFirst = lower.First(char.IsLetter);
if (char.IsLower(lowerFirst))
{
    Console.WriteLine($"  PASS  lowercase input → lowercase output (\"{lower}\")");
    passed++;
}
else
{
    Console.WriteLine($"  FAIL  lowercase input produced capitalised output (\"{lower}\")");
    failed++;
}

string title = AlgorithmicConverter.ConvertToDrow("Computer");
char titleFirst = title.First(char.IsLetter);
if (char.IsUpper(titleFirst))
{
    Console.WriteLine($"  PASS  Title Case input → Title Case output (\"{title}\")");
    passed++;
}
else
{
    Console.WriteLine($"  FAIL  Title Case input produced lowercase output (\"{title}\")");
    failed++;
}

string allcaps = AlgorithmicConverter.ConvertToDrow("COMPUTER");
bool isAllCaps = allcaps.All(c => !char.IsLetter(c) || char.IsUpper(c));
if (isAllCaps)
{
    Console.WriteLine($"  PASS  ALL CAPS input → ALL CAPS output (\"{allcaps}\")");
    passed++;
}
else
{
    Console.WriteLine($"  FAIL  ALL CAPS input did not produce ALL CAPS output (\"{allcaps}\")");
    failed++;
}

// No double apostrophes
foreach (var word in new[] { "dragon", "shadow", "strength", "welcome", "friend" })
{
    string result = AlgorithmicConverter.ConvertToDrow(word);
    if (!result.Contains("''"))
    {
        Console.WriteLine($"  PASS  no double apostrophes in \"{word}\" → \"{result}\"");
        passed++;
    }
    else
    {
        Console.WriteLine($"  FAIL  double apostrophe in \"{word}\" → \"{result}\"");
        failed++;
    }
}

// Short words don't crash
AssertNotEmpty("single char \"a\"",  AlgorithmicConverter.ConvertToDrow("a"));
AssertNotEmpty("two chars   \"be\"", AlgorithmicConverter.ConvertToDrow("be"));

// ── Database lookups ────────────────────────────────────────────────────────
Console.WriteLine("\n=== Database lookups ===");

string Lookup(string word, string fromCol, string toCol, SqliteConnection conn)
{
    using var cmd = new SqliteCommand($"SELECT {toCol} FROM drow_dictionary WHERE {fromCol} = @w", conn);
    cmd.Parameters.AddWithValue("@w", word.ToLower());
    var val = cmd.ExecuteScalar();
    return val as string ?? "";
}

using (var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
{
    conn.Open();

    string drow = Lookup("hello", "Common", "Drow", conn);
    AssertNotEmpty("hello → Drow", drow);

    string common = Lookup(drow, "Drow", "Common", conn);
    Assert($"round-trip hello→{drow}→Common", "hello", common);

    foreach (var w in new[] { "sword", "dark", "magic", "elf", "death" })
    {
        string d = Lookup(w, "Common", "Drow", conn);
        AssertNotEmpty($"{w} → Drow", d);
    }

    string missing = Lookup("zzzznotaword", "Common", "Drow", conn);
    Assert("unknown word returns empty", "", missing);
}

// ── Capitalization fix: single uppercase letter ─────────────────────────────
Console.WriteLine("\n=== Capitalization: single uppercase letter ===");

// A single uppercase letter like "E" satisfies the old isAllCap check (every letter
// is uppercase), producing ALL CAPS output.  After the fix, at least two letters are
// required to be treated as ALL CAPS.
Assert("E  → Ae  (Title Case, not AE)",  "Ae",  AlgorithmicConverter.ConvertToDrow("E"));
Assert("O  → Au  (Title Case, not AU)",  "Au",  AlgorithmicConverter.ConvertToDrow("O"));

// Multi-letter ALL CAPS words must still produce ALL CAPS output.
Assert("BE → BAE (ALL CAPS preserved)",  "BAE", AlgorithmicConverter.ConvertToDrow("BE"));
Assert("GO → GAU (ALL CAPS preserved)",  "GAU", AlgorithmicConverter.ConvertToDrow("GO"));

// ── AlgorithmicConverter: explicit encoding assertions ───────────────────────
Console.WriteLine("\n=== AlgorithmicConverter: explicit encoding assertions ===");

// "greet" is not in the dictionary; the algorithmic output appears in the bug report.
// Both 'e's each become "ae", forming a double-ae run with no internal apostrophe.
Assert("greet → graeaet (no apostrophe)",  "graeaet",   AlgorithmicConverter.ConvertToDrow("greet"));
Assert("shadow → ssad'auj",                "ssad'auj",  AlgorithmicConverter.ConvertToDrow("shadow"));
Assert("champion → xamp'iaun",             "xamp'iaun", AlgorithmicConverter.ConvertToDrow("champion"));
// "north": o→au, th→z → "naurz" (5 chars, no second vowel group after consonant tail)
Assert("north → naurz (no apostrophe)",    "naurz",     AlgorithmicConverter.ConvertToDrow("north"));

// ConvertToCommon (decode) symmetry
Assert("decode zae → the",  "the", AlgorithmicConverter.ConvertToCommon("zae"));
Assert("decode ssae → she", "she", AlgorithmicConverter.ConvertToCommon("ssae"));
Assert("decode gau → go",   "go",  AlgorithmicConverter.ConvertToCommon("gau"));
Assert("decode sink → sing (nk→ng)", "sing", AlgorithmicConverter.ConvertToCommon("sink"));

// ── Database: Drow → Common lookups ─────────────────────────────────────────
Console.WriteLine("\n=== Database: Drow → Common lookups ===");

using (var conn2 = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
{
    conn2.Open();

    // Core pronouns from the bug report
    Assert("usstan → i",  "i",   Lookup("usstan", "Drow", "Common", conn2));
    Assert("dos → you",   "you", Lookup("dos",    "Drow", "Common", conn2));

    // Common function words
    Assert("nau → no",    "no",    Lookup("nau",    "Drow", "Common", conn2));
    Assert("xas → yes",   "yes",   Lookup("xas",    "Drow", "Common", conn2));
    Assert("lil → the",   "the",   Lookup("lil",    "Drow", "Common", conn2));
    Assert("rivvil → human", "human", Lookup("rivvil", "Drow", "Common", conn2));

    // Unknown Drow word returns empty
    Assert("unknown Drow word returns empty", "", Lookup("zzzznotadrowword", "Drow", "Common", conn2));
}

// ── Database: Common → Drow explicit values ──────────────────────────────────
Console.WriteLine("\n=== Database: Common → Drow explicit values ===");

using (var conn3 = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
{
    conn3.Open();

    Assert("i → usstan",  "usstan", Lookup("i",   "Common", "Drow", conn3));
    Assert("you → dos",   "dos",    Lookup("you", "Common", "Drow", conn3));
    Assert("no → nau",    "nau",    Lookup("no",  "Common", "Drow", conn3));
    // "yes" and "the" each have multiple Drow translations in the dictionary;
    // just verify a result is returned rather than asserting which comes first.
    AssertNotEmpty("yes → (a Drow word)", Lookup("yes", "Common", "Drow", conn3));
    AssertNotEmpty("the → (a Drow word)", Lookup("the", "Common", "Drow", conn3));
}

// ── Summary ─────────────────────────────────────────────────────────────────
Console.WriteLine($"\n{passed + failed} tests: {passed} passed, {failed} failed.");
if (failed > 0) Environment.Exit(1);
