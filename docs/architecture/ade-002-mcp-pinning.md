# ADE-002 — Pinning de versões: MCP C# SDK (F5), integração LLM (F5) e tag n8n (F4)

> **Tipo:** Architecture Decision Entry (technology selection + version pinning)
> **Status:** ✅ Accepted
> **Date:** 2026-06-06
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 F5 (`phase-05-ai-mcp` — `src/Fifa2026.V2.McpServer/` + chatbot React + LLM) e F4 (`phase-04-orchestration` — n8n self-hosted em Container Apps)
> **Supersedes:** Resolve a "DECISAO PENDENTE @architect — pinning de versão do MCP SDK" das stories 2.4 e 2.5 (slot ADE-002 reservado em ADE-005 linha 188)
> **Related:** ADE-000 (microsserviço paralelo), ADE-003 (baseline PaaS + Container Apps), ADE-004 (gateway YARP — todo tráfego ao MCP passa pelo gateway), ADE-005 (identidade `oid` propagada como `X-Entra-OID` ao MCP Server)

---

## Context

Esta ADE fecha a única decisão arquitetural que as stories 2.4 e 2.5 (Ready) deixaram explicitamente pendente para o @architect: **a fixação (pinning) de versões de componentes recentes/voláteis** que a squad usará na implementação de F4 e F5. As stories foram modeladas corretamente como gate sequencial (2.5 Task 2 BLOQUEANTE → Task 3); esta ADE remove o bloqueio.

Para os **pacotes .NET / SDK** (artefatos versionados em `.csproj`), `latest`/floating em tecnologia recente é fonte direta de quebra ao vivo num workshop, então a decisão de versão deve ser **real e verificável** (Art. IV — No Invention) e **pinada exata**. Para o **container n8n**, o owner decidiu o oposto (ver Invariante 4): rodar sempre `latest` para que os alunos usem a versão mais nova do n8n — exceção explícita e aprovada ao princípio de pinning, com mitigação documentada.

Três artefatos exigem decisão de versão/tag:

1. **MCP C# SDK** (`src/Fifa2026.V2.McpServer/`, Story 2.5 AC-2/AC-6, Task 3): o protocolo MCP é recente e o SDK .NET evoluiu rápido de preview (`0.x`) para GA. Precisamos do pacote oficial e da versão estável exata. **→ pin exato (Inv 1).**
2. **Integração LLM (Gemini 2.0 Flash + Groq + Mistral)** (Story 2.5 AC-8/AC-10, Task 5): definir **onde** a integração LLM vive (frontend React vs McpServer .NET) e qual mecanismo/endpoint cada provider usa — para não inventar parâmetros de API. **→ endpoint/modelo pinados (Inv 3).**
3. **Tag do container n8n** (`n8nio/n8n`, Story 2.4 AC-3/Task 4.2): **decisão de owner (2026-06-06)** — usar `latest` para que a turma sempre rode a versão mais nova. **→ `n8nio/n8n:latest` (Inv 4, exceção aprovada ao pinning).**

A decisão abaixo foi tomada com pesquisa nas fontes oficiais (NuGet, GitHub releases do csharp-sdk, ai.google.dev, Docker Hub / GitHub releases do n8n) em 2026-06-06. Todas as versões citadas têm fonte/URL.

---

## Decision

Adotamos **pinning exato (sem `latest`, sem floating) para os pacotes/SDK .NET**, com **uma exceção explícita aprovada pelo owner (2026-06-06): o container n8n usa `latest`** (Invariante 4). São 5 invariantes.

### Invariante 1: MCP Server usa o SDK C# oficial `ModelContextProtocol`, pinado em **1.4.0**

O `src/Fifa2026.V2.McpServer/` referencia os pacotes oficiais do SDK C# do Model Context Protocol (repo `modelcontextprotocol/csharp-sdk`, mantido em colaboração com a Microsoft), pinados em **versão exata 1.4.0** — a última estável (GA) publicada, de 2026-06-04. Os três pacotes do SDK estão alinhados na mesma versão 1.4.0 e todos têm como target **.NET 8.0** (compatível .NET 8/9/10), batendo com o padrão `.NET 8 isolated` já usado em `src/Fifa2026.V2.Functions/`.

| Pacote NuGet | Versão pinada | Papel | Fonte |
|---|---|---|---|
| `ModelContextProtocol` | `1.4.0` | Pacote principal (hosting + DI extensions) | https://www.nuget.org/packages/ModelContextProtocol/1.4.0 |
| `ModelContextProtocol.AspNetCore` | `1.4.0` | **Server HTTP** — expõe o endpoint MCP via `MapMcp()` (Streamable HTTP). Depende de `ModelContextProtocol (>= 1.4.0)` | https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/1.4.0 |
| `ModelContextProtocol.Core` | `1.4.0` | Núcleo client/server low-level (dependência transitiva; pinar se referenciado direto) | https://www.nuget.org/packages/ModelContextProtocol.Core/1.4.0 |

**Histórico de maturidade (justifica a confiança no pin):** o SDK saiu de preview com `v1.0` em 2026-02-25 e seguiu em GA (1.x) até `1.4.0` (2026-06-04). Não é mais preview — é uma linha estável `1.x` com cadência de patches. Fonte: https://github.com/modelcontextprotocol/csharp-sdk/releases e https://csharp.sdk.modelcontextprotocol.io/.

**Como referenciar (contrato para o @dev — `.csproj` do McpServer):**

```xml
<ItemGroup>
  <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.4.0" />
  <!-- ModelContextProtocol e ModelContextProtocol.Core entram transitivamente como 1.4.0;
       referenciar explicitamente (também em 1.4.0) só se usar APIs diretas. -->
</ItemGroup>
```

> **Regra de pin (NON-NEGOTIABLE para esta fase):** `Version="1.4.0"` é **exato**. Proibido `Version="1.*"`, `1.4.*`, `[1.4.0,)` ou ausência de versão. Justificativa: SDK recente em cadência rápida — um minor inesperado pode mudar a assinatura de `tools/list`/`tools/call` ou de `MapMcp()` no meio do workshop. O upgrade é decisão consciente (ver Invariante 5), não automática.

### Invariante 2: O endpoint MCP é HTTP (Streamable HTTP via `ModelContextProtocol.AspNetCore`) — coerente com hosting do epic e com o gateway YARP

A Story 2.5 (AC-2) pede um endpoint **`POST /mcp` JSON-RPC 2.0** acessível **via o gateway YARP** (AC-8/AC-9, não direto do browser). Isso casa com o transport **Streamable HTTP** do `ModelContextProtocol.AspNetCore`, exposto por `MapMcp()` (registra o endpoint na raiz configurada; em modo stateful também mapeia GET/DELETE para streaming/cleanup). As 3 tools (`consultar_disponibilidade`, `verificar_ingresso`, `consultar_bracket`) são declaradas com `[McpServerTool]` + JSON Schema de input, e o dispatch `tools/list`/`tools/call` é provido pelo SDK — o @dev **não implementa o framing JSON-RPC à mão** (reduz superfície de invenção, AC-15).

Hosting: ASP.NET Core (host do `ModelContextProtocol.AspNetCore`) em **Azure Container Apps** ou Function .NET isolated com integração ASP.NET Core — mesma decisão de host já discutida em ADE-004 Inv 2 para o gateway. Como o McpServer é um servidor HTTP de longa duração que serve streaming, **Container App é o host recomendado** (mesma justificativa do YARP); Function isolated permanece alternativa aceitável se a turma priorizar uniformidade em Functions. A escolha final de host do McpServer é ponto de design da Story 2.5 Task 3 — **não bloqueia** este pinning.

Acesso de dados segue o padrão do projeto: Dapper + repositórios análogos a `src/Fifa2026.V2.Functions/Data/` (Story 2.5 Dev Notes). O McpServer lê `X-Entra-OID` do header propagado pelo gateway (ADE-005 Inv 4) para logging/personalização — **nunca revalida o JWT** (o gateway é o guardião).

### Invariante 3: A integração LLM vive no **frontend React** (não no McpServer .NET); cada provider tem endpoint oficial pinado por URL/modelo, não por SDK .NET

Ponto de arquitetura que evita confusão de escopo: o **chatbot e a orquestração LLM↔tools são do frontend React** (`Lovable/World Cup Tickets Hub/src/`, Story 2.5 AC-7/Task 4/Task 5). O `src/Fifa2026.V2.McpServer/` **não** chama o LLM — ele apenas expõe as tools MCP. O LLM (no browser) recebe a lista de tools, decide chamar uma tool, e a chamada é roteada **via gateway YARP** ao `/mcp` (AC-8). Logo, **não há "Gemini SDK .NET" a pinar no McpServer**.

A portabilidade entre providers (AC-10) é feita por `LLM_PROVIDER=gemini|groq|mistral`. Cada provider é "pinado" pela combinação **endpoint oficial + nome de modelo** (não há lock de versão de SDK no browser; o que precisa ser real e verificável é o endpoint/modelo, Art. IV):

| Provider | Base URL oficial | Endpoint/modelo de referência | Fonte oficial |
|---|---|---|---|
| `gemini` (default) | `https://generativelanguage.googleapis.com/v1beta` | `models/gemini-2.0-flash:generateContent`; function calling via `tools` + `tool_config.function_calling_config.mode` | https://ai.google.dev/api/generate-content · https://ai.google.dev/gemini-api/docs |
| `groq` | `https://api.groq.com/openai/v1` | `chat/completions` (OpenAI-compatible; `tools`/`tool_calls`) | https://console.groq.com/docs |
| `mistral` | `https://api.mistral.ai/v1` | `chat/completions` (`tools`/`tool_calls`) | https://docs.mistral.ai/ |

> **Anti-hallucination (AC-15):** o @dev confirma os parâmetros exatos de cada API contra a doc oficial **no momento da implementação** (Task 9). A API version do Gemini é **`v1beta`** (a versão que expõe function calling para os modelos 2.x), confirmada na doc oficial — ver https://ai.google.dev/gemini-api/docs/api-versions. Não inventar campos.
>
> **Nota sobre SDK .NET do Gemini (caso a turma queira chamar o LLM do backend no futuro):** existe a biblioteca `Google.GenAI` para .NET, porém em estágio inicial/preview e **fora do caminho recomendado** desta fase (a integração é no front). Se algum aluno optar por orquestrar o LLM no backend, o caminho de menor invenção é **HTTP REST direto** ao endpoint oficial acima (sem SDK), pinando apenas o `gemini-2.0-flash` por nome. Registrado como nota, não como decisão de pin.

### Invariante 4: O container n8n usa a tag **`latest`** — DECISÃO DE OWNER (2026-06-06), exceção explícita ao pinning

> **DECISÃO DE OWNER (2026-06-06):** o container n8n usa **`n8nio/n8n:latest`**. O owner quer que os alunos rodem **sempre a versão mais nova** do n8n. Esta decisão **sobrepõe explicitamente** a restrição do blueprint (Story 2.4 AC-3 dizia "NÃO usar `latest`") e a postura geral de pinning desta ADE. É a **única exceção aprovada** ao princípio "proibido floating" — que permanece em vigor para todos os pacotes/SDK .NET (ver Invariante 5).

A Story 2.4 (AC-3, Task 4.2) sobe `n8nio/n8n`. Decisão: usar a **tag flutuante `n8nio/n8n:latest`**, que aponta sempre para a release mais recente publicada no Docker Hub.

```text
Image: n8nio/n8n:latest      ← decisão de owner (sempre a versão mais nova)
```

**Por que `latest` (decisão de owner):** o objetivo pedagógico do owner é que cada turma experimente o n8n na sua versão mais atual, com os nodes/UI mais recentes, sem ter de revisar um pin a cada nova release. O custo aceito (ver trade-off abaixo) é abrir mão da reprodutibilidade exata entre aulas.

**Trade-off aceito (honesto):** `latest` significa que **a reprodutibilidade entre aulas não é garantida** — duas turmas em datas diferentes podem rodar versões diferentes do n8n. Soma-se a isso o fato de o n8n já ter tido **breaking change em major (2.0** — https://docs.n8n.io/2-0-breaking-changes/), então uma futura major pode mudar comportamento de UI/nodes. **Mitigação aprovada:** (a) validar o workflow `post-purchase-notification` (4 nodes) no **início de cada aula** antes da demo ao vivo; (b) cada aula é **gravada com a versão do dia**, de modo que o material reflete exatamente o que rodou. O escopo do workshop é curto e a demo é resiliente a variação de versão de UI.

**Fontes:** https://hub.docker.com/r/n8nio/n8n/tags · https://github.com/n8n-io/n8n/releases · https://docs.n8n.io/release-notes/ · https://docs.n8n.io/2-0-breaking-changes/.

> **Validação obrigatória no momento da implementação (Story 2.4 Task 10.2):** no **início de cada aula**, subir/atualizar o container para `n8nio/n8n:latest` e **validar o workflow `post-purchase-notification`** (4 nodes) end-to-end antes da demo, dado que `latest` pode ter avançado desde a aula anterior. As env vars permanecem as de AC-3 (rastreadas a https://docs.n8n.io/hosting/environment-variables/).

### Invariante 5: Pinning dos pacotes/SDK .NET é decisão consciente — upgrade só por nova ADE/nota, nunca automático (n8n é exceção do owner)

Para os **pacotes/SDK .NET** (MCP SDK e quaisquer `PackageReference` do epic):

- **Proibido floating** (`latest`, ranges abertos, wildcards `1.*`/`1.4.*`, `[1.4.0,)`, ausência de versão) em **qualquer artefato .NET versionado do epic** (`.csproj`). **Escopo do "proibido floating" = pacotes/SDK .NET.** O container n8n é **exceção explícita aprovada pelo owner** (Invariante 4 → `latest`); não há outras exceções.
- **Upgrade do MCP SDK** (ex.: 1.4.0 → 1.5.0 ou eventual 2.0) só após avaliar changelog e validar `tools/list`/`tools/call`/`MapMcp()` — registrar em nota de upgrade nos Dev Notes da story (ou nova ADE se houver breaking change estrutural). O SDK está em cadência rápida; assumir que minors podem trazer mudança relevante.
- **n8n (exceção):** roda `latest` por decisão de owner (Invariante 4). Não há "upgrade consciente" a registrar — a versão avança sozinha; o controle é a **revalidação do workflow `post-purchase-notification` (4 nodes) no início de cada aula** (n8n já demonstrou breaking change em major — 2.0).
- **Plano de upgrade padrão:** congelar **as versões .NET** durante todo o EPIC-002 (reavaliar só no encerramento do epic ou em CVE crítico); o **n8n acompanha `latest`** continuamente, conforme decisão de owner.

---

## Rationale

### Por que SDK oficial `ModelContextProtocol` 1.4.0 (vs alternativas)?

- **Oficial + mantido pela Microsoft:** é o SDK canônico do MCP em .NET (repo `modelcontextprotocol/csharp-sdk`). Implementar JSON-RPC 2.0 à mão multiplicaria a superfície de invenção (AC-15) e o risco de divergir da spec `modelcontextprotocol.io`. O SDK entrega `tools/list`/`tools/call` e o transport Streamable HTTP prontos.
- **Saiu de preview (GA desde 2026-02-25):** 1.4.0 é estável, não `0.x`. O risco de "SDK recente" é mitigado por (a) GA, (b) pin exato, (c) plano de upgrade consciente.
- **.NET 8.0 target:** bate exatamente com `src/Fifa2026.V2.Functions/` (.NET 8 isolated) — zero atrito de runtime, reforça o fio condutor "microsserviço .NET".
- **Linha alinhada em 1.4.0:** os três pacotes (`*`, `.AspNetCore`, `.Core`) publicados juntos em 1.4.0 (2026-06-04) — sem mismatch de dependência.

### Por que integração LLM no front (vs no McpServer .NET)?

- **Fidelidade ao desenho da story:** chatbot é React (AC-7); o McpServer só expõe tools (AC-2). MCP **desacopla** o LLM dos dados — o LLM é cliente das tools, não parte do servidor MCP. Colocar o LLM no front mantém esse desacoplamento (e é o que permite a demo de portabilidade Gemini→Groq→Mistral trocando só uma env var).
- **Menos invenção:** sem SDK .NET de LLM imaturo no caminho crítico; o front fala REST direto com endpoints oficiais documentados.
- **Tudo passa pelo gateway:** as tool calls vão do front → gateway YARP (Bearer Entra) → `/mcp` (ADE-004/ADE-005), preservando o gateway como ponto único de auth — coerência pedagógica.

### Por que tag `latest` no n8n (decisão de owner, 2026-06-06)?

- **Decisão do owner:** o owner determinou que os alunos rodem **sempre a versão mais nova** do n8n. Isso sobrepõe a recomendação técnica anterior (pin exato) e a restrição original do blueprint (que pedia "não usar `latest`").
- **Sempre na versão atual:** a turma experimenta os nodes/UI mais recentes sem precisar manter um pin atualizado a cada release.
- **Trade-off assumido conscientemente:** abre-se mão da reprodutibilidade exata entre aulas; mitigado por validação do workflow no início de cada aula e pela gravação da aula com a versão do dia (ver Invariante 4 e Consequences). Foi uma escolha de owner, não uma omissão técnica.

---

## Consequences

### Positivas

- ✅ Resolve a decisão pendente que bloqueava a Story 2.5 Task 3 (e a dependência de tag da Story 2.4) — squad destravada.
- ✅ Versões .NET reais, verificáveis e congeladas: zero surpresa de `latest` ao vivo no SDK/pacotes. (n8n é a exceção aprovada pelo owner — roda `latest`, com mitigação por validação no início de cada aula.)
- ✅ SDK oficial reduz superfície de invenção do JSON-RPC (apoia AC-15 de 2.5).
- ✅ Alinhamento total com .NET 8 do projeto e com o gateway YARP/identidade (ADE-004/ADE-005).
- ✅ Demo de portabilidade entre LLMs preservada (integração no front, agnóstica de provider).

### Negativas / Trade-offs aceitos

- ⚠️ **Versões congeladas envelhecem:** patches de segurança não entram automaticamente. Mitigado: Invariante 5 prevê reavaliação no fim do epic / em CVE crítico; horizonte do workshop é curto.
- ⚠️ **SDK MCP em cadência rápida:** um 1.5.x pode trazer melhorias que ficaremos sem. Mitigado: o objetivo é didático e estável durante o evento; upgrade é decisão consciente, não perda.
- ⚠️ **n8n em `latest` não garante reprodutibilidade entre aulas (decisão de owner):** turmas em datas diferentes podem rodar versões diferentes; uma futura major do n8n (já houve breaking em 2.0) pode mudar UI/nodes. Mitigado: validar o workflow `post-purchase-notification` no **início de cada aula** + cada aula é **gravada com a versão do dia** (Invariante 4). Trade-off assumido conscientemente pelo owner em 2026-06-06.
- ⚠️ **`ModelContextProtocol.Core` pode ser referenciado só transitivamente:** se o @dev usar APIs low-level diretas, deve pinar explicitamente 1.4.0 também (registrado na Invariante 1).

---

## Alternatives Considered (rejeitadas)

### Alt 1: Usar `latest` / floating para o **SDK/pacotes .NET**
- **Rejected porque:** floating em tecnologia recente quebra reprodutibilidade ao vivo; o SDK MCP muda assinaturas entre minors. Mantém-se o pin exato para todos os pacotes .NET (Invariante 1/5). **Nota:** para o **n8n**, `latest` **não** foi rejeitado — foi **adotado por decisão de owner (2026-06-06, Invariante 4)**, sobrepondo a restrição original do blueprint (Story 2.4 AC-3, que pedia "não usar `latest`"). Veja Alt 1b.

### Alt 1b: Pinar o n8n em tag exata (ex.: `2.23.4`) — abordagem técnica anterior, **sobreposta pelo owner**
- **Não adotada porque:** apesar de o pin exato maximizar a reprodutibilidade entre aulas, o **owner decidiu (2026-06-06)** que os alunos devem rodar **sempre a versão mais nova** do n8n. A decisão de owner prevalece; o pin exato fica registrado aqui apenas como alternativa considerada. Trade-off de reprodutibilidade mitigado por validação no início de cada aula + gravação com a versão do dia (Invariante 4).

### Alt 2: Implementar o JSON-RPC 2.0 do MCP à mão (sem SDK)
- **Rejected porque:** multiplica a superfície de invenção (AC-15) e o risco de divergir da spec; o SDK oficial já entrega `tools/list`/`tools/call`/transport. Manter à mão só faria sentido se não existisse SDK GA — existe (1.4.0).

### Alt 3: Pinar a linha legada n8n 1.123.x
- **Rejected porque:** é linha legada/manutenção; iniciar um workshop novo na linha antiga não tem ganho didático e perde melhorias de UI/nodes da 2.x. 2.x é a linha GA ativa.

### Alt 4: Orquestrar o LLM no backend (.NET) com SDK Gemini .NET
- **Rejected como caminho desta fase (mantido como nota):** o `Google.GenAI` .NET está em estágio inicial/preview; e o desenho da story coloca o chatbot no front (desacoplamento MCP). Se houver necessidade backend futura, usar HTTP REST direto ao endpoint oficial — não um SDK imaturo no caminho crítico.

---

## Validation

Esta decisão é considerada **validada** quando:

- [ ] `src/Fifa2026.V2.McpServer/*.csproj` referencia `ModelContextProtocol.AspNetCore` em **`Version="1.4.0"`** exata (e quaisquer pacotes MCP diretos também em 1.4.0); nenhum range/wildcard/`latest`.
- [ ] `dotnet restore` resolve a linha MCP 1.4.0 sem conflito; projeto compila em .NET 8.
- [ ] Endpoint `/mcp` responde `tools/list` com as 3 tools (schemas válidos) e `tools/call` despacha os 3 handlers — métodos rastreáveis à spec `modelcontextprotocol.io` (AC-15).
- [ ] Frontend integra Gemini `gemini-2.0-flash` via `v1beta`; troca `LLM_PROVIDER` para `groq`/`mistral` sem mudança de código (AC-10).
- [ ] Container n8n provisionado como `n8nio/n8n:latest` (decisão de owner — Inv 4); workflow `post-purchase-notification` (4 nodes) revalidado end-to-end no **início da aula** (Story 2.4 Task 10.2).
- [ ] Nenhum artefato **.NET** versionado do epic usa floating em MCP SDK (`.csproj` com versões exatas). O n8n é a única exceção aprovada (`latest`, Inv 4).

## Impact on EPIC-002

### Stories afetadas (referência — re-draft de conteúdo é autoridade de @sm; aqui só registro o impacto e atualizo as referências de versão pendentes)

| Story | Impacto | Ação |
|---|---|---|
| **2.5 (F5)** | Decisão pendente **resolvida**. AC-6/Task 2: a ADE-002 agora existe (este arquivo). Task 3 deve referenciar o pin `ModelContextProtocol* 1.4.0`. Marcadores "pinning pendente"/"ADE-002 pendente" removidos. | Referências de versão atualizadas nesta sessão; conteúdo de AC/escopo permanece de @sm. |
| **2.4 (F4)** | Tag n8n **decidida por owner (2026-06-06)**: `n8nio/n8n:latest` (sempre a versão mais nova; sobrepõe a restrição "não usar latest" do blueprint/AC-3). AC-3, Task 4.2 e Task 10.2 atualizados. | Referência de tag atualizada para `latest` nesta sessão. |

> **NÃO altero Status nem AC/escopo das stories** (autoridade de @po/@sm). Esta ADE fecha a decisão técnica e atualiza apenas as referências de versão que estavam marcadas como pendentes para o @architect.

### Nota de naming

O nome do arquivo (`ade-002-mcp-pinning.md`) segue a referência já gravada na Story 2.5 AC-6/Task 2.1. O escopo foi ampliado para cobrir, na mesma ADE, o pin do n8n da F4 (decisão de versão de mesma natureza, pedido na mesma escalação) — evitando uma ADE extra para uma única tag. ADE-001 permanece aposentada (ADE-005); ADE-002 é este slot.

---

## Addendum — Decisões abertas resolvidas no gate da Story 2.5 (2026-06-06)

O quality gate da Story 2.5 (@architect) confirmou a implementação e fecha as 3 questões que o @dev deixou em aberto para o @architect (Dev Agent Record → "Para @architect"). Estas decisões **complementam** as Invariantes 1–5 sem alterá-las.

### Decisão A — Host do McpServer = **Azure Container App** (resolve o "em aberto" da Inv 2)

A Invariante 2 deixou a escolha final de host (Container App vs Function .NET isolated) como ponto de design da Task 3. **Decisão: Azure Container App.** Justificativa:

- O McpServer é um servidor HTTP de **longa duração** que serve **Streamable HTTP** (`MapMcp()`); esse é o caso de uso natural de um Container App, não de uma Function event-driven. Streaming + cold start de scale-to-zero de Functions atritam com a sessão MCP.
- **Mesma justificativa e mesmo host do gateway YARP** (ADE-004 Inv 2) → uniformidade operacional (mesmo ACR, mesmo padrão de Dockerfile multi-stage, mesma malha de deploy `deploy-phase-05.yml`).
- O `Dockerfile` do McpServer (multi-stage SDK 8.0 → aspnet 8.0) já foi adaptado do gateway; o workflow já faz `az containerapp update`. Implementação coerente com a decisão. **Trade-off aceito:** abre-se mão da uniformidade "tudo em Functions"; ganha-se aderência ao modelo de servidor HTTP persistente. Function isolated permanece alternativa teórica, mas **não recomendada** para este caso de streaming.

### Decisão B — Proxy LLM **dentro do McpServer** = ACEITÁVEL para o escopo do workshop

O `LlmProxyEndpoints.cs` (rotas `/llm/{provider}/{*path}`) vive no mesmo host do McpServer. **Decisão: aceitável**, com ressalva registrada:

- **Pró (decisivo no escopo didático):** reuso de host + App Settings (as keys já estão no Container App do McpServer); mantém "tudo passa pelo gateway" (o front fala `/llm` no gateway → cluster `mcp-server`); menos um artefato de deploy para o aluno provisionar em 8h.
- **Separação de responsabilidades é fraca, mas mitigada:** o proxy só injeta a key e encaminha o corpo cru ao endpoint oficial pinado (Inv 3) — não há acoplamento lógico com as tools MCP; são endpoints minimal-API independentes do pipeline `MapMcp`. A key nunca toca o front (fail-safe 503 sem key; CI guard no `dist/`).
- **Ressalva (não bloqueante):** num produto real, o proxy de LLM seria um BFF/serviço próprio (limite de blast radius da key, escala independente). Para o workshop, a co-hospedagem é a escolha pragmática correta. Registrado como nota de evolução pós-epic; **não exige mudança nesta fase.**

### Decisão C — Rótulos `VIP/Cat1/Cat2` no PIVOT **NÃO batem com o seed real** → CONCERN (correção obrigatória)

A Task 3.5 (`consultar_disponibilidade`) faz PIVOT condicional em `tc.category = 'VIP' / 'Cat1' / 'Cat2'`. **Verificação contra o seed canônico** (`fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql`, o mais recente; idem `legacy/seed.sql`) mostra que os rótulos reais de `ticket_categories.category` são **`'VIP Premium'`, `'Categoria 1'`, `'Categoria 2'`** (e `'Categoria 3'` no legacy). Os rótulos do código (`'VIP'`, `'Cat1'`, `'Cat2'`) **não existem na base** → todas as somas do PIVOT retornariam **0** e os preços **NULL** para qualquer partida real.

- **Risco:** AC-3 falha em runtime (a tool "encontra" a partida mas reporta disponibilidade zero/sem preço); o smoke test AC-11 ("Tem ingresso para Brasil x Argentina?") daria resposta incorreta ao vivo.
- **Por que os testes não pegaram:** todos os 32 testes mockam `IFifaQueryRepository`; o SQL real nunca roda contra o schema real. É exatamente a lacuna que o @dev sinalizou ("seed em runtime").
- **Recomendação (obrigatória antes do deploy):** alinhar o PIVOT aos rótulos reais — `'VIP Premium'`, `'Categoria 1'`, `'Categoria 2'` — preferencialmente com casamento case-insensitive e tolerante a variação (ex.: `LIKE 'VIP%'`, `'Categoria 1'`/`'Cat1'`) para resiliência entre seeds (legacy vs real-fifa-prices). Validar a query real contra a base no início da aula (paridade com a validação de seed de outras fases). Detalhe e patch sugerido no gate report `docs/qa/2026-06-06-architect-gate-S2.5.md`.

## Change Log

| Date | Author | Description |
|---|---|---|
| 2026-06-06 | @architect (Aria) | ADE criada — pin MCP C# SDK 1.4.0 exato, LLM no front (endpoint/modelo pinados), n8n `2.23.4` exato. |
| 2026-06-06 | @architect (Aria) | **Addendum (gate Story 2.5):** resolvidas as 3 questões abertas do @dev — (A) host McpServer = Azure Container App; (B) proxy LLM co-hospedado no McpServer = aceitável (ressalva BFF pós-epic); (C) rótulos `VIP/Cat1/Cat2` do PIVOT NÃO batem com o seed real (`VIP Premium`/`Categoria 1`/`Categoria 2`) → CONCERN, correção obrigatória antes do deploy. Pinos .NET e endpoints LLM inalterados. |
| 2026-06-06 | @architect (Aria) | **owner override — n8n → `latest`.** Decisão de owner: container n8n passa a usar `n8nio/n8n:latest` (sempre a versão mais nova). Invariante 4 reescrita; Invariante 5 ("proibido floating") reescopada para pacotes/SDK .NET com n8n como exceção explícita aprovada. Rationale, Consequences, Alternatives (Alt 1/1b), Validation e tabela de Impacto atualizados de forma coerente. Trade-off de reprodutibilidade documentado (mitigação: validar workflow no início de cada aula + aula gravada com a versão do dia). **MCP SDK 1.4.0 e endpoints LLM permanecem inalterados.** |

**Authority:** Aria (Architect) — designado por @aiox-master para decisões de seleção de tecnologia, versionamento e integração.
**Review cycle:** Imutável durante EPIC-002 **para os pinos .NET**; o n8n acompanha `latest` por decisão de owner. Mudanças nos pinos .NET → nova ADE que a supersede (ou nota de upgrade nos Dev Notes conforme Invariante 5).
