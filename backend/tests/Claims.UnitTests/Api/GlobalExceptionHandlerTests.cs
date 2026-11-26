using Claims.Api.Errors;
using Claims.Application.Common;
using Claims.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Claims.UnitTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int> KnownExceptions => new()
    {
        { new DomainException("Only a submitted claim can be assigned."), StatusCodes.Status409Conflict },
        { new DbUpdateConcurrencyException("row version changed"), StatusCodes.Status409Conflict },
        { new NotFoundException("Claim not found."), StatusCodes.Status404NotFound },
        { new ForbiddenException("Not your claim."), StatusCodes.Status403Forbidden },
        { new InvalidOperationException("boom"), StatusCodes.Status500InternalServerError },
    };

    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public void Maps_exception_to_status(Exception exception, int expectedStatus)
    {
        GlobalExceptionHandler.ToProblem(exception).Status.ShouldBe(expectedStatus);
    }

    [Fact]
    public void Business_errors_explain_themselves()
    {
        GlobalExceptionHandler.ToProblem(new DomainException("Only a submitted claim can be assigned."))
            .Detail.ShouldBe("Only a submitted claim can be assigned.");
    }

    [Fact]
    public void Unexpected_errors_do_not_leak_their_message()
    {
        var problem = GlobalExceptionHandler.ToProblem(new InvalidOperationException("Npgsql: password authentication failed"));

        problem.Detail.ShouldBeNull();
        problem.Title!.ShouldNotContain("Npgsql");
    }

    [Fact]
    public async Task Sets_status_code_and_writes_problem_details()
    {
        var writer = Substitute.For<IProblemDetailsService>();
        writer.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);
        var handler = new GlobalExceptionHandler(writer, NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new NotFoundException("Claim not found."), TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        await writer.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c =>
            c.ProblemDetails.Status == StatusCodes.Status404NotFound && c.ProblemDetails.Detail == "Claim not found."));
    }
}
