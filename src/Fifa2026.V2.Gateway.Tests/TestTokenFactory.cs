using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 2.3 AC-12 + Story 2.11 (Quartas) — fábrica de tokens JWT de teste assinados
/// com uma chave RSA conhecida (compartilhada com a validação de teste no
/// <see cref="GatewayTestFixture"/>). Permite mintar tokens válidos e tokens
/// deliberadamente inválidos (expirado, issuer errado, audience errado) sem depender
/// do Entra real.
///
/// Story 2.11 — DOIS MUNDOS: além do token do CLIENTE (issuer CIAM, ciamlogin.com),
/// emite o token de ADMIN (issuer workforce, login.microsoftonline.com) com a claim de
/// role "Admin". O gateway sob teste roteia cada token ao handler do seu issuer
/// (PolicyScheme selector). A mesma chave RSA de teste assina ambos — o que distingue
/// os mundos no teste é o ISSUER (e o aud), exatamente como em produção.
/// </summary>
public static class TestTokenFactory
{
    // ---- CLIENTE (CIAM / Entra External ID) ----
    // O gateway deriva a authority CIAM de Jwt:CiamTenantId como
    //   https://<tenantId>.ciamlogin.com/<tenantId>/v2.0
    // logo o ValidIssuer esperado tem essa forma. Usamos um GUID de tenant de teste.
    public const string CiamTenantId = "11111111-2222-3333-4444-555555555555";
    public const string CiamClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    public static string CiamIssuer =>
        $"https://{CiamTenantId}.ciamlogin.com/{CiamTenantId}/v2.0";

    // ---- ADMIN (workforce) ----
    public const string AdminTenantId = "99999999-8888-7777-6666-555544443333";
    public const string AdminClientId = "bbbbbbbb-cccc-dddd-eeee-ffffffffffff";
    public static string AdminIssuer =>
        $"https://login.microsoftonline.com/{AdminTenantId}/v2.0";

    // ---- Compat Story 2.3 (cliente é o caminho default; aponta para o CIAM) ----
    public const string TenantId = CiamTenantId;
    public const string ClientId = CiamClientId;
    public static string ValidIssuer => CiamIssuer;

    /// <summary>Chave RSA única do processo de teste (substitui o JWKS do Entra).</summary>
    private static readonly RsaSecurityKey SigningKey = CreateKey();

    /// <summary>SecurityKey pública usada pela validação de teste (IssuerSigningKey).</summary>
    public static SecurityKey PublicSigningKey => SigningKey;

    private static RsaSecurityKey CreateKey()
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = "test-key-1" };
    }

    /// <summary>
    /// Gera um token de CLIENTE (CIAM). Por padrão é VÁLIDO (issuer/aud CIAM corretos,
    /// não expirado, com claim oid). Sobrescreva os parâmetros para produzir os cenários
    /// de rejeição (expirado, issuer/aud errados).
    /// </summary>
    public static string Create(
        string? issuer = null,
        string? audience = null,
        DateTime? expires = null,
        string? oid = "99999999-8888-7777-6666-555555555555")
    {
        var claims = new List<Claim> { new("sub", "test-subject") };
        if (!string.IsNullOrEmpty(oid))
        {
            claims.Add(new Claim("oid", oid));
        }

        return Sign(
            issuer: issuer ?? CiamIssuer,
            audience: audience ?? CiamClientId,
            claims: claims,
            expires: expires);
    }

    /// <summary>
    /// Gera um token de ADMIN (issuer workforce). Por padrão inclui a claim de role
    /// "Admin" (App Role construída no Bloco 3 — ADE-007 Inv 5). Passe
    /// <paramref name="includeAdminRole"/> = false para simular um admin SEM a role
    /// (deve receber 403 na rota AdminOnly). O oid também é incluído (admin é um
    /// usuário com identidade).
    /// </summary>
    public static string CreateAdmin(
        bool includeAdminRole = true,
        string? oid = "abababab-cdcd-efef-1212-343434343434",
        DateTime? expires = null)
    {
        var claims = new List<Claim> { new("sub", "test-admin") };
        if (!string.IsNullOrEmpty(oid))
        {
            claims.Add(new Claim("oid", oid));
        }
        if (includeAdminRole)
        {
            // O Entra emite App Roles na claim "roles". O JwtBearer mapeia "roles" para
            // ClaimTypes.Role por padrão, satisfazendo RequireRole("Admin").
            claims.Add(new Claim("roles", "Admin"));
        }

        return Sign(
            issuer: AdminIssuer,
            audience: AdminClientId,
            claims: claims,
            expires: expires);
    }

    private static string Sign(
        string issuer,
        string audience,
        IEnumerable<Claim> claims,
        DateTime? expires)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);

        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        // notBefore no passado (token válido começa a valer já), mas SEMPRE antes do
        // expires — cobre tanto o token válido (expires futuro) quanto o expirado.
        var earliest = DateTime.UtcNow.AddMinutes(-5);
        var notBefore = expiresAt < earliest ? expiresAt.AddMinutes(-5) : earliest;

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
