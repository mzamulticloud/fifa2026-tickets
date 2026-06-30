---
title: "F2 — Gateway Profissional em Código com YARP"
subtitle: "Workshop Living Lab Azure-Native · Fase 2 de 6"
theme: black
revealOptions:
  transition: slide
---

# F2 — Gateway em Código

## YARP (.NET) na frente das suas Functions

Workshop **Living Lab Azure-Native** · Fase 2 de 6

`/purchase` → [Gateway YARP] → `/api/v2/purchase`

---

## O dia em 6 blocos · 6h

1. Conceitos: 6 níveis, reverse proxy/BFF, construir vs comprar — 50min
2. Provisioning Registry + Container App via Portal — 45min
3. Routing + CORS + header transform (live coding) — 60min
4. ☕ Coffee — 15min
5. Rate limit + Output Cache + JWT placeholder (live coding) — 60min
6. CI/CD + smoke test — 40min
7. Trade-off APIM vs YARP + Retro — 50min

---

## A frase do dia

# "Um gateway faz por dentro <br/> o que você vai escrever em C#."

<small>Gateway não é caixa-preta. É código que você lê, escreve e depura.</small>

---

## De onde viemos (F1)

```
[Browser] ──POST /api/v2/purchase──> [Function App F1 (Anonymous)]
```

- Qualquer um na internet chama sua Function **direto**
- Sem rate limit, sem CORS, sem validação central
- O cliente **conhece a URL real** do backend

<small>Foi de propósito — segurança não era o tema da F1. Agora é.</small>

---

## Bloco 1 — Conceitos

### 6 níveis · reverse proxy / BFF · construir vs comprar

---

## O que é um API Gateway?

Um **reverse proxy** com inteligência de borda, na frente dos backends.

```
[Browser] ──/purchase──> [Gateway] ──/api/v2/purchase──> [Function F1]
            (URL pública    (regras de borda,             (URL interna,
             genérica)       reescreve o caminho)          escondida)
```

- Cliente fala com o **gateway**, nunca com o backend direto
- **Ponto único de entrada** do sistema

---

## A analogia: a portaria do prédio

- Toda visita passa pela **portaria** (gateway)
- Identifica-se (JWT — em F3)
- Respeita as regras da casa (rate limit, CORS)
- Ganha um crachá de rastreio (`X-Correlation-ID`)
- Só então vai ao apartamento certo (a Function)

<small>Os apartamentos não precisam ter cada um sua própria portaria.</small>

---

## Reverse proxy ≠ proxy

- **Proxy (forward):** representa o **cliente** (saída de internet da empresa)
- **Reverse proxy:** representa o **servidor** — o cliente nem sabe que existe

<small>Gateways são **reverse** proxies. O cliente acha que fala com a aplicação.</small>

---

## Os 6 níveis de um gateway

| Nível | Conceito |
|---|---|
| **0** | Ponto único de entrada (reverse proxy / BFF) |
| **1** | Roteamento (path/host) |
| **2** | **Transversais: rate limit, CORS, transform, cache, JWT** ← coração |
| **3** | Resiliência (load balancing, health checks) |
| **4** | Observabilidade de borda (tracing centralizado) |
| **5** | Meta-aprendizado: **construir vs comprar** |

<small>Construímos 0-2 em código · tocamos 3-5 conceitualmente.</small>

---

## Construímos com YARP

**YARP** = Yet Another Reverse Proxy

- Biblioteca **open-source da Microsoft**
- Roda como app **ASP.NET Core** (.NET 8)
- Pacote NuGet: `Yarp.ReverseProxy` **2.2.0**
- Config em `appsettings.json` (declarativa) + C# (programática)

<small>O gateway vira **código no repo**, versionado junto com os microsserviços.</small>

---

## YARP: Routes e Clusters

```
ReverseProxy
 ├── Routes    "QUANDO chega X, mande para o cluster Y"
 │     ├── purchase-post : /purchase POST    → functions-f1
 │     └── purchase-get  : /purchase/{id} GET → functions-f1
 └── Clusters  "ONDE estão os backends"
       └── functions-f1
             └── Destinations: { f1: localhost:7071 (dev) }
```

- **Route** = match + cluster + transforms
- **Cluster** = grupo de destinations (backends)

---

## A pipeline de middleware (a ORDEM importa!)

```
requisição
  ▼
1. UseCors            origem permitida?
2. UseRateLimiter     6ª/min → 429 (nem chega ao backend)
3. XCacheMiddleware   marca X-Cache: MISS por padrão
4. UseOutputCache     resposta cacheada? devolve + X-Cache: HIT
5. UseAuthentication  valida JWT (F2: configurado, anônimo)
6. UseAuthorization   políticas (F2: nenhuma exigida)
  ▼
7. MapReverseProxy    encaminha + reescreve + X-Correlation-ID
```

---

## Por que a ordem importa?

- **Rate limit ANTES do proxy** → barra o abuso **antes** de gastar o backend
- **CORS no início** → rejeita origens proibidas logo
- **Auth antes do proxy** → nada não-autenticado chega ao backend (em F3)

> Errar a ordem = comportamento **sutilmente** quebrado.

<small>É a armadilha nº1 da fase.</small>

---

## A grande decisão: construir vs comprar

# YARP <br/> vs <br/> APIM

<small>O blueprint pedia **Azure API Management**. Trocamos por **YARP em código**. Por quê?</small>

---

## Trade-off: APIM vs YARP

| Eixo | APIM (comprar) | YARP (construir) |
|---|---|---|
| **Custo** | ~US$50-80 | **~US$0** |
| **Provisioning** | **30-45 min** | **segundos** |
| **Config** | XML proprietário | **C# no repo** |
| **Transparência** | caixa-preta | **vê o mecanismo** |
| **Produto** | portal, test-console, analytics | usa curl + logs + App Insights |
| **Operação** | gerenciado | é código seu |

---

## A nota didática (guarde)

> Em produção corporativa, o equivalente gerenciado deste gateway **é o APIM**.

- Construímos em código para **ver o que um gateway faz por dentro**
- APIM é a escolha certa em muitos cenários reais (governança, portal, menos operação)
- **Não é "YARP > APIM"** — é "para aprender, construir; para muita produção, comprar"

<small>Saber **decidir** entre os dois é o que esta fase te dá.</small>

---

## Paridade APIM → YARP (nada se perde)

| Policy APIM | Código no nosso gateway | AC |
|---|---|---|
| `rate-limit-by-key` | `AddRateLimiter` → 429 | AC-5 |
| `cache-lookup/store` | `AddOutputCache` + X-Cache | AC-6 |
| `cors` | `AddCors` + `UseCors` | AC-7 |
| `set-header` | `AddRequestTransform` | AC-8 |
| `rewrite-uri` | `PathSet`/`PathPattern` | AC-4 |
| `validate-jwt` (placeholder) | `AddJwtBearer` anônimo | AC-9 |

---

## Bloco 2 — Provisioning via Portal

### Container Registry → Container App → deploy → smoke test

Siga o **`PORTAL-GUIDE.md`** · eu projeto, vocês replicam.

---

## Duas peças

1. **Container Registry (ACR)** = o **depósito** da imagem Docker
2. **Container App** = quem **roda** a imagem e dá URL pública

```
Dockerfile → az acr build → [ACR: gateway:v1] → [Container App] → URL pública
```

<small>Você publica no Registry; o App puxa de lá.</small>

---

## Os steps do Portal

1. **Container Registry** `acrfifa2026<iniciais><rand>` (Basic, Admin user)
2. **Publicar imagem** `gateway:v1` (`az acr build`)
3. **Container App** `gateway-<iniciais>` · **Target port 8080** ⚠️
4. **Env vars:** `FunctionAppF1Url`, `Gateway__FrontendOrigin`, `Jwt__TenantId`
5. **Smoke test:** `POST /purchase` → 202 + `X-Correlation-ID`

<small>Gateway é **per-aluno** — não há recurso compartilhado (ADE-004 Inv 5).</small>

---

## ⚠️ Armadilhas do Bloco 2

```
Target port ≠ 8080         → 502 em TUDO
FunctionAppF1Url ausente   → 502 só em /purchase (cai no localhost:7071)
Gateway:FrontendOrigin     → na env é Gateway__FrontendOrigin (duplo _)
```

<small>O `Dockerfile` expõe a porta **8080**. A connection string do SQL **NÃO** vai no gateway.</small>

---

## Bloco 3 — Routing + CORS + Transform

### live coding

---

## Routes + Clusters (appsettings.json)

```jsonc
"Routes": {
  "purchase-post": {
    "ClusterId": "functions-f1",
    "Match": { "Path": "/purchase", "Methods": ["POST"] },
    "Transforms": [ { "PathSet": "/api/v2/purchase" } ]
  },
  "purchase-get": {
    "Match": { "Path": "/purchase/{correlationId}", "Methods": ["GET"] },
    "Transforms": [ { "PathPattern": "/api/v2/purchase/{correlationId}" } ]
  }
}
```

<small>Path rewrite (AC-4): cliente usa `/purchase`; a Function recebe `/api/v2/purchase`.</small>

---

## A URL real vem da env (nunca hardcoded)

```csharp
// FunctionDestinationConfigFilter : IProxyConfigFilter
_functionAppF1Url = configuration["FunctionAppF1Url"];
// sobrescreve a Address da destination 'f1' em produção
kvp.Value with { Address = _functionAppF1Url }
```

- `appsettings.json` → `localhost:7071` (dev)
- Produção → env `FunctionAppF1Url` sobrescreve

<small>URL real fora do repo (ADE-003 Inv 3). SQL connection string fica nas Functions.</small>

---

## CORS restrito ao front (AC-7)

```csharp
var frontendOrigin = builder.Configuration["Gateway:FrontendOrigin"]
    ?? "https://fifa2026-web.azurewebsites.net";

builder.Services.AddCors(o =>
    o.AddPolicy("frontend", p =>
        p.WithOrigins(frontendOrigin)
         .AllowAnyMethod().AllowAnyHeader()));
// ...
app.UseCors("frontend");
```

<small>Origin **restrito** — não é `AllowAnyOrigin`. Paridade com a policy `cors` do APIM.</small>

---

## O transform do X-Correlation-ID (AC-8)

```csharp
.AddTransforms(ctx => ctx.AddRequestTransform(tc =>
{
    var incoming = tc.HttpContext.Request.Headers["X-Correlation-ID"].ToString();
    var correlationId = string.IsNullOrWhiteSpace(incoming)
        ? Guid.NewGuid().ToString()    // gera novo se ausente
        : incoming;                     // preserva o que veio

    tc.ProxyRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
    tc.HttpContext.Response.Headers["X-Correlation-ID"] = correlationId; // devolve ao cliente
    return ValueTask.CompletedTask;
}));
```

<small>Programático (gera GUID = exige lógica). O gateway é o **nó zero** do Flow Visualizer (F6).</small>

---

## Demo: routing + correlação

```bash
curl -i -X POST $GATEWAY/purchase \
  -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# → 202  { "correlationId": "...", "status": "queued" }
#   header X-Correlation-ID: <guid>
```

Nos logs da Function: a requisição chegou como **`/api/v2/purchase`** (reescrita).

---

## ☕ Coffee break — 15min

<small>Voltamos pro coração da F2: rate limit, cache e o placeholder de JWT.</small>

---

## Bloco 4 — Rate limit + Cache + JWT

### O coração da fase · live coding

---

## A ordem do pipeline (Program.cs)

```csharp
app.UseCors("frontend");                  // 1
app.UseRateLimiter();                      // 2
app.UseMiddleware<XCacheMiddleware>();     // 2.5 (default MISS)
app.UseOutputCache();                      // 3
app.UseAuthentication();                   // 4
app.UseAuthorization();                    // 5
app.MapReverseProxy()                      // 6
   .RequireRateLimiting("fixed")
   .CacheOutput("purchase-status-30s");
// F3: aplicar .RequireAuthorization()
```

---

## Rate limiting (AC-5)

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("fixed", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});
```

<small>5 req/min por **IP**. 6ª em < 1min → **429**. Paridade com `rate-limit-by-key`.</small>

---

## Demo: o 429 ao vivo (momento "uau")

```bash
for i in $(seq 1 6); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST $GATEWAY/purchase \
    -H "Content-Type: application/json" \
    -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
done
# → 202 202 202 202 202 429
```

<small>Cinco passam, a sexta bate o limite. É o `rate-limit-by-key` do APIM em ~10 linhas de C#.</small>

---

## Output cache (AC-6)

```csharp
builder.Services.AddOutputCache(o =>
    o.AddPolicy("purchase-status-30s", b =>
        b.AddPolicy<XCacheOutputCachePolicy>()
         .Expire(TimeSpan.FromSeconds(30))));
```

- Cache de **30s** no GET de status
- O ASP.NET Core **não diz HIT/MISS** sozinho → middleware extra

---

## O header X-Cache: HIT/MISS

```csharp
// XCacheOutputCachePolicy.ServeFromCacheAsync → HIT (headers ainda graváveis)
context.HttpContext.Response.Headers["X-Cache"] = "HIT";

// XCacheMiddleware.OnStarting → MISS por padrão (se ninguém marcou HIT)
if (!headers.ContainsKey("X-Cache")) headers["X-Cache"] = "MISS";
```

<small>O `OnStarting` evita escrever em headers já commitados pelo YARP — **bug real**, corrigido no dev.</small>

---

## Demo: cache HIT (momento "uau" nº2)

```bash
curl -is $GATEWAY/purchase/<id> | grep -i x-cache   # MISS (vai à Function)
curl -is $GATEWAY/purchase/<id> | grep -i x-cache   # HIT (servido do cache)
```

<small>A segunda resposta nem tocou a Function — veio do cache, em milissegundos.</small>

---

## JWT placeholder (AC-9)

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Entra", options => {
        options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        options.Audience = audience;
        // valida iss/aud/lifetime/assinatura
    });
// MapReverseProxy() SEM .RequireAuthorization() → rotas anônimas em F2
```

> A porta está **instalada e destrancada**. F3 vira a chave.

<small>Paridade com o `<validate-jwt>` desabilitado dentro de um `<choose>` do APIM.</small>

---

## Bloco 5 — CI/CD

### `deploy-phase-02.yml`

```yaml
on: { push: { branches: [phase-02-gateway] } }
# checkout → setup-dotnet 8 → restore → build → test
#   → azure login → acr login → build & push imagem (github.sha)
#   → az containerapp update → smoke test (curl + jq .correlationId + grep X-Correlation-ID)
```

- Trigger por **branch** + paths
- Smoke test valida `.correlationId` E o header `X-Correlation-ID`

---

## A lição da F1 no CI (H-1)

```yaml
- name: Test
  # SEM --no-build: o dotnet test faz restore+build do projeto
  # de testes E da dependência Fifa2026.V2.Gateway.
  run: dotnet test src/Fifa2026.V2.Gateway.Tests -c Release
```

<small>Na S2.1 o `--no-build` quebrou o gate. Lição aplicada aqui.</small>

---

## Branching cumulativo (ADE-000 Inv 7)

```
phase-01-servicebus-functions → phase-02-gateway → phase-03-...
```

- Cada fase é um **branch na linha do tempo** (não feature branch paralela)
- Hotfix em fase antiga = cherry-pick para as seguintes

---

## Bloco 6 — Retro & DoD

### Você completou a F2 se...

- ✅ Registry + Container App do gateway no ar (per-aluno)
- ✅ `/purchase` roteado + reescrito para `/api/v2/purchase`
- ✅ 6ª chamada em < 1min → **429**
- ✅ 2ª GET idêntica → **X-Cache: HIT**
- ✅ CORS restrito ao front
- ✅ `X-Correlation-ID` injetado + devolvido
- ✅ JWT configurado, rotas anônimas (placeholder F3)
- ✅ workflow do branch + smoke test

---

## Observabilidade de borda (Nível 4 → semente F6)

| Hop | Como o correlationId viaja |
|---|---|
| **Gateway** | injeta `X-Correlation-ID` (GUID se ausente) ← **nó zero** |
| HTTP downstream | header repassado à Function |
| Service Bus | corpo da mensagem (da F1) |
| SQL | coluna `correlation_id` (da F1) |

<small>O gateway é o **primeiro nó** do Flow Visualizer da F6.</small>

---

## Carry-over → F3

Hoje: a porta de identidade está **instalada e destrancada**.

**F3 — Identidade com Entra ID:**
- liga o `.RequireAuthorization()` (rotas deixam de ser anônimas)
- App Registration + MSAL / Easy Auth
- o gateway valida o token Entra e propaga o claim `oid`
- `X-Correlation-ID` + identidade viajam juntos

<small>O gateway que vocês construíram hoje é onde a validação do token vai acontecer.</small>

---

# Obrigado!

## Dúvidas?

Próxima leitura: **F3 — Identidade com Entra ID**

`phase-02-gateway` → `phase-03-identity`
