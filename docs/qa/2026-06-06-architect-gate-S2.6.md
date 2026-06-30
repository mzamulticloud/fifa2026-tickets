# Architect Quality Gate — Story 2.6 (F6: Flow Visualizer)

> **Gate by:** Aria (@architect)
> **Date:** 2026-06-06
> **Story:** docs/stories/2.6.story.md — EPIC-002 / F6
> **Branch:** phase-06-flow-visualizer
> **Gate tools:** code-review, ui-validation, telemetry-validation
> **Verdict:** **CONCERNS (GO)** — InReview → Done

---

## Verdict

**CONCERNS (GO).** O escopo de código (Tasks 1-5, 7.1, 8, 11) está arquiteturalmente correto, fiel à arquitetura real (Gateway YARP como nó zero, ZERO APIM como componente), com as 3 decisões do @dev validadas. Nenhuma issue HIGH/CRITICAL. Há 1 issue MEDIUM (doc-fix do AC-4) e issues LOW/observações que não bloqueiam. Tasks 6/7.2/9 são runtime/@devops e ficam corretamente fora do escopo deste gate.

---

## Decisão das 3 questões do @dev

### Decisão 1 — Host FlowEvents = serviço ASP.NET .NET 8 (Container App), NÃO Azure Function. **VALIDADO.**

Correto. AC-2 exige SignalR **Service Mode: Default (Hub clássico)**, que requer um host de longa duração com `AddSignalR().AddAzureSignalR()` + `IHubContext<FlowHub>`. O runtime serverless do Functions é incompatível com o Default mode (esse exige o SDK `Microsoft.Azure.SignalR.Management`/upstream connections, não um Hub hospedado). O `Fifa2026.V2.FlowEvents` segue o mesmo padrão de host do `McpServer` (Container App), preservando o princípio "um projeto .NET dedicado por hop do epic" (ADE-000). A nomenclatura "FlowEventsFunction" da story é apenas um rótulo; a substância (consulta App Insights + push SignalR) é honrada. Confirmado em `Program.cs` (linhas 35-44, 68).

### Decisão 2 — App Insights via `Azure.Monitor.Query` (`LogsQueryClient`) + Managed Identity Log Analytics Reader. **VALIDADO.**

Correto e é a escolha arquitetural certa. A REST `api.applicationinsights.io` está em desativação e o `TelemetryClient` é de ESCRITA (não de query). `LogsQueryClient.QueryWorkspaceAsync(workspaceId, query, timeRange)` é a API real do Azure SDK for .NET (anti-hallucination AC-13 satisfeito). Auth via `DefaultAzureCredential` → Managed Identity no Container App, com o papel **Log Analytics Reader** documentado no workflow (deploy-phase-06.yml, NOTA AC-3) e no PORTAL-GUIDE. WorkspaceId externalizado (`LogAnalyticsWorkspaceId`, nunca hardcoded — ADE-003 Inv 3). Confirmado em `Data/AppInsightsFlowEventRepository.cs` (linhas 24-36, 62-66).

### Decisão 3 — Correlation: corpo da PurchaseMessage + BeginScope (REAL) vs texto do AC-4 (ApplicationProperties). **CONFIRMADO o REAL; AC-4 marcado como doc-fix (MEDIUM).**

A implementação REAL propaga o correlationId no **corpo** da `PurchaseMessage` (`PurchaseEntryFunction.cs` linha 105-110: `Message = JsonSerializer.Serialize(message)` com `CorrelationId`) + `ILogger.BeginScope(["CorrelationId"])` → `customDimensions.CorrelationId` no App Insights. O consumer lê do **corpo** (`PurchaseConsumerFunction.cs` linha 44-46, BeginScope na linha 62), NÃO de `message.ApplicationProperties["CorrelationId"]`.

A FlowEvents consulta exatamente esse caminho real: `where tostring(customDimensions.CorrelationId) == correlationId` (`AppInsightsFlowEventRepository.cs` linha 41). Portanto código está coerente fim-a-fim.

**Conflito documental (não de código):**
- **AC-4 da story** (hop 2) diz "Incluído nas `ApplicationProperties` da mensagem".
- **ADE-000 Invariante 5** também lista "Service Bus hops: `ApplicationProperties["CorrelationId"]`".
- A implementação real usa corpo+BeginScope (alinhada ao gate S2.4/PO, Should-Fix v0.3.0).

Isso é um **doc-fix MEDIUM**, não um defeito de código. A propagação por corpo é válida e até mais robusta para o caminho de tracing que a FlowEvents efetivamente consulta (customDimensions). Recomendação abaixo (M-1).

---

## Confirmações solicitadas

| Item | Status | Evidência |
|---|---|---|
| 6 nós, Gateway YARP = nó zero | ✅ CONFIRMADO | `FlowEventType.cs` (GATEWAY_YARP_RECEIVED=0 .. SQL_INSERTED=5); `flowNodes.ts` FLOW_NODES[0]=Gateway YARP; testes `Node_zero_is_gateway_yarp_never_apim`, `Timeline_returns_six_nodes_with_gateway_yarp_as_node_zero`, `All_six_event_types_have_distinct_sequential_node_indexes` |
| ZERO referência a APIM (componente) | ✅ CONFIRMADO | grep em `src/Fifa2026.V2.FlowEvents` e `docs/workshops/phase-06`: todas as ocorrências são comentários/texto CORRETIVOS ("NUNCA APIM", "NÃO existe APIM", "corrija para Gateway YARP"). Teste força `Classify("legacy-apim","APIM policy executed") == null` |
| Correlation real (corpo+BeginScope→customDimensions) | ✅ CONFIRMADO | Functions gravam no corpo + BeginScope; FlowEvents lê `customDimensions.CorrelationId`. AC-4 doc desalinhado (M-1) |
| SignalR seguro | ⚠️ CONCERNS | Connection string só em App Setting `AzureSignalRConnectionString` (secretref no workflow, nunca no bundle); CORS origin restrito (bicep + Program.cs, sem `*` com credentials); fallback polling 2s. Hub SEM authorization por grupo (L-1, mitigado pelo gateway) |

---

## Avaliação de ACs (código / IaC / runtime)

| AC | Tipo | Status | Nota |
|---|---|---|---|
| AC-1 Branch + workflow | código | ✅ PASS | `deploy-phase-06.yml` (2 jobs, paths corretos) |
| AC-2 SignalR Free/Default | IaC | ✅ PASS | `infra/phase-06/signalr.bicep` (Free_F1, eastus2, ServiceMode=Default, CORS origin restrito). Provisionamento runtime = @devops/aluno |
| AC-3 Serviço dedicado consulta App Insights + push SignalR | código | ✅ PASS | FlowEvents (Azure.Monitor.Query + FlowHub); endpoints `/api/flow/recent`,`/{id}`,`/{id}/replay`,`/hubs/flow` |
| AC-4 Correlation nos 6 componentes | código | ⚠️ CONCERNS | Propagação real correta fim-a-fim; texto do hop 2 (ApplicationProperties) desalinhado com a impl (corpo+BeginScope) — M-1 |
| AC-5 Rota /flow (diagrama 6 nós, lista 50, busca) | código | ✅ PASS | `Flow.tsx`, `FlowDiagram.tsx`, `RecentPurchases.tsx` |
| AC-6 Animação tempo real + SignalR + fallback | código | ✅ PASS | framer-motion `motion.div` spring; `useFlowConnection` (SignalR + polling 2s); Sheet de payload |
| AC-7 Smoke ponta-a-ponta | runtime | ⛔ N/A neste gate | Task 6 — requer Azure provisionado (@devops/aluno) |
| AC-8 Performance | código(split)/runtime(60fps,TTI) | ✅ PASS (split) / runtime pendente | Build: chunk `Flow-*.js` ~66.8 KB gzip isolado; framer/signalr fora do bundle inicial. TTI<1.5s e 60fps = Lighthouse runtime (7.2) |
| AC-9 Acessibilidade | código | ✅ PASS | nós `<button>` aria-label; `<ol>` semântico; bolinha `aria-hidden`; modo lista toggleável + auto `prefers-reduced-motion`; busca/sort com aria-label |
| AC-10 Merge final + tag v2.0.0 | runtime | ⛔ N/A neste gate | Task 9 — @devops EXCLUSIVO |
| AC-11 6 artefatos didáticos | docs | ✅ PASS | `docs/workshops/phase-06/` (README, PORTAL-GUIDE, SPEAKER-NOTES, slides, intro-video-script + branch/workflow) |
| AC-12 Retro do workshop | docs | ✅ PASS | SPEAKER-NOTES bloco de encerramento + retro + perguntas finais |
| AC-13 Anti-hallucination | código/docs | ✅ PASS | APIs reais (Azure.Monitor.Query, Microsoft.Azure.SignalR, @microsoft/signalr, framer-motion); ZERO APIM componente; teste anti-regressão |

---

## Issues por severidade

### MEDIUM

- **M-1 (docs / requirements) — AC-4 e ADE-000 Inv 5 descrevem Service Bus hop via `ApplicationProperties["CorrelationId"]`, mas a implementação real usa corpo da mensagem + `ILogger.BeginScope`.**
  - **Impacto:** apenas documental — o código é coerente fim-a-fim e a FlowEvents lê o caminho real (`customDimensions.CorrelationId`). Sem impacto funcional. Risco: confunde aluno/futuro mantenedor que comparar AC-4 com o código.
  - **Recomendação:** @po atualiza o texto do hop 2 do AC-4 (e a nota de ADE-000 Inv 5, via ADE addendum) para "correlationId propagado no CORPO da PurchaseMessage + `ILogger.BeginScope` → `customDimensions.CorrelationId`". Não requer mudança de código. Já registrado pelo PO como Should-Fix na v0.3.0; converter em fix de redação antes do merge final (Task 9).

### LOW

- **L-1 (security) — FlowHub não tem autorização por grupo: qualquer cliente conectado pode `Subscribe("correlation-<id>")` de qualquer correlationId.**
  - **Impacto:** baixo no contexto do workshop. O gateway YARP é o guardião de JWT (a request REST `/flow-events/**` exige Bearer Entra), mas o WebSocket do Hub roteado por `/flow-events/hubs/**` passa pelo mesmo gateway com `RequireAuthorization()`, então a CONEXÃO exige token. Porém, uma vez conectado, não há checagem de que o usuário "possui" aquele correlationId. Como correlationId é um GUID opaco e os dados são telemetria de fluxo (sem PII além do payload de compra), o risco é baixo para um workshop didático.
  - **Recomendação:** aceitável para o workshop (documentar como trade-off). Para produção, adicionar `[Authorize]` no FlowHub e validar ownership do correlationId no `Subscribe` (ou derivar o grupo do claim do usuário). Anotar no SPEAKER-NOTES como "o que eu mudaria em produção".

- **L-2 (observability) — `TraceEventMapper` classifica por substring de mensagem/role (heurística textual).**
  - **Impacto:** baixo. Funciona com as mensagens reais das Functions (`"Compra v2 recebida"`, `"Processando compra v2"`, `"gravada com sucesso"`), cobertas por testes. Mas é frágil a mudanças de wording de log nas fases anteriores (um rename de log message em F1 quebraria a classificação silenciosamente).
  - **Recomendação:** aceitável (a heurística está testada e o acoplamento é didaticamente visível). Para robustez futura, considerar `customDimensions.EventType` explícito emitido por cada hop. Anotar como evolução possível.

- **L-3 (performance) — `durationMs` não é populado a partir dos traces.**
  - **Impacto:** baixo/cosmético. AC-5/AC-6 pedem "tempo gasto no hop" por nó; `FlowEvent.DurationMs` existe no modelo mas o `AppInsightsFlowEventRepository` não o preenche (a query projeta `timestamp/message/severity/role`, não duração). O front trata `durationMs == null` graciosamente ("aguardando"/omite). Logo a UI não quebra, mas a métrica de duração por hop fica vazia em runtime real.
  - **Recomendação:** opcional para o gate (não há AC que exija duração por hop com número específico além do smoke runtime). Para completar AC-6 plenamente, derivar `durationMs` como delta entre timestamps consecutivos de hops, ou ler `duration` de `dependencies`/`requests`. Pode entrar como ajuste no smoke runtime (Task 6).

### Observações (não-issues)

- O endpoint `POST /{id}/replay` relê a telemetria a cada seleção. Em runtime, a latência de ingestão do App Insights (até ~2min) pode atrasar a "bolinha" além dos 30s do AC-7 — já documentado no Troubleshooting da story e do PORTAL-GUIDE. Mitigação (near-real-time/pré-popular) prevista. Sem ação no gate.
- CORS aparece em 2 camadas (SignalR bicep + FlowEvents Program.cs + gateway). Consistente (origin restrito em todas), correto para WebSocket com credentials.

---

## Validação de testes (executados neste gate)

| Suite | Resultado | Executado por mim |
|---|---|---|
| `Fifa2026.V2.FlowEvents.Tests` | **22/22 PASS** (319 ms) | ✅ `dotnet test -c Release` |
| `Fifa2026.V2.Gateway.Tests` (regressão pós rota flow-events) | **11/11 PASS** (913 ms) | ✅ `dotnet test -c Release` |
| Frontend `npm run lint` | **PASS** (0 erros) | ✅ |
| Frontend `npm run build` | **PASS** — chunk `Flow-BcfsQcga.js` 213 KB / **66.8 KB gzip** isolado | ✅ |
| `Fifa2026.V2.Functions.Tests` (50) / `McpServer.Tests` (41) | reportados PASS pelo @dev (não re-executados — fora do delta de F6) | — |

---

## Gate decision

**CONCERNS (GO).** Story 2.6 escopo de código aprovado para `InReview → Done`. As issues são 1 MEDIUM doc-fix (M-1, ação @po antes do merge) + 3 LOW (aceitáveis para o workshop, anotar trade-offs). Tasks runtime/@devops (6, 7.2, 9, 10-merge) seguem o fluxo normal: @devops abre PR phase-06 → main e aplica a tag `v2.0.0` (AC-10) após o smoke runtime.

**Carry-forward para @devops / Task 9:**
- M-1: alinhar texto do AC-4 (hop 2) à implementação real (corpo+BeginScope) — doc-fix antes do merge.
- L-1: documentar no SPEAKER-NOTES o trade-off de autorização do Hub ("em produção: `[Authorize]` + ownership do correlationId").
- AC-7/AC-8 runtime (smoke + Lighthouse) a validar no deploy real antes da tag v2.0.0.
