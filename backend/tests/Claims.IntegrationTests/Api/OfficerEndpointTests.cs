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
/// The officer slice end to end. Test classes share one database and run in parallel, so this class only creates
/// claims on claimant2's POL-PH-0001 (Philippines, Property); the claimant tests use the Singapore policies. Tests
/// inside one class run one after another. Seeded rows are never changed.
/// </summary>
public sealed class OfficerEndpointTests(ApiFactory factory)
{
    private const string Claimant2 = "claimant2@claimdesk.test";
    private const string Officer1 = "officer1@claimdesk.test";
    private const string Officer2 = "officer2@claimdesk.test";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Yesterday in UTC is never in the future in any market and always inside the seeded policy periods.
    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    // ---- POST /api/claims/{id}/assign ----

    [Fact]
    public async Task Two_officers_assigning_at_once_one_wins_and_the_other_gets_409()
    {
        var claimId = await SubmitAsync();
        using var officer1 = await LoginAsync(Officer1);
        using var officer2 = await LoginAsync(Officer2);

        // However the two requests interleave, the loser gets 409: either it loads the claim after the winner
        // committed (already UnderReview) or it loses the xmin check when saving.
        var responses = await Task.WhenAll(
            officer1.PostAsync(AssignUrl(claimId), null, Ct),
            officer2.PostAsync(AssignUrl(claimId), null, Ct));

        try
        {
            responses.Select(r => r.StatusCode).Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            await ShouldBeProblemAsync(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);

            var winnerEmail = responses[0].StatusCode == HttpStatusCode.OK ? Officer1 : Officer2;
            var winnerId = await UserIdAsync(winnerEmail);
            var (assignee, underReviewRows, assignedEvents) = await WithDbAsync(async db => (
                await db.Claims.Where(c => c.Id == claimId).Select(c => c.AssignedOfficerId).SingleAsync(Ct),
                await db.Set<ClaimStatusHistory>().CountAsync(h => h.ClaimId == claimId && h.ToStatus == ClaimStatus.UnderReview, Ct),
                await db.OutboxMessages.CountAsync(m => m.ClaimId == claimId && m.EventType == nameof(ClaimAssigned), Ct)));

            assignee.ShouldBe(winnerId);
            underReviewRows.ShouldBe(1);
            assignedEvents.ShouldBe(1); // the loser's history and outbox inserts rolled back with its update
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- full flow: assign -> information -> answer -> assessment -> approve -> settle ----

    [Fact]
    public async Task Officer_works_a_claim_to_settlement_and_every_step_writes_history_and_an_outbox_row()
    {
        var claimId = await SubmitAsync();
        using var officer = await LoginAsync(Officer1);
        using var claimant = await LoginAsync(Claimant2);

        (await PostOkAsync(officer, AssignUrl(claimId), null)).Status.ShouldBe(ClaimStatus.UnderReview);

        var awaiting = await PostOkAsync(officer, $"/api/claims/{claimId}/information-requests",
            new InformationRequestRequest("Please upload photos of the roof damage."));
        awaiting.Status.ShouldBe(ClaimStatus.AwaitingInformation);
        var requestId = awaiting.OpenInformationRequests.ShouldHaveSingleItem().Id;

        (await PostOkAsync(claimant, $"/api/claims/{claimId}/information-requests/{requestId}/response",
            new InformationResponseRequest("Photos uploaded."))).Status.ShouldBe(ClaimStatus.UnderReview);

        (await PostOkAsync(officer, $"/api/claims/{claimId}/assessment",
            new AssessmentRequest(50_000m, "PHP", "Contractor quote reviewed."))).Status.ShouldBe(ClaimStatus.UnderReview);

        var approved = await PostOkAsync(officer, DecisionUrl(claimId),
            new DecisionRequest(DecisionOutcome.Approved, 45_000m, "PHP", "Covered storm damage."));
        approved.Status.ShouldBe(ClaimStatus.Approved);
        approved.Decision.ShouldNotBeNull().ApprovedAmount.ShouldBe(new MoneyDto(45_000m, "PHP"));

        var settled = await PostOkAsync(officer, $"/api/claims/{claimId}/settle", null);
        settled.Status.ShouldBe(ClaimStatus.Settled);
        settled.Timeline.Select(t => t.ToStatus).ShouldBe([
            ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.AwaitingInformation,
            ClaimStatus.UnderReview, ClaimStatus.Approved, ClaimStatus.Settled]);

        var (reserve, notes, events) = await WithDbAsync(async db =>
        {
            var claim = await db.Claims.AsNoTracking().SingleAsync(c => c.Id == claimId, Ct);
            var types = await db.OutboxMessages
                .Where(m => m.ClaimId == claimId)
                .OrderBy(m => m.OccurredAt).ThenBy(m => m.Id)
                .Select(m => m.EventType)
                .ToListAsync(Ct);
            return (claim.Reserve, claim.AssessmentNotes, types);
        });

        reserve.ShouldBe(new Money(50_000m, "PHP"));
        notes.ShouldBe("Contractor quote reviewed.");
        events.ShouldBe([
            nameof(ClaimSubmitted), nameof(ClaimAssigned), nameof(InformationRequested), nameof(InformationProvided),
            nameof(ClaimAssessed), nameof(ClaimApproved), nameof(ClaimSettled)]);

        // Settled is final.
        using var again = await officer.PostAsync($"/api/claims/{claimId}/settle", null, Ct);
        await ShouldBeProblemAsync(again, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rejected_claim_records_the_reason_and_cannot_be_settled()
    {
        var claimId = await AssignedWithReserveAsync(Officer1);
        using var officer = await LoginAsync(Officer1);

        var rejected = await PostOkAsync(officer, DecisionUrl(claimId),
            new DecisionRequest(DecisionOutcome.Rejected, null, null, "Wear and tear is excluded."));

        rejected.Status.ShouldBe(ClaimStatus.Rejected);
        rejected.Decision.ShouldNotBeNull().Reason.ShouldBe("Wear and tear is excluded.");
        rejected.Decision.ApprovedAmount.ShouldBeNull();
        (await WithDbAsync(db => db.OutboxMessages.AnyAsync(m => m.ClaimId == claimId && m.EventType == nameof(ClaimRejected), Ct)))
            .ShouldBeTrue();

        using var settle = await officer.PostAsync($"/api/claims/{claimId}/settle", null, Ct);
        await ShouldBeProblemAsync(settle, HttpStatusCode.Conflict);
    }

    // ---- refusals ----

    [Fact]
    public async Task Decision_before_a_reserve_is_recorded_is_409()
    {
        var claimId = await SubmitAsync();
        using var officer = await LoginAsync(Officer1);
        (await PostOkAsync(officer, AssignUrl(claimId), null)).Status.ShouldBe(ClaimStatus.UnderReview);

        using var response = await officer.PostAsJsonAsync(DecisionUrl(claimId),
            new DecisionRequest(DecisionOutcome.Rejected, null, null, "Too early."), Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approval_above_the_maximum_payable_is_409()
    {
        var claimId = await AssignedWithReserveAsync(Officer1);
        using var officer = await LoginAsync(Officer1);

        // POL-PH-0001: limit 3,000,000 - deductible 20,000 = 2,980,000 PHP.
        using var response = await officer.PostAsJsonAsync(DecisionUrl(claimId),
            new DecisionRequest(DecisionOutcome.Approved, 2_980_000.01m, "PHP", "Too much."), Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Officer_who_is_not_assigned_cannot_work_the_claim()
    {
        var claimId = await AssignedWithReserveAsync(Officer1);
        using var other = await LoginAsync(Officer2);

        using var response = await other.PostAsJsonAsync(DecisionUrl(claimId),
            new DecisionRequest(DecisionOutcome.Rejected, null, null, "Not mine."), Json, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approval_without_an_amount_is_400()
    {
        var claimId = await AssignedWithReserveAsync(Officer1);
        using var officer = await LoginAsync(Officer1);

        using var response = await officer.PostAsJsonAsync(DecisionUrl(claimId),
            new { outcome = "Approved", reason = "Covered." }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("ApprovedAmount", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Claimant_cannot_assign()
    {
        var claimId = await SubmitAsync();
        using var claimant = await LoginAsync(Claimant2);

        using var assign = await claimant.PostAsync(AssignUrl(claimId), null, Ct);

        assign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- helpers ----

    private static string AssignUrl(Guid claimId) => $"/api/claims/{claimId}/assign";

    private static string DecisionUrl(Guid claimId) => $"/api/claims/{claimId}/decision";

    /// <summary>A fresh Submitted claim on POL-PH-0001.</summary>
    private async Task<Guid> SubmitAsync()
    {
        using var claimant = await LoginAsync(Claimant2);
        using var response = await claimant.PostAsJsonAsync("/api/claims", new SubmitClaimRequest(
            "POL-PH-0001", ProductType.Property, Yesterday, "Typhoon damage to roof tiles.", 80_000m, "PHP"), Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ClaimDetail>(Json, Ct))!.Id;
    }

    /// <summary>A fresh claim assigned to <paramref name="officerEmail"/> with a reserve, ready for a decision.</summary>
    private async Task<Guid> AssignedWithReserveAsync(string officerEmail)
    {
        var claimId = await SubmitAsync();
        using var officer = await LoginAsync(officerEmail);
        await PostOkAsync(officer, AssignUrl(claimId), null);
        await PostOkAsync(officer, $"/api/claims/{claimId}/assessment", new AssessmentRequest(60_000m, "PHP", null));
        return claimId;
    }

    private static async Task<ClaimDetail> PostOkAsync(HttpClient client, string url, object? body)
    {
        using var response = await client.PostAsync(url, body is null ? null : JsonContent.Create(body, body.GetType(), options: Json), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ClaimDetail>(Json, Ct))!;
    }

    private Task<Guid> UserIdAsync(string email) =>
        WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(Ct));

    /// <summary>A context of its own, outside any request, to inspect rows directly.</summary>
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

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
    }
}
