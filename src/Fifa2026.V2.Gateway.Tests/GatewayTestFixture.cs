using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WireMock.Server;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Sobe o gateway YARP via <see cref="WebApplicationFactory{TEntryPoint}"/> com um
/// backend Function F1 mockado por <see cref="WireMockServer"/>. A env
/// <c>FunctionAppF1Url</c> aponta o cluster YARP para o WireMock (ADE-003 Inv 3 —
/// destination externalizada). Isola os testes de integração do Azure real.
///
/// Story 2.3 / 2.11 — fornece config de identidade DUAL-ISSUER VÁLIDA (Jwt:Ciam* +
/// Jwt:Admin*, fail-closed exige tenant real NÃO 'common' em ambos) e sobrescreve a
/// validação dos esquemas "Ciam" e "Admin" para usar uma chave RSA de teste conhecida
/// em vez do JWKS real — permitindo mintar tokens cliente/admin válidos e inválidos
/// offline (AC-6/AC-12). O PolicyScheme "Selector" do Program.cs continua roteando cada
/// token ao handler do seu issuer (não é sobrescrito — é a unidade sob teste).
/// </summary>
public sealed class GatewayTestFixture : WebApplicationFactory<Program>
{
    public WireMockServer Backend { get; }

    /// <summary>Shared secret de teste injetado como X-Gateway-Key nas rotas /admin/*.</summary>
    public const string AdminSharedSecret = "test-shared-secret-123";

    public GatewayTestFixture()
    {
        Backend = WireMockServer.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting escreve direto na configuração do host que builder.Configuration
        // lê em tempo de build no Program.cs (precede appsettings.json) — garante que
        // a config fail-closed dos DOIS mundos chegue antes da checagem de startup.
        builder.UseSetting("FunctionAppF1Url", Backend.Url);
        builder.UseSetting("Gateway:FrontendOrigin", "https://fifa2026-web.azurewebsites.net");
        // Quartas / "admin 100% workforce" — o cluster backend-v1 aponta pro MESMO
        // WireMock (que também serve /api/admin/*). O shared secret de teste é injetado
        // como X-Gateway-Key nas rotas /admin/* (AdminProxyTests valida a injeção).
        builder.UseSetting("BackendV1Url", Backend.Url);
        builder.UseSetting("Gateway:AdminSharedSecret", AdminSharedSecret);
        // CLIENTE (CIAM)
        builder.UseSetting("Jwt:CiamTenantId", TestTokenFactory.CiamTenantId);
        builder.UseSetting("Jwt:CiamClientId", TestTokenFactory.CiamClientId);
        // ADMIN (workforce)
        builder.UseSetting("Jwt:AdminTenantId", TestTokenFactory.AdminTenantId);
        builder.UseSetting("Jwt:AdminClientId", TestTokenFactory.AdminClientId);

        builder.ConfigureTestServices(services =>
        {
            // Substitui a validação dos esquemas concretos "Ciam" e "Admin": em vez de
            // buscar o JWKS no discovery real (rede + chaves reais), valida contra a
            // chave RSA de teste. iss/aud/lifetime continuam validados EXPLICITAMENTE —
            // os mesmos parâmetros de segurança do Program.cs, só que com a chave de
            // teste. Os cenários de rejeição (AC-6) continuam valendo. O selector NÃO é
            // tocado — é exatamente o roteamento por issuer que os testes exercitam.
            services.PostConfigure<JwtBearerOptions>("Ciam", options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.ConfigurationManager = null!;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestTokenFactory.CiamIssuer,
                    ValidateAudience = true,
                    ValidAudiences = new[]
                    {
                        TestTokenFactory.CiamClientId,
                        $"api://{TestTokenFactory.CiamClientId}"
                    },
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestTokenFactory.PublicSigningKey,
                    ClockSkew = TimeSpan.Zero
                };
            });

            services.PostConfigure<JwtBearerOptions>("Admin", options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.ConfigurationManager = null!;
                // Não renomeia "roles" → URI longa (espelha o Program.cs); RoleClaimType
                // "roles" abaixo faz RequireRole("Admin") casar a App Role do token.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestTokenFactory.AdminIssuer,
                    ValidateAudience = true,
                    ValidAudiences = new[]
                    {
                        TestTokenFactory.AdminClientId,
                        $"api://{TestTokenFactory.AdminClientId}"
                    },
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestTokenFactory.PublicSigningKey,
                    // O Entra emite App Roles na claim "roles". Mapeia "roles" → role
                    // claim type para que RequireRole("Admin") seja satisfeito.
                    RoleClaimType = "roles",
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Backend.Stop();
            Backend.Dispose();
        }
        base.Dispose(disposing);
    }
}
