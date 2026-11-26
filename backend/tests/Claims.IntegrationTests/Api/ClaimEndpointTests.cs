using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Auth;
using Claims.Application.Claims;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.IntegrationTests.Api;

/// <summary>
/// The claimant slice end to end. Each test submits its own claims, so seeded rows are never changed.
/// claimant1 holds POL-SG-0001 (Motor) and POL-SG-0002 (Property); claimant2 holds the HK/TH/PH policies.
/// </summary>
public sealed class ClaimEndpointTests(ApiFactory factory)
{
    private const string Claimant1 = "claimant1@claimdesk.test";
    private const string Claimant2 = "claimant2@claimdesk.test";
    private const string Officer1 = "officer1@claimdesk.test";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Yesterday in UTC is never in the future in any market and always inside the seeded policy periods.
    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    // ---- POST /api/claims ----

    [Fact]
    public async Task Claimant_submits_a_claim_and_the_event_is_in_the_outbox_with_the_correlation_id()
    {
        using var client = await LoginAsync(Claimant1);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims") { Content = JsonContent.Create(MotorClaim(), options: Json) };
        request.Headers.Add("X-Correlation-Id", "submit-test-1");

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDetail>(Json, Ct))!;
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/claims/{claim.Id}");
        claim.Status.ShouldBe(ClaimStatus.Submitted);
        claim.ClaimNumber.ShouldStartWith("CLM-");
        claim.PolicyNumber.ShouldBe("POL-SG-0001");
        claim.Market.ShouldBe(Market.Singapore);
        claim.ClaimedAmount.ShouldBe(new MoneyDto(1_250.50m, "SGD"));
        claim.MaxPayable.ShouldBe(new MoneyDto(49_500m, "SGD"));
        claim.Timeline.ShouldHaveSingleItem().ToStatus.ShouldBe(ClaimStatus.Submitted);
        claim.OpenInformationRequests.ShouldBeEmpty();

        var outbox = await WithDbAsync(db => db.OutboxMessages.AsNoTracking().Where(m => m.ClaimId == claim.Id).ToListAsync(Ct));
        var message = outbox.ShouldHaveSingleItem();
        message.EventType.ShouldBe(nameof(ClaimSubmitted));
        message.CorrelationId.ShouldBe("submit-test-1");
        message.SentAt.ShouldBeNull();
    }

    [Fact]
    public async Task Claim_for_a_product_the_policy_does_not_cover_is_409()
    {
        using var client = await LoginAsync(Claimant1);

        using var response = await client.PostAsJsonAsync("/api/claims", MotorClaim() with { ProductType = ProductType.Property }, Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Claim_on_someone_elses_policy_is_404()
    {
        using var client = await LoginAsync(Claimant2);

        using var response = await client.PostAsJsonAsync("/api/claims", MotorClaim(), Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Claim_without_a_product_type_is_400()
    {
        using var client = await LoginAsync(Claimant1);

        using var response = await client.PostAsJsonAsync("/api/claims", new
        {
            policyNumber = "POL-SG-0001",
            incidentDate = Yesterday,
            description = "No product type",
            claimedAmount = 100m,
            currency = "SGD",
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("ProductType", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Officer_cannot_submit_a_claim()
    {
        using var client = await LoginAsync(Officer1);

        using var response = await client.PostAsJsonAsync("/api/claims", MotorClaim(), Json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- GET /api/claims/mine ----

    [Fact]
    public async Task Mine_lists_only_the_claimants_own_claims_newest_first()
    {
        using var client = await LoginAsync(Claimant1);
        var submitted = await SubmitAsync(client);

        var mine = (await client.GetFromJsonAsync<List<ClaimSummary>>("/api/claims/mine", Json, Ct))!;

        mine.ShouldContain(c => c.Id == submitted.Id);
        mine.Select(c => c.PolicyNumber).ShouldAllBe(n => n.StartsWith("POL-SG") || n.StartsWith("POL-MY") || n.StartsWith("POL-ID"));
        mine.Select(c => c.SubmittedAt).ShouldBeInOrder(SortDirection.Descending);
    }

    // ---- GET /api/claims/{id} ----

    [Fact]
    public async Task Claimant_reads_their_own_claim()
    {
        using var client = await LoginAsync(Claimant1);
        var submitted = await SubmitAsync(client);

        var claim = await client.GetFromJsonAsync<ClaimDetail>($"/api/claims/{submitted.Id}", Json, Ct);

        claim.ShouldNotBeNull();
        claim.Id.ShouldBe(submitted.Id);
        claim.Description.ShouldBe("Rear-ended at a junction.");
        claim.Timeline.Select(t => t.ToStatus).ShouldBe([ClaimStatus.Submitted]);
    }

    [Fact]
    public async Task Another_claimant_reading_the_claim_is_403()
    {
        using var owner = await LoginAsync(Claimant1);
        var submitted = await SubmitAsync(owner);
        using var other = await LoginAsync(Claimant2);

        using var response = await other.GetAsync($"/api/claims/{submitted.Id}", Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unknown_claim_is_404()
    {
        using var client = await LoginAsync(Claimant1);

        using var response = await client.GetAsync($"/api/claims/{Guid.CreateVersion7()}", Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Officer_reads_any_claim_including_its_decision()
    {
        // The oldest seeded claim is Settled without an information request.
        var settledId = await WithDbAsync(db => db.Claims
            .Where(c => c.Status == ClaimStatus.Settled)
            .OrderBy(c => c.ClaimNumber)
            .Select(c => c.Id)
            .FirstAsync(Ct));

        using var client = await LoginAsync(Officer1);

        var claim = (await client.GetFromJsonAsync<ClaimDetail>($"/api/claims/{settledId}", Json, Ct))!;

        claim.Decision.ShouldNotBeNull();
        claim.Decision.Outcome.ShouldBe(DecisionOutcome.Approved);
        claim.Decision.ApprovedAmount.ShouldNotBeNull().Currency.ShouldBe(claim.ClaimedAmount.Currency);
        claim.Timeline.Select(t => t.ToStatus).ShouldBe(
            [ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.Approved, ClaimStatus.Settled]);
    }

    // ---- POST /api/claims/{id}/information-requests/{requestId}/response ----

    [Fact]
    public async Task Claimant_answers_the_open_request_and_the_claim_goes_back_to_review()
    {
        using var client = await LoginAsync(Claimant1);
        var (claimId, requestId) = await ArrangeAwaitingInformationAsync(client);

        using var response = await client.PostAsJsonAsync(ResponseUrl(claimId, requestId), new InformationResponseRequest("Report ref PR-123."), Json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDetail>(Json, Ct))!;
        claim.Status.ShouldBe(ClaimStatus.UnderReview);
        claim.OpenInformationRequests.ShouldBeEmpty();
        claim.Timeline.Select(t => t.ToStatus).ShouldBe(
            [ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.AwaitingInformation, ClaimStatus.UnderReview]);

        (await WithDbAsync(db => db.OutboxMessages.AnyAsync(m => m.ClaimId == claimId && m.EventType == nameof(InformationProvided), Ct)))
            .ShouldBeTrue();

        // Answering again: the claim is no longer awaiting information.
        using var again = await client.PostAsJsonAsync(ResponseUrl(claimId, requestId), new InformationResponseRequest("Again."), Json, Ct);
        await ShouldBeProblemAsync(again, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Another_claimant_answering_is_403()
    {
        using var owner = await LoginAsync(Claimant1);
        var (claimId, requestId) = await ArrangeAwaitingInformationAsync(owner);
        using var other = await LoginAsync(Claimant2);

        using var response = await other.PostAsJsonAsync(ResponseUrl(claimId, requestId), new InformationResponseRequest("Not mine."), Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Answering_an_unknown_request_is_404()
    {
        using var client = await LoginAsync(Claimant1);
        var (claimId, _) = await ArrangeAwaitingInformationAsync(client);

        using var response = await client.PostAsJsonAsync(ResponseUrl(claimId, Guid.CreateVersion7()), new InformationResponseRequest("?"), Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound);
    }

    // ---- helpers ----

    private static SubmitClaimRequest MotorClaim() =>
        new("POL-SG-0001", ProductType.Motor, Yesterday, "Rear-ended at a junction.", 1_250.50m, "SGD");

    private static string ResponseUrl(Guid claimId, Guid requestId) =>
        $"/api/claims/{claimId}/information-requests/{requestId}/response";

    /// <summary>A context of its own, outside any request, to arrange and inspect rows directly.</summary>
    private async Task<T> WithDbAsync<T>(Func<ClaimsDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ClaimsDbContext>());
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DbSeeder.DemoPassword), Json, Ct);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json, Ct))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<ClaimDetail> SubmitAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/claims", MotorClaim(), Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ClaimDetail>(Json, Ct))!;
    }

    /// <summary>Submits through the API, then plays the officer side through the aggregate (no officer endpoints yet).</summary>
    private async Task<(Guid ClaimId, Guid RequestId)> ArrangeAwaitingInformationAsync(HttpClient claimant)
    {
        var submitted = await SubmitAsync(claimant);

        var requestId = await WithDbAsync(async db =>
        {
            var officerId = await db.Users.Where(u => u.Email == Officer1).Select(u => u.Id).SingleAsync(Ct);
            var claim = await db.Claims
                .Include(c => c.StatusHistory)
                .Include(c => c.InformationRequests)
                .SingleAsync(c => c.Id == submitted.Id, Ct);
            claim.Assign(officerId, DateTimeOffset.UtcNow);
            claim.RequestInformation(officerId, "Please send the police report reference.", DateTimeOffset.UtcNow);
            claim.ClearDomainEvents(); // test arrangement, not news
            await db.SaveChangesAsync(Ct);
            return claim.InformationRequests.Single(r => r.IsOpen).Id;
        });

        return (submitted.Id, requestId);
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
    }
}
