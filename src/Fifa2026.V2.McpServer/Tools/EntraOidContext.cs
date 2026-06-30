using System.Diagnostics.CodeAnalysis;

namespace Fifa2026.V2.McpServer.Tools;

/// <summary>
/// AC-9 / Task 3.8 — leitura do header <c>X-Entra-OID</c> propagado pelo gateway YARP
/// (ADE-005 Inv 4). O McpServer USA o oid apenas para LOGGING/personalização — NUNCA
/// revalida o JWT (o gateway é o guardião único de autenticação).
///
/// SEGURANÇA: o oid é PII de identidade — só logamos um valor MASCARADO
/// (8 primeiros chars), nunca o GUID completo em texto (ADE-005 / CodeRabbit focus
/// area "entraOid não logado como PII").
/// </summary>
public sealed class EntraOidContext
{
    /// <summary>Header propagado pelo gateway (Story 2.3 AC-7 / ADE-005 Inv 4).</summary>
    public const string HeaderName = "X-Entra-OID";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public EntraOidContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Retorna o oid bruto do header, ou null se ausente (request não autenticado —
    /// o gateway só propaga o header para requests com JWT válido).
    /// </summary>
    public string? GetRawOid()
    {
        var value = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Versão MASCARADA do oid, segura para logging (ex.: "1234abcd…"). Retorna
    /// "anônimo" se o header não veio. NUNCA retorna o GUID completo.
    /// </summary>
    public string GetMaskedOidForLog()
    {
        var oid = GetRawOid();
        return Mask(oid);
    }

    internal static string Mask([AllowNull] string oid)
    {
        if (string.IsNullOrWhiteSpace(oid))
        {
            return "anônimo";
        }
        return oid.Length <= 8 ? "********" : oid[..8] + "…";
    }
}
