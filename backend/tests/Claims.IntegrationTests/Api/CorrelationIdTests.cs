using Claims.Api.Observability;

namespace Claims.IntegrationTests.Api;

public sealed class CorrelationIdTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<string> SendAsync(string? correlationId)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        using var response = await client.SendAsync(request, Ct);
        return response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
    }

    [Fact]
    public async Task Well_formed_inbound_id_is_echoed()
    {
        (await SendAsync("frontend-42.abc_DEF")).ShouldBe("frontend-42.abc_DEF");
    }

    [Fact]
    public async Task Missing_id_falls_back_to_the_trace_id()
    {
        (await SendAsync(null)).ShouldMatch("^[0-9a-f]{32}$");
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("new\\nline")]
    [InlineData("<script>")]
    public async Task Malformed_inbound_id_is_replaced(string inbound)
    {
        var id = await SendAsync(inbound);

        id.ShouldNotBe(inbound);
        id.ShouldMatch("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task Overlong_inbound_id_is_replaced()
    {
        (await SendAsync(new string('a', 65))).ShouldMatch("^[0-9a-f]{32}$");
    }
}
