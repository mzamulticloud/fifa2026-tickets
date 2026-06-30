using Fifa2026.V2.McpServer.Tools;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// AC-9 / segurança — o McpServer LÊ X-Entra-OID (propagado pelo gateway) só para
/// logging mascarado e NUNCA loga o GUID completo (PII). Confirma:
///   - header ausente → "anônimo";
///   - máscara expõe no máximo 8 chars + reticências (nunca o oid inteiro).
/// </summary>
public sealed class EntraOidContextTests
{
    private static EntraOidContext WithHeader(string? value)
    {
        var ctx = new DefaultHttpContext();
        if (value is not null)
        {
            ctx.Request.Headers[EntraOidContext.HeaderName] = value;
        }
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(ctx);
        return new EntraOidContext(accessor.Object);
    }

    [Fact]
    public void Missing_header_returns_anonimo_and_null_raw()
    {
        var sut = WithHeader(value: null);
        Assert.Null(sut.GetRawOid());
        Assert.Equal("anônimo", sut.GetMaskedOidForLog());
    }

    [Fact]
    public void Present_header_is_exposed_raw_but_masked_for_log()
    {
        const string oid = "12345678-aaaa-bbbb-cccc-dddddddddddd";
        var sut = WithHeader(oid);

        Assert.Equal(oid, sut.GetRawOid());

        var masked = sut.GetMaskedOidForLog();
        Assert.Equal("12345678…", masked);
        Assert.DoesNotContain(oid, masked); // GUID completo NUNCA aparece no log
    }

    [Theory]
    [InlineData("short", "********")]
    [InlineData("", "anônimo")]
    [InlineData("   ", "anônimo")]
    public void Mask_handles_edge_cases(string input, string expected)
    {
        Assert.Equal(expected, EntraOidContext.Mask(input));
    }
}
