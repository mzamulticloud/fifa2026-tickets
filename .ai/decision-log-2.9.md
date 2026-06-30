# Decision Log: Story 2.9

**Agent:** dev (Dex)
**Mode:** YOLO (Autonomous Development)
**Story:** docs/stories/2.9.story.md
**Started:** 2026-06-10

---

## Decisions Made

### Decision 1: Modo YOLO para o *develop

**Type:** process · **Priority:** medium

**Reason:** Operação autônoma (usuário ausente); story determinística com Dev Notes completas, code samples prontos e AUTO-DECISIONS já adjudicadas pelo @po (GO 9/10). Decision logging ativo compensa a ausência de checkpoints interativos.

**Alternatives:** Interactive (default — exigiria usuário presente), Pre-Flight (desnecessário — zero ambiguidade após validação @po).

### Decision 2: Task 0 via `az containerapp exec` (inspeção do container vivo) em vez de UI do n8n

**Type:** verification-approach · **Priority:** high

**Reason:** A instância n8n viva NÃO usa basic auth (env vars `N8N_BASIC_AUTH_*` ausentes no Container App — verificado via `az containerapp show`). n8n 2.x usa user management próprio (login e-mail/senha no PostgreSQL) e as credenciais de owner não estão disponíveis para o agente. A inspeção via `exec` no filesystem do container (`@n8n/n8n-nodes-langchain/dist/nodes/`) é verificação direta na instância viva — satisfaz AC-1/Art. IV (No Invention) com evidência mais forte que screenshot de UI.

**Alternatives:** browser-harness na UI (bloqueado por credenciais), n8n REST API (requer API key inexistente), docs.n8n.io (NÃO satisfaz AC-1 — exige instância viva).

**Side effect:** data plane do `exec` tem rate limit (429 após 2 sessões paralelas; retry-after 600s). Mitigação: chamadas sequenciais espaçadas.

### Decision 3: Story 2.4 desatualizada quanto ao auth do n8n — registrar, não corrigir

**Type:** scope · **Priority:** low

**Reason:** S2.4 descreve basic auth (`N8N_BASIC_AUTH_*`), mas o deploy real usa user management + PostgreSQL (decisão registrada na memória F4: sqlite+Azure Files inviável → PostgreSQL gerenciado). S2.4 está Done; correção retroativa está fora do escopo da S2.9. Registrado nos achados da Task 0.

---

## Files Modified

- docs/stories/2.9.story.md (status Ready → InProgress + Change Log)
- .ai/decision-log-2.9.md (created)
