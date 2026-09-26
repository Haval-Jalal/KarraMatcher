using System.Reflection;
using System.Text.RegularExpressions;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// §KM.10 — vissa fält får aldrig nå en logg.
///
/// <para>
/// Barnets namn, en användares e-post, en push-endpoint, en JWT och all fritext en användare
/// skrivit (chatt, kallelse-hälsning, samåkningsnotis) är förbjudna i loggar. Regeln finns för
/// att en logg är den läckvägen ingen tänker på: den kopieras till en tredjepartstjänst, syns
/// för fler än den avsedda kretsen och sparas längre än datan själv. En läcka därifrån är exakt
/// den sortens intrång §KM.1–§KM.3 är byggda för att omöjliggöra.
/// </para>
///
/// <para>
/// Vakten läser <b>källan</b>, inte den byggda assemblyn — av samma skäl som
/// <see cref="RawSqlGuardTests"/>: regeln handlar om vad koden får skriva, inte om vad
/// kompilatorn råkade behålla. Den granskar varje <c>[LoggerMessage]</c>-mall (projektets
/// enda loggväg — strukturerad Serilog, aldrig <c>Console.WriteLine</c>) och fäller bygget om
/// en mall interpolerar en platshållare vars namn röjer ett förbjudet fält. Den fångar inte
/// allt tänkbart missbruk — ett oskyldigt namngivet fält kan bära e-post — men den fångar den
/// vanligaste regressionen: någon lägger till <c>{Email}</c> eller <c>{Body}</c> i en logg.
/// </para>
///
/// <para>
/// <b>Namngivet undantag:</b> <c>DevelopmentEmailSender</c> loggar med flit mottagare och
/// brödtext — den ersätter mejlutskicket i lokalt utvecklingsläge och finns till just för att
/// visa brevet som annars skickats. Den registreras aldrig i produktion. Undantaget är
/// medvetet och begränsat till den filen, på samma sätt som §KM.6 räknar upp sina audit-noter.
/// </para>
/// </summary>
public class LogRedactionGuardTests
{
    private static readonly string BackendRoot =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "BackendRoot").Value!;

    // Platshållarnamn som förråder ett §KM.10-förbjudet fält. Medvetet snäv och entydig: här
    // står inga tvetydiga ord som {Name} (lag- och spelplatsnamn loggas fritt) eller {Message}
    // (bär oftast ett feltextsvar). Jämförelsen är skiftlägesokänslig.
    private static readonly string[] ForbiddenPlaceholders =
    [
        "Email", "Recipient", "Mail",
        "Body", "Greeting", "Note", "ResponseMessage", "Freetext", "Comment",
        "Endpoint",
        "Token", "Jwt", "RefreshToken", "AccessToken", "Secret", "Password",
        "FirstName", "LastName", "ChildName", "FullName", "Surname",
        "Personnummer", "Ssn", "Phone", "PhoneNumber", "Address",
    ];

    // Filer som med flit loggar ett annars förbjudet fält. Se klassdoc.
    private static readonly string[] NamedExceptions = ["DevelopmentEmailSender.cs"];

    // Fångar hela [LoggerMessage(...)]-attributet, även när det bryts över flera rader. Den
    // negerade klassen korsar radbrytningar av sig själv, så ingen Singleline behövs.
    private static readonly Regex LoggerMessageAttribute = new(@"\[LoggerMessage\b[^\]]*\]");

    // Plockar strängen efter Message = "...", med \"-escaper bevarade.
    private static readonly Regex MessageTemplate = new("Message\\s*=\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

    // Platshållare i en Serilog-mall: {Namn}, {Namn:format}, {@Namn}, {$Namn}.
    private static readonly Regex Placeholder = new(@"\{[@$]?(\w+)");

    [Fact]
    public void Loggmallar_InterpolerarAldrigForbjudetFalt()
    {
        var srcRoot = Path.Combine(BackendRoot, "src");
        Assert.True(Directory.Exists(srcRoot), $"Hittade inte {srcRoot}");

        var offenders = new List<string>();
        var templatesSeen = 0;

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (NamedExceptions.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var source = File.ReadAllText(file);

            foreach (Match attribute in LoggerMessageAttribute.Matches(source))
            {
                var template = MessageTemplate.Match(attribute.Value);
                if (!template.Success)
                {
                    continue;
                }

                templatesSeen++;

                foreach (Match placeholder in Placeholder.Matches(template.Groups[1].Value))
                {
                    var name = placeholder.Groups[1].Value;
                    if (ForbiddenPlaceholders.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        offenders.Add($"{Path.GetFileName(file)}: {{{name}}} i \"{template.Groups[1].Value}\"");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "En loggmall interpolerar ett §KM.10-förbjudet fält. Logga id, inte innehåll:\n"
                + string.Join('\n', offenders));

        // Läsbarhet: hittade vi loggmallar alls? Annars har regexen slutat matcha och vakten
        // vaktar ingenting — det vill vi veta, inte tolka som grönt.
        Assert.True(templatesSeen > 0, "Inga [LoggerMessage]-mallar hittades — vakten matchar inget längre.");
    }

    [Fact]
    public void Vakten_FangarEnForbjudenPlatshallare()
    {
        // Meta: bevisa att regeln träffar en mall som röjer e-post och brödtext. Faller det här
        // testet är detekteringen trasig och tystnaden i testet ovan säger ingenting.
        const string leaky = "Skickar till {Recipient}: {Body}";

        var caught = Placeholder
            .Matches(leaky)
            .Select(m => m.Groups[1].Value)
            .Where(name => ForbiddenPlaceholders.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(["Recipient", "Body"], caught);
    }
}
