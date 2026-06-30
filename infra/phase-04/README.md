# infra/phase-04 — n8n self-hosted (Story 2.4 / F4)

Artefatos IaC/config versionados da Fase 4. **Não provisionam o Azure sozinhos** — são
o contrato de configuração consumido pelo workflow
[`.github/workflows/deploy-phase-04.yml`](../../.github/workflows/deploy-phase-04.yml)
e a base para aplicação manual via `az containerapp`.

> Escopo de código/infra desta entrega (@devops). Os artefatos didáticos
> (`docs/workshops/phase-04/`) são responsabilidade do @analyst (Task 7/9 da story).

## Arquivos

| Arquivo | Papel |
|---|---|
| `n8n-containerapp.yaml` | Definição declarativa do Container App do n8n (imagem, ingress, env vars, volume, scale, basic auth). Fonte da verdade + base para `az containerapp create --yaml`. |
| `post-purchase-notification.workflow.json` | Workflow de referência (4 nodes) para importar no n8n UI (AC-5). Reprodutível entre aulas. |
| `apply-volume-mount.py` | Helper que injeta o volume Azure Files (`/home/node/.n8n`) no YAML do Container App (usado pelo job `deploy-n8n`). |

## Decisões fixadas

- **Imagem:** `n8nio/n8n:latest` — decisão de owner ([ADE-002](../../docs/architecture/ade-002-mcp-pinning.md) Inv 4).
  Sempre a versão mais nova. **Mitigação obrigatória:** revalidar o workflow
  `post-purchase-notification` no **início de cada aula** (Story 2.4 Task 10.2), pois
  `latest` pode ter avançado (n8n já teve breaking em major 2.0).
- **Persistência:** Azure Files share `n8n-data` montado em `/home/node/.n8n` (AC-4).
- **Segurança:** basic auth obrigatório + HTTPS only (`allowInsecure: false`) (AC-10).
- **Scale:** Consumption, min 0 / max 2, 0.5 vCPU / 1Gi (Dev Notes).

## Env vars do n8n (rastreadas à doc oficial — AC-13)

Todas conferidas em <https://docs.n8n.io/hosting/environment-variables/>:

| Env var | Valor | Fonte |
|---|---|---|
| `N8N_BASIC_AUTH_ACTIVE` | `true` | docs.n8n.io/hosting/environment-variables/ |
| `N8N_BASIC_AUTH_USER` | `admin` | idem |
| `N8N_BASIC_AUTH_PASSWORD` | (secret) | idem |
| `N8N_HOST` | FQDN do Container App | idem |
| `WEBHOOK_URL` | `https://<fqdn>` | idem |
| `N8N_PROTOCOL` | `https` | idem |
| `N8N_PORT` | `5678` | idem |
| `DB_TYPE` | `sqlite` | idem (default; persistido via Azure Files) |
| `GENERIC_TIMEZONE` | `America/Sao_Paulo` | idem |

## Integração com a Function F1 (consumer)

O `PurchaseConsumerFunction` (em `src/Fifa2026.V2.Functions/`) dispara um **webhook
fire-and-forget** ao n8n **apenas** quando a compra é gravada (`InsertOutcome.Inserted`),
nunca em duplicata. Detalhes:

- URL via App Setting **`N8N_WEBHOOK_URL`** (nunca hardcoded — AC-6). Vazio = no-op.
- Payload JSON no **corpo**: `correlationId`, `matchId`, `category`, `entraOid` —
  todos vindos do **corpo** da `PurchaseMessage` (não das Application Properties do SB).
- Timeout 5s; **qualquer** falha do n8n é capturada e logada — a mensagem do Service
  Bus **nunca** vai ao DLQ por causa do n8n.

> Implementação: `src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs`
> (+ `IN8nWebhookNotifier.cs`, `N8nWebhookPayload.cs`). Registro DI em `Program.cs`
> (`AddHttpClient<IN8nWebhookNotifier, N8nWebhookNotifier>`).

> **Nota de fidelidade (Art. IV):** o blueprint da AC-6 lista um campo `amount` no
> payload, porém a `PurchaseMessage` de F1 **não** carrega valor monetário no corpo
> (o `unit_price` só é resolvido no INSERT do SQL, via JOIN em `ticket_categories`).
> Para não inventar um valor, o payload inclui apenas os campos realmente presentes.

## Ordem de configuração (runbook)

1. Aplicar o Container App do n8n (workflow job `deploy-n8n` ou `az ... --yaml`).
2. Acessar o n8n UI (basic auth), **importar** `post-purchase-notification.workflow.json`,
   **ativar** o workflow e copiar a URL gerada do webhook trigger
   (`https://<fqdn>/webhook/purchase`).
3. Gravar essa URL no secret do repo `N8N_WEBHOOK_URL`.
4. Rodar o job `deploy-function` (ou push) para aplicar o App Setting na Function App.
5. Smoke ponta-a-ponta (Story 2.4 Task 7 — smoke; demo @analyst): compra v2 → execução
   visível no n8n UI com `correlationId` correto.
