# Architect Quality Gate — Story 2.4 (F4: n8n em Container Apps)

> **Gate:** @architect (Aria) · **Tools:** container-validation, iac-validation, security-check
> **Date:** 2026-06-06 · **Branch:** `phase-04-orchestration` · **Story:** [2.4](../stories/2.4.story.md)
> **Decision reference:** [ADE-002](../architecture/ade-002-mcp-pinning.md) Inv 4 (n8n `latest`) · ADE-000 Inv 5 (correlation) · ADE-005 Inv 3 (`entraOid`)

---

## Verdict: **GO / PASS**

A entrega de código + infra + docs da Story 2.4 é **arquiteturalmente sólida e fiel à story**. O padrão fire-and-forget está implementado com defesa em profundidade real (notifier + consumer), a idempotência é preservada (webhook só em `Inserted`), os contratos de payload/env vars são fiéis (Art. IV respeitado, inclusive a omissão consciente de `amount`), a segurança do n8n está corretamente modelada (basic auth obrigatório + HTTPS-only validado no CI), e a tag `n8nio/n8n:latest` está consistente em todos os artefatos versionados conforme decisão de owner. Testes 35/35 verdes (reexecutados neste gate). As 3 issues encontradas são todas **LOW** (cosméticas/documentais) e não bloqueiam.

---

## Confirmações solicitadas (núcleo do gate)

| Item | Confirmação | Evidência |
|---|---|---|
| **Fire-and-forget sem DLQ** | ✅ CONFIRMADO | `N8nWebhookNotifier.cs` captura **toda** exceção (timeout 5s via `CancellationTokenSource`, rede, HTTP non-2xx) e nunca re-lança. `PurchaseConsumerFunction.cs` adiciona try/catch externo (defesa em profundidade). Testes `Http_non_2xx_does_not_throw`, `Network_failure_does_not_throw`, `Webhook_n8n_failure_does_NOT_propagate_to_dlq`. |
| **Disparo só em `Inserted`** | ✅ CONFIRMADO | `switch (outcome)` chama o notifier apenas no `case InsertOutcome.Inserted`. `Duplicate` e `CategoryNotFound` não disparam. Testes `Webhook_n8n_is_NOT_fired_on_Duplicate` e `..._on_CategoryNotFound`. |
| **n8n = `latest`** | ✅ CONFIRMADO | `n8n-containerapp.yaml` (`image: n8nio/n8n:latest`), `deploy-phase-04.yml` (`N8N_IMAGE: 'n8nio/n8n:latest'`), `infra/phase-04/README.md`, docs workshop — todos consistentes com ADE-002 Inv 4 (decisão de owner). |
| **Correlation (Inv 5)** | ✅ CONFIRMADO | `correlationId` + `entraOid` no **corpo** do payload (`N8nWebhookPayload.cs`), vindos do corpo da `PurchaseMessage` (confirmado em `PurchaseEntryFunction` e `PurchaseConsumerFunction`), não das Application Properties. `BeginScope` propaga correlationId ao App Insights. |

---

## ACs: atendidos (código/infra) vs. runtime

| AC | Tipo | Status | Nota |
|---|---|---|---|
| AC-1 (branch + workflow CI) | código/infra | ✅ PASS | `deploy-phase-04.yml` com 2 jobs; `dotnet test` SEM `--no-build` (lição S2.1 aplicada). |
| AC-6 (consumer dispara webhook) | código | ✅ PASS | Implementado + 5 testes F4. App Setting `N8N_WEBHOOK_URL`, vazio=no-op. |
| AC-7 (correlationId no payload) | código | ✅ PASS | No corpo; coberto por teste. |
| AC-13 (anti-hallucination env vars) | infra/docs | ✅ PASS | Tabela rastreada a docs.n8n.io; `amount` omitido por fidelidade (Art. IV). |
| AC-3 (n8n container app) | infra (IaC) | ✅ PASS (IaC) | `latest`, env vars completas, ingress 5678. Provisionamento real = runtime. |
| AC-4 (Azure Files mount) | infra (IaC) | ✅ PASS (IaC) | `/home/node/.n8n`, helper `apply-volume-mount.py` idempotente. |
| AC-10 (basic auth + HTTPS) | infra (IaC) | ✅ PASS (IaC) | `allowInsecure: false`; CI valida senha presente + smoke 401 sem auth. |
| AC-2, AC-5, AC-8, AC-9, AC-12 | runtime/Portal/demo | ⏳ RUNTIME | Execução do aluno/owner — fora do escopo deste gate de código/infra. |
| AC-11 (artefatos didáticos) | docs | ✅ PASS | 5 artefatos em `docs/workshops/phase-04/`, fiéis ao código real. |

---

## Issues

### LOW-1 — Comentário do CI referencia arquivo inexistente
- **Severidade:** LOW · **Categoria:** docs/iac
- **Local:** `.github/workflows/deploy-phase-04.yml` linhas 234-235
- **Descrição:** O comentário do step "Mount Azure Files volume" diz "O arquivo `infra/phase-04/n8n-volume-patch.yaml` documenta a forma esperada". Esse arquivo **não existe** — o que existe é `n8n-containerapp.yaml`. A chamada funcional (`python3 apply-volume-mount.py ...`) está **correta**; apenas o comentário aponta para um nome errado.
- **Impacto:** Nenhum funcional. Pode confundir quem lê o workflow.
- **Recomendação:** Corrigir o comentário para referenciar `n8n-containerapp.yaml` (ou remover a frase). Não bloqueia.

### LOW-2 — `HttpClient.Timeout` (10s) maior que o timeout efetivo (5s) — redundância benigna
- **Severidade:** LOW · **Categoria:** code
- **Local:** `Program.cs` (`client.Timeout = TimeSpan.FromSeconds(10)`) vs. `N8nWebhookNotifier` (CTS 5s)
- **Descrição:** O timeout efetivo é o do `CancellationTokenSource` (5s), que dispara antes do `HttpClient.Timeout` (10s). O comentário já explica que o 10s é "guarda superior conservador". É correto e intencional, mas a diferença pode levar um leitor a achar que o timeout é 10s.
- **Impacto:** Nenhum — o caminho de cancelamento via CTS prevalece e está testado.
- **Recomendação:** Manter como está (a redundância é defensiva). Opcionalmente alinhar o comentário enfatizando que 5s é o valor efetivo. Não bloqueia.

### LOW-3 — Validação de segurança do smoke é parcial (apenas UI 401)
- **Severidade:** LOW · **Categoria:** security
- **Local:** `deploy-phase-04.yml` step "Smoke test (AC-10)"
- **Descrição:** O smoke valida `GET / sem auth → 401` (correto). Não valida explicitamente o redirect/bloqueio HTTP→HTTPS (Task 8.3) nem o acesso autenticado 200 (Task 8.2) — esses ficam como passos de runtime (Tasks 7/8). O `allowInsecure: false` no IaC já garante o HTTPS-only na camada de ingress, então a defesa está correta por configuração; falta apenas a verificação ativa no CI.
- **Impacto:** Baixo — o controle existe (IaC), a verificação ativa é parcial.
- **Recomendação:** Aceitável para o escopo de gate. Tasks 8.2/8.3 permanecem como verificação de runtime (já previstas na story). Não bloqueia.

---

## Avaliação por dimensão

**Container/IaC.** `n8n-containerapp.yaml` é um contrato declarativo completo e correto: ingress external + `allowInsecure: false` + target 5678, scale 0/2, 0.5 vCPU/1Gi, secret para a senha (nunca em texto), volume Azure Files em `/home/node/.n8n`. O helper `apply-volume-mount.py` é idempotente (busca por nome antes de adicionar) e bem justificado — `az containerapp update --set-env-vars` realmente não cobre volume mounts. O workflow CI cria-ou-atualiza corretamente (`az containerapp show` guard) e exige a senha do basic auth (falha o job se ausente — AC-10 enforced no pipeline). Workflow JSON de referência (4 nodes) reprodutível mitiga o risco do `latest`.

**Segurança.** Basic auth obrigatório (env vars + secretRef), senha como secret do Container App e secret do repo (nunca hardcoded), HTTPS-only por `allowInsecure: false`. No código: `N8N_WEBHOOK_URL` via App Setting (vazio=no-op), `entraOid`/token nunca logados em texto (só `correlationId`, que é id de hop). Smoke de 401 no CI. Defesa em profundidade contra DLQ acidental. Postura de segurança adequada para o contexto de workshop.

**Padrão reusável de Container App.** O pattern (Consumption, scale-to-zero, ingress external HTTPS, Azure Files) é coerente com o que F2 usa para o gateway YARP e com ADE-003 — confirma a reusabilidade que justificou o gate do @architect.

**Fidelidade (Art. IV).** Exemplar. A omissão de `amount` é documentada em 3 lugares (payload class, infra README, workshop README) com a justificativa correta (não está no corpo da `PurchaseMessage`). Env vars rastreadas à doc oficial. Nada inventado.

**Testes.** 35/35 verdes (reexecutados: `dotnet test ... -c Release` → Aprovado 35, Falha 0, 52ms). Cobertura F4 cobre os 4 cenários críticos: dispara em Inserted, não em Duplicate, não em CategoryNotFound, e falha não propaga (nos dois níveis — notifier e consumer).

**Docs.** Fidelidade técnica alta. README e SPEAKER-NOTES descrevem com precisão o fire-and-forget, idempotência, no-DLQ, o quinto hop, os contratos exatos (payload, env vars, App Setting) e a ressalva honesta do `latest`. Links ADE corretos.

---

## QA Results (resumo)

- **Verdict:** GO / PASS
- **Blocking issues:** 0
- **LOW issues:** 3 (cosméticas/documentais — não bloqueiam)
- **Tests:** 35/35 PASS (reexecutado neste gate)
- **Recomendação de status:** InReview → **Done**
- **Carry-forward (runtime, já previsto na story):** Tasks 2-5, 7, 8.2/8.3, 10.2 (provisionamento real + smoke + revalidação do workflow no início de cada aula).
