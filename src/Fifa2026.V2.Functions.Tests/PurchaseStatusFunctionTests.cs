using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Functions;
using Fifa2026.V2.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// AC-8 — endpoint de status: GUID inválido → 400; sem linha → queued;
/// linha completed → status + ticketId.
/// </summary>
public sealed class PurchaseStatusFunctionTests
{
    private static PurchaseStatusFunction CreateSut(IPurchaseRepository repo) =>
        new(repo, NullLogger<PurchaseStatusFunction>.Instance);

    private static HttpRequest EmptyRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task Invalid_guid_returns_bad_request()
    {
        var sut = CreateSut(Mock.Of<IPurchaseRepository>());

        var result = await sut.RunAsync(EmptyRequest(), "not-a-guid", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task No_row_returns_queued()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.GetStatusByCorrelationIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseStatusResult?)null);

        var sut = CreateSut(repo.Object);

        var result = await sut.RunAsync(EmptyRequest(), id.ToString(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Completed_row_returns_status_result()
    {
        var id = Guid.NewGuid();
        var expected = new PurchaseStatusResult { Status = "completed", TicketId = 42 };
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.GetStatusByCorrelationIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = CreateSut(repo.Object);

        var result = await sut.RunAsync(EmptyRequest(), id.ToString(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<PurchaseStatusResult>(ok.Value);
        Assert.Equal("completed", payload.Status);
        Assert.Equal(42, payload.TicketId);
    }
}
