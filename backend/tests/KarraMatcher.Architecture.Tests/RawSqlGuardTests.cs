using System.Reflection;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// Förbjuder rå SQL i produktionskoden.
///
/// <para>
/// De rå EF Core-API:erna (<c>FromSqlRaw</c>, <c>ExecuteSqlRaw</c>, <c>SqlQueryRaw</c>) tar en
/// sträng och kör den som SQL. Interpoleras en användarinmatning in i den strängen är det den
/// klassiska injektionsvägen — och den vanligaste orsaken bakom läckta personregister. Vi
/// använder ingen av dem: all datatillgång går via LINQ eller parametriserade frågor, och det
/// ska förbli så. Skulle någon införa en rå fråga ska bygget falla här, inte upptäckas i drift.
/// </para>
///
/// <para>
/// Vi läser källan och inte den byggda assemblyn av samma skäl som
/// <see cref="ProjectReferenceTests"/>: regeln handlar om vad koden får skriva, inte om vad
/// kompilatorn råkade behålla. De parametrisera-säkra <c>FromSql</c>/<c>ExecuteSql</c>
/// (FormattableString) är inte förbjudna — men vi använder inte dem heller.
/// </para>
/// </summary>
public class RawSqlGuardTests
{
    private static readonly string BackendRoot =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "BackendRoot").Value!;

    // "ExecuteSqlRaw" fångar även den asynkrona varianten (den bär strängen som del av namnet).
    private static readonly string[] Forbidden = ["FromSqlRaw", "ExecuteSqlRaw", "SqlQueryRaw"];

    [Fact]
    public void Produktionskoden_AnvanderIngenRaSql()
    {
        var srcRoot = Path.Combine(BackendRoot, "src");
        Assert.True(Directory.Exists(srcRoot), $"Hittade inte {srcRoot}");

        var offenders = new List<string>();

        foreach (var file in EnumerateSourceFiles(srcRoot))
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var token = ForbiddenTokenIn(lines[i]);

                if (token is not null)
                {
                    offenders.Add($"{Path.GetRelativePath(srcRoot, file)}:{i + 1}: {token}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Rå SQL är förbjuden i produktionskoden — parametrisera via LINQ eller FromSql "
                + "(FormattableString). Träffar:\n"
                + string.Join('\n', offenders));
    }

    [Fact]
    public void Vakten_KannerIgenRaSql()
    {
        // Vaktar vakten: en regel som slutat matcha faller tyst. Här bevisas att den träffar.
        Assert.Equal("FromSqlRaw", ForbiddenTokenIn("        return db.Set<Foo>().FromSqlRaw(sql);"));
        Assert.Equal("ExecuteSqlRaw", ForbiddenTokenIn("await db.Database.ExecuteSqlRawAsync(sql);"));
        Assert.Null(ForbiddenTokenIn("        var rows = await db.Foos.Where(f => f.Id == id).ToListAsync();"));
    }

    private static string? ForbiddenTokenIn(string line) =>
        Array.Find(Forbidden, token => line.Contains(token, StringComparison.Ordinal));

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(obj, StringComparison.Ordinal)
                && !path.Contains(bin, StringComparison.Ordinal));
    }
}
