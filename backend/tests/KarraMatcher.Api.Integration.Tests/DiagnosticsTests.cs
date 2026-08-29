using System.Net;

using KarraMatcher.Api.Diagnostics;

using Microsoft.AspNetCore.Mvc.Testing;

namespace KarraMatcher.Api.Integration.Tests;

public class CorrelationIdTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    [Fact]
    public async Task Svar_SaknarKlientensId_FarEttNyttCorrelationId()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", CancellationToken.None);

        Assert.True(response.Headers.TryGetValues(
            CorrelationIdMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task Svar_KlientenSkickarId_AteranvanderDet()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "abc-123_XYZ");

        var response = await client.GetAsync("/", CancellationToken.None);

        Assert.Equal(
            "abc-123_XYZ",
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Theory]
    [InlineData("id med mellanslag")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("id\rmed\nradbrytning")]
    public async Task Svar_OtjanligtIdFranKlienten_ErsattsMedEttNytt(string hostile)
    {
        // Idet hamnar i en svarsheader och i loggarna. Vi litar inte på indata.
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName, hostile);

        var response = await client.GetAsync("/", CancellationToken.None);

        var returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.NotEqual(hostile, returned);
        Assert.Matches("^[0-9a-f]{32}$", returned);
    }

    [Fact]
    public async Task Svar_ForLangtIdFranKlienten_ErsattsMedEttNytt()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            CorrelationIdMiddleware.HeaderName, new string('a', 65));

        var response = await client.GetAsync("/", CancellationToken.None);

        Assert.Matches(
            "^[0-9a-f]{32}$",
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }
}

public class ProblemDetailsTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    [Fact]
    public async Task OhanteratFel_Svarar500MedProblemDetails()
    {
        using var app = factory.WithThrowingEndpoint();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/test/kasta", CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OhanteratFel_LackerVarkenStackTraceEllerInternaDetaljer()
    {
        using var app = factory.WithThrowingEndpoint();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/test/kasta", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("HEMLIG-INTERN-DETALJ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.Contains("Något gick fel", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OhanteratFel_HarAndaEttCorrelationId()
    {
        // Utan det går felet inte att hitta i loggarna när någon rapporterar det.
        using var app = factory.WithThrowingEndpoint();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/test/kasta", CancellationToken.None);

        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }
}
