# F2 — Gateway Profissional em Código com YARP (.NET)

> **Leitura prévia obrigatória** · Workshop "Living Lab Azure-Native" (40h) · Fase 2 de 6
> **Tempo estimado de leitura:** 25-35 min · **Faça ANTES da aula.**
> **Story:** [2.2](../../stories/2.2.story.md) · **Decisão de arquitetura:** [ADE-004](../../architecture/ade-004-gateway-yarp.md) (Invariantes 1-5)
> **Continuidade:** parte cumulativa da [F1](../phase-01/README.md) — o gateway entra **na frente** das Functions que você construiu na Fase 1.

---

## 0. Por que você está lendo isto antes da aula

No fim da Fase 1, você deixou as Functions abertas: `authLevel: Anonymous`, qualquer um na internet pode chamar `POST /api/v2/purchase` direto. Isso foi **de propósito** — segurança e preocupações de borda não eram o tema da F1. Agora são.

A Fase 2 coloca um **gateway profissional** na frente das suas Functions. Mas atenção ao verbo: em vez de **comprar** um produto gerenciado pronto (Azure API Management, o "APIM"), você vai **construir** o gateway em código C# usando **YARP** (Yet Another Reverse Proxy). A diferença não é estética — é o coração pedagógico desta fase:

> Um API Gateway não é mágica nem caixa-preta. Rate limiting, cache, transformação de headers e validação de JWT são **mecanismos de código** que você consegue ler, escrever e depurar.

Se você chegar na aula entendendo os conceitos desta leitura, as 6 horas de hands-on rendem o dobro: gastaremos o tempo escrevendo o pipeline de middleware e investigando comportamento real (um 429 de verdade, um `X-Cache: HIT` de verdade), não soletrando teoria.

Esta leitura cobre:

1. O que é um **API Gateway** e o padrão **reverse proxy / BFF**
2. **Anatomia do YARP** (routes, clusters, transforms) e do pipeline ASP.NET Core
3. Os **6 níveis de um gateway** (do ponto único de entrada ao meta-aprendizado)
4. A grande decisão da fase: **construir (YARP) vs comprar (APIM)** — custo, controle e provisioning
5. A **tabela de paridade** APIM → YARP (nenhum conceito de gateway se perde)
6. O que vamos construir (arquitetura delta sobre a F1) e os **contratos exatos**
7. Glossário e checklist de pré-aula

> **Pré-requisitos de conhecimento:** você fez a F1 (ou entende o fluxo `POST /api/v2/purchase` → fila → consumer → SQL). Você programa em qualquer linguagem; não exigimos experiência prévia com .NET nem com reverse proxies. Se você já configurou Nginx, HAProxy, Traefik ou um Ingress do Kubernetes, ótimo — vai reconhecer os conceitos com nomes diferentes.

---

## 1. O que é um API Gateway (e o padrão reverse proxy / BFF)

### 1.1 O problema: o cliente falando direto com o backend

No fim da F1, a topologia é esta:

```
[Browser] ──POST /api/v2/purchase──> [Function App F1 (Anonymous)]
```

O navegador conhece e chama **a URL real da Function**. Isso traz problemas que só pioram conforme o sistema cresce:

- **Acoplamento de topologia:** o cliente sabe o endereço interno do backend. Se você mudar a Function de lugar, renomear, ou colocar duas atrás de um balanceador, **todo cliente quebra**.
- **Sem ponto central de controle:** onde você aplica rate limiting? CORS? Validação de token? Em cada Function, repetido? E quando tiver 10 microsserviços?
- **Superfície exposta:** a internet inteira fala direto com o seu backend, sem nenhuma camada de defesa ou observabilidade na borda.

### 1.2 A solução: um reverse proxy na frente

Um **reverse proxy** é um servidor que fica **na frente** dos seus backends e encaminha as requisições para eles. O cliente fala com o proxy; o proxy fala com o backend. O cliente **nunca vê a URL real** do backend.

```
[Browser] ──POST /purchase──> [Gateway YARP] ──POST /api/v2/purchase──> [Function App F1]
            (URL pública,        (reescreve o caminho,                   (URL interna,
             genérica)            aplica regras de borda)                 escondida)
```

> **Proxy vs reverse proxy (não confunda):** um *proxy* (forward) representa o **cliente** (ex.: a saída de internet de uma empresa). Um *reverse proxy* representa o **servidor**: o cliente nem sabe que ele existe, acha que está falando com a aplicação. Gateways são reverse proxies.

### 1.3 API Gateway e BFF

Quando esse reverse proxy ganha **inteligência de borda** — rate limiting, autenticação, cache, transformação, roteamento por regra — ele vira um **API Gateway**: o **ponto único de entrada** de um sistema, onde as **preocupações transversais** (cross-cutting concerns) vivem **uma vez só**, em vez de espalhadas por cada serviço.

Um parente próximo é o **BFF (Backend For Frontend)**: um gateway moldado para um cliente específico (web, mobile), que agrega/adapta respostas para aquele front. Nosso gateway de F2 é um API Gateway simples; ele tem traços de BFF (CORS amarrado ao domínio do frontend), e nas próximas fases pode evoluir nessa direção.

> **A intuição que importa:** o gateway é a **portaria do prédio**. Toda visita passa por ela: identifica-se (JWT, em F3), respeita as regras da casa (rate limit, CORS), recebe um crachá de rastreio (`X-Correlation-ID`) e só então é encaminhada ao apartamento certo (a Function). Os apartamentos não precisam ter cada um sua própria portaria.

---

## 2. Anatomia do YARP e do pipeline ASP.NET Core

**YARP** (Yet Another Reverse Proxy) é uma biblioteca **open-source da Microsoft** para construir reverse proxies **em código .NET**, rodando como uma aplicação **ASP.NET Core**. Em vez de configurar um produto, você adiciona um pacote NuGet (`Yarp.ReverseProxy`) e descreve o comportamento em `appsettings.json` e/ou em C#.

### 2.1 Os dois conceitos centrais: Routes e Clusters

```
ReverseProxy
 ├── Routes      ← "QUANDO chega uma requisição que casa com X, mande para o cluster Y"
 │     ├── purchase-post : Path /purchase, Method POST   → cluster functions-f1
 │     └── purchase-get  : Path /purchase/{correlationId}, GET → cluster functions-f1
 └── Clusters    ← "ONDE estão os backends de destino"
       └── functions-f1
             └── Destinations: { f1: { Address: http://localhost:7071/ } }
```

- **Route** = uma regra de correspondência (match por path, método, host, headers) + para qual cluster enviar + quais **transforms** aplicar.
- **Cluster** = um grupo de uma ou mais **destinations** (URLs de backend). Com mais de uma destination, o YARP faz **load balancing** (assunto do Nível 3).

No nosso `appsettings.json` real (você vai ler em sala), a destination de dev aponta para `http://localhost:7071/` (a Function rodando localmente). Em produção, a URL real da Function F1 do aluno entra via a variável de ambiente **`FunctionAppF1Url`** — nunca hardcoded no repo. Mais sobre isso na seção 6.

### 2.2 Transforms: reescrevendo a requisição no caminho

Um **transform** modifica a requisição (ou a resposta) enquanto ela atravessa o gateway. No nosso gateway há dois tipos:

- **Declarativos** (no `appsettings.json`, por route): `PathSet` e `PathPattern` reescrevem o caminho. `/purchase` vira `/api/v2/purchase`; `/purchase/{correlationId}` vira `/api/v2/purchase/{correlationId}`. É assim que o cliente usa uma URL limpa enquanto a Function mantém sua rota interna.
- **Programáticos** (em C#, no `Program.cs`): `AddRequestTransform` injeta o header `X-Correlation-ID` — algo que exige lógica (gerar um GUID novo se ele não veio), impossível de fazer só com config declarativa.

### 2.3 O pipeline de middleware do ASP.NET Core (a ordem importa!)

Aqui está o conceito mais importante da fase do ponto de vista de código. Uma aplicação ASP.NET Core processa cada requisição passando por uma **pipeline de middlewares**, **em ordem**, como uma linha de montagem. Cada middleware pode agir na requisição, passar adiante, e agir na resposta na volta.

O nosso gateway monta a pipeline **nesta ordem exata** (e a ordem **não é negociável**):

```
requisição
   │
   ▼
1. UseCors            ── valida a origem (é o frontend permitido?)
2. UseRateLimiter     ── conta as chamadas; 6ª em < 1min → 429 (nem chega ao backend)
3. XCacheMiddleware   ── marca X-Cache: MISS por padrão
4. UseOutputCache     ── se houver resposta cacheada, devolve (e marca X-Cache: HIT)
5. UseAuthentication  ── valida o JWT (em F2: configurado mas rotas anônimas)
6. UseAuthorization   ── aplica políticas de autorização (em F2: nenhuma exigida)
   │
   ▼
7. MapReverseProxy    ── encaminha para a Function F1 (com path rewrite + X-Correlation-ID)
```

> **Por que a ordem importa?** Pense no custo de cada coisa. Rate limiting **antes** do proxy garante que uma chamada abusiva é barrada **antes** de gastar recursos do backend — se você colocasse o rate limit depois do proxy, já teria pago o custo da chamada. CORS bem no início rejeita origens proibidas logo. Auth antes do proxy garante que nenhuma requisição não autenticada (quando F3 ligar isso) chega ao backend. **Errar a ordem = comportamento sutilmente quebrado** (ex.: cachear uma resposta de erro, ou rate-limitar depois de já ter chamado o backend). É a armadilha nº1 da fase.

---

## 3. Os 6 níveis de um gateway

Esta é a espinha dorsal didática da F2. Pense em um gateway como uma escada de capacidades — cada degrau adiciona valor. Vamos construir os níveis 0-2 em código nesta fase e tocar nos níveis 3-5 conceitualmente.

| Nível | Conceito | O que você aprende na prática (no nosso código) |
|---|---|---|
| **0 — Ponto único de entrada** | Reverse proxy, padrão API Gateway / BFF | O cliente chama `/purchase` no gateway e **nunca vê** a URL real da Function. Desacoplamento de topologia. |
| **1 — Roteamento** | Path/host-based routing | `Routes` + `Clusters` do YARP no `appsettings.json`. `/purchase` (POST) e `/purchase/{correlationId}` (GET) roteados para o cluster `functions-f1`, com **path rewrite** para `/api/v2/purchase`. |
| **2 — Preocupações transversais (o coração)** | Rate limiting, CORS, header transform, output cache, JWT | `AddRateLimiter` (→ 429), `AddCors`, `AddRequestTransform` (`X-Correlation-ID`), `AddOutputCache` (`X-Cache: HIT`), `AddJwtBearer` (placeholder anônimo) — **tudo em C# legível**. |
| **3 — Resiliência** | Load balancing, health checks, timeouts, retries | Clusters YARP com múltiplas destinations e health probes ativo/passivo. **Conceitual em F2** (nosso cluster tem 1 destination); a mecânica é a mesma. |
| **4 — Observabilidade de borda** | Logging/tracing centralizado | O gateway injeta o `X-Correlation-ID` e devolve ele ao cliente. É o **primeiro nó** (nó zero) do Flow Visualizer da F6; o App Insights captura o trace de borda. |
| **5 — Meta-aprendizado** | Trade-off "comprar gerenciado vs construir" | A grande discussão: APIM (gerenciado, ~US$50-80, 30-45min para provisionar) vs YARP (código, ~US$0, segundos). "Em produção corporativa, o equivalente gerenciado é o APIM." (seção 4) |

> **Onde a aula gasta tempo:** o Nível 2 é o coração. Cada uma das 5 capacidades transversais tem paridade 1:1 com uma policy que o APIM entregaria — e você vai escrever cada uma em C#. Os Níveis 0 e 1 são a fundação (rápidos); os 3-5 são discussão e visão de futuro.

---

## 4. A grande decisão: construir (YARP) vs comprar (APIM)

Esta é a pergunta de engenharia mais valiosa da fase, e ela aparece o tempo todo na vida real: **quando vale construir algo, e quando vale comprar/usar um produto gerenciado?**

O blueprint original do workshop pedia **Azure API Management (APIM) Developer tier** — um produto gerenciado robusto, padrão de mercado corporativo. A decisão foi **trocar por YARP em código**. Não porque APIM seja ruim — ele é excelente em seu lugar — mas porque, **para este workshop**, construir ensina mais. Veja o trade-off honesto (registrado na [ADE-004](../../architecture/ade-004-gateway-yarp.md)):

| Eixo | **APIM (comprar/gerenciado)** | **YARP (construir/código)** |
|---|---|---|
| **Custo** | ~US$50-80 (Developer tier, pro-rata) | **~US$0** (open-source no Container App Consumption, scale-to-zero) |
| **Provisioning** | **30-45 min** para subir uma instância | **Segundos a poucos minutos** (deploy de um container) |
| **Configuração** | Policies em **XML proprietário** + portal gerenciado | **Código C#** que você lê, escreve e versiona no repo |
| **Transparência** | Caixa-preta: o "como funciona por dentro" fica escondido | Você **vê o mecanismo**: o rate limiter, o cache, o transform são código seu |
| **Recursos de produto** | Developer portal, test-console, analytics nativos, gestão de subscriptions/keys | Não tem (usa `curl`/Postman + logs + App Insights) |
| **Operação** | Gerenciado pela Microsoft (menos para você manter) | É código seu (mais para manter, mas é didático e simples) |
| **Quando ganha** | Empresa quer um produto gerenciado, portal para parceiros, governança de APIs em escala | Time quer custo zero, controle total, transparência, e o gateway dentro do mesmo stack .NET |

### A nota didática (importante guardar)

> **Em produção corporativa, o equivalente gerenciado deste gateway é o APIM.** Aqui escolhemos construir em código por uma razão pedagógica: para você **enxergar o que um gateway faz por dentro**. Um produto gerenciado é a escolha certa em muitos cenários reais (governança em escala, portal de parceiros, menos operação) — mas ele esconde os mecanismos atrás de policies XML e de um portal. Ao construir com YARP, você aprende o **conceito** de gateway, não a operação de um produto específico — e esse conceito você leva para qualquer ferramenta (APIM, Kong, Apigee, AWS API Gateway).

Não é "YARP é melhor que APIM". É "para aprender, construir > comprar; para muitas produções, comprar > construir". Saber **decidir entre os dois** é o que esta fase quer te dar.

---

## 5. Tabela de paridade APIM → YARP (nenhum conceito se perde)

Cada capacidade que uma policy do APIM entregaria tem um equivalente direto em código YARP/ASP.NET Core. Esta tabela é o **contrato de paridade** da fase ([ADE-004 Invariante 3](../../architecture/ade-004-gateway-yarp.md)) — você sai sabendo exatamente o que cada policy faz, só que escrito em C#:

| Capacidade (policy APIM) | Equivalente no nosso gateway (código real) | AC |
|---|---|---|
| `rate-limit-by-key` | `AddRateLimiter` — fixed window, **5 req/min por IP**, 6ª → **HTTP 429** | AC-5 |
| `cache-lookup` / `cache-store` | `AddOutputCache` — policy `purchase-status-30s` (30s) + header `X-Cache: HIT/MISS` | AC-6 |
| `cors` | `AddCors` + `UseCors` — origin restrito a `https://fifa2026-web.azurewebsites.net` | AC-7 |
| `set-header X-Correlation-ID` | YARP `AddRequestTransform` — injeta `X-Correlation-ID` (GUID novo se ausente) | AC-8 |
| `rewrite-uri` / path strip | YARP `PathSet` / `PathPattern` — `/purchase` → `/api/v2/purchase` | AC-4 |
| `validate-jwt` (placeholder) | `AddJwtBearer("Entra", ...)` configurado, **rotas anônimas em F2** (F3 ativa) | AC-9 |

> **O placeholder de JWT (AC-9) merece atenção.** No APIM, o equivalente seria uma policy `<validate-jwt>` deixada **desabilitada** dentro de um `<choose>` — preparada, mas inativa nesta fase. No nosso código fazemos exatamente o mesmo: `AddJwtBearer` está **configurado** (apontando para o issuer Entra), mas as rotas continuam **anônimas** — não há `.RequireAuthorization()`. Há um comentário literal no código: `// F3: aplicar .RequireAuthorization()`. Em F2 a porta está instalada, mas destrancada. **F3 vira a chave.** Essa paridade de faseamento é proposital ([ADE-004 Inv 4](../../architecture/ade-004-gateway-yarp.md)).

---

## 6. O que vamos construir (arquitetura delta sobre a F1)

### 6.1 Diagrama

```
[Browser]
   │ POST /purchase   (URL pública do SEU Container App de gateway)
   ▼
[Container App: gateway-<iniciais>  (ASP.NET Core + YARP .NET 8)]
   ├─ UseCors("frontend")                 ── origem restrita ao front (AC-7)
   ├─ UseRateLimiter                       ── 429 se > 5/min por IP (AC-5)
   ├─ XCacheMiddleware → UseOutputCache    ── cache 30s no GET + X-Cache HIT/MISS (AC-6)
   ├─ UseAuthentication / UseAuthorization ── JWT placeholder, anônimo em F2 (AC-9)
   ├─ RequestTransform: X-Correlation-ID   ── injetado downstream + devolvido (AC-8)
   └─ MapReverseProxy                       ── reescreve /purchase → /api/v2/purchase
            │                                  e encaminha para a Function F1 (AC-4)
            ▼
     [Function App F1 (da sua Fase 1)]
            └─ PurchaseEntryFunction → (resto do fluxo F1: Service Bus → Consumer → SQL)
```

> **O que NÃO tocamos:** as **Functions da F1 ficam intactas** (continuam aceitando requisições, agora via gateway). A API Node (`fifa2026-api/`), o frontend Vite e o schema SQL **não mudam**. A connection string SQL (`SqlConnectionString`) **permanece nas Functions, não no gateway** — o gateway só conhece a URL da Function (`FunctionAppF1Url`). Isso é o [ADE-003 Invariante 3](../../architecture/ade-003-v2-infrastructure-baseline.md): segredos ficam onde são usados.

### 6.2 Os contratos exatos (decore — você vai testar com `curl`)

O gateway expõe **caminhos limpos** e os reescreve para as rotas internas da Function:

**POST `/purchase`** (no gateway) → reescrito para **`POST /api/v2/purchase`** (na Function):

```jsonc
// Request ao gateway
POST https://gateway-<iniciais>.azurecontainerapps.io/purchase
{ "matchId": 1, "category": "VIP", "userId": 1, "quantity": 1 }

// Response (202 Accepted, vinda da Function F1, devolvida pelo gateway)
{ "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "status": "queued" }
// + header de resposta: X-Correlation-ID: 3fa85f64-...  (devolvido pelo gateway)
// + header de resposta: X-Cache: MISS                    (POST não é cacheado)
```

**GET `/purchase/{correlationId}`** (no gateway) → reescrito para **`GET /api/v2/purchase/{correlationId}`**:

```jsonc
// 1ª chamada: X-Cache: MISS (vai à Function)
// 2ª chamada idêntica em < 30s: X-Cache: HIT (servida do cache, sem tocar a Function)
{ "status": "completed", "ticketId": 42 }
```

> Repare: os contratos de negócio (`correlationId`, `status`) são **os mesmos da F1** — o gateway não muda o que a Function responde. Ele adiciona **comportamento de borda**: reescreve o caminho, aplica rate limit, cacheia o GET, injeta o correlation ID.

### 6.3 Por que Container App (e não Function)?

O gateway é hospedado em um **Azure Container App** (Consumption), um por aluno. Por quê não numa Function, como a F1?

- Um reverse proxy é um workload de **longa duração** (pipeline de middleware, keep-alive, streaming). É o habitat natural de uma aplicação ASP.NET Core em container — não de uma Function, que sofre com cold start no caminho crítico.
- Container Apps **já está no stack** do workshop (a F4 sobe o n8n lá). Não introduz um tipo de recurso novo na sua cabeça.
- Scale-to-zero do Consumption mantém o custo **~US$0** em ociosidade.

(A alternativa "YARP em Function" é aceitável mas não é o default — ver [ADE-004 Inv 2](../../architecture/ade-004-gateway-yarp.md).)

### 6.4 Isolamento: não há mais recurso compartilhado

Na F1, todos os recursos já eram per-aluno. Com a remoção do APIM, **isso continua valendo para o gateway**: cada aluno sobe o seu próprio Container App de gateway no seu Resource Group. Não há instância compartilhada entre a turma. Consequência boa: o gateway de um colega cair **não derruba o workshop inteiro** ([ADE-004 Inv 5](../../architecture/ade-004-gateway-yarp.md)).

---

## 7. Glossário rápido

| Termo | Significado curtíssimo |
|---|---|
| **Reverse proxy** | Servidor que fica na frente dos backends e encaminha requisições; o cliente não vê a URL real. |
| **API Gateway** | Reverse proxy com inteligência de borda (rate limit, auth, cache, transform) — ponto único de entrada. |
| **BFF** | Backend For Frontend: gateway moldado para um cliente específico (web/mobile). |
| **YARP** | Yet Another Reverse Proxy — biblioteca .NET open-source da Microsoft para construir gateways em código. |
| **Route** | Regra YARP: "requisição que casa com X vai para o cluster Y" (+ transforms). |
| **Cluster** | Grupo de destinations (URLs de backend) para onde uma route aponta. |
| **Destination** | Uma URL de backend dentro de um cluster. |
| **Transform** | Modificação da requisição/resposta no caminho (path rewrite, injeção de header). |
| **Middleware** | Estágio da pipeline de processamento do ASP.NET Core; a ordem deles importa. |
| **Cross-cutting concern** | Preocupação transversal (rate limit, CORS, auth) que vale para todas as rotas. |
| **Rate limiting** | Limitar quantas chamadas um cliente pode fazer num período (5/min aqui → 429 ao estourar). |
| **HTTP 429** | "Too Many Requests" — resposta de rate limit atingido. |
| **Output cache** | Cacheia a resposta de um endpoint por um tempo (30s no GET) para responder mais rápido. |
| **X-Cache: HIT/MISS** | Header que indica se a resposta veio do cache (HIT) ou foi gerada agora (MISS). |
| **X-Correlation-ID** | GUID de rastreio injetado pelo gateway e propagado por todas as camadas (semente da F6). |
| **JWT / Bearer token** | Token assinado que prova a identidade do chamador (validado a partir da F3). |
| **Issuer (Entra)** | Quem emite e assina o token (o Entra ID); o gateway valida o token contra ele em F3. |
| **Container App** | Serviço Azure para rodar containers com scale-to-zero (host do gateway). |
| **Cold start** | Latência extra na 1ª chamada após scale-to-zero (esperado no Consumption). |

---

## 8. Checklist antes de entrar na aula

- [ ] Entendi o que é um reverse proxy e por que o cliente não deve ver a URL real do backend (seção 1)
- [ ] Sei a diferença entre **route** e **cluster** no YARP (seção 2.1)
- [ ] Entendi por que a **ordem do pipeline de middleware importa** (seção 2.3) ← conceito-chave
- [ ] Conheço os **6 níveis** do gateway e sei que o Nível 2 é o coração (seção 3)
- [ ] Consigo explicar o trade-off **construir (YARP) vs comprar (APIM)** em custo/controle/provisioning (seção 4)
- [ ] Sei que o **JWT é placeholder em F2** (configurado, mas rotas anônimas; F3 ativa) (seção 5)
- [ ] Entendi que o gateway reescreve `/purchase` → `/api/v2/purchase` e não muda os contratos de negócio da F1 (seção 6.2)
- [ ] Tenho minha Function App da F1 funcionando (o gateway precisa de um backend para encaminhar) e login em portal.azure.com

Nos vemos na aula. Próximo artefato que você vai usar: [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md), no Bloco 2 (Container Registry + Container App).
