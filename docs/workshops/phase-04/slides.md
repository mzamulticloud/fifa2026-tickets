---
title: "F4 — Orquestração Visual: n8n em Container Apps disparado pelo consumer F1"
subtitle: "Workshop Living Lab Azure-Native · Fase 4 de 6"
theme: black
revealOptions:
  transition: slide
---

# F4 — Orquestração Visual

## n8n self-hosted em Container Apps, disparado pelo consumer F1

Workshop **Living Lab Azure-Native** · Fase 4 de 6

`Compra gravada` → `webhook fire-and-forget` → [n8n: Switch → e-mail | log]

---

## O dia em 7 blocos · 6h

1. Conceitos: workflow, low-code vs código, n8n, fire-and-forget — 50min
2. Container Apps Environment + Azure Files — 45min
3. n8n Container App (imagem, env vars, basic auth, volume) — 60min
4. ☕ Coffee — 15min
5. Desenhar/importar o workflow (4 nodes) — 50min
6. Integração: consumer dispara o webhook — 55min
7. Smoke ponta-a-ponta + segurança — 45min · Retro + carry-over F5 — 40min

---

## A frase do dia

# "O consumer grava a compra; <br/> o n8n cuida do que vem depois."

<small>A compra é crítica (garantida). A notificação é best-effort (fire-and-forget).</small>

---

## De onde viemos (F1 → F2 → F3)

```
[Browser+MSAL] → [Gateway YARP] → [Entry] → [SB] → [Consumer] → SQL
                  X-Correlation-ID    valida JWT       INSERT idempotente
```

- A compra grava no SQL... **e nada acontece depois**.
- Na vida real: e-mail, log, CRM, Slack — **orquestração**.

<small>Hoje a gente pendura o "depois da compra" — sem derrubar o consumer.</small>

---

## Bloco 1 — Conceitos

### Workflow · low-code vs código · n8n · fire-and-forget

---

## O que é um workflow

Uma sequência de passos disparada por um evento:

> "Quando uma compra é gravada → é VIP? → sim: e-mail premium; não: log padrão."

- Cada passo = **node**
- A ligação entre eles = o **fluxo**

---

## Duas formas de fazer

| | No código (consumer C#) | Low-code (n8n) |
|---|---|---|
| Lógica | `if (VIP) SendEmail()...` | desenho visual |
| Mudar | recompila + redeploy | edita o desenho |
| Adicionar ação | mais código no caminho crítico | arrasta um node |
| Falha afeta a compra? | pode (se mal feito) | **não** (fire-and-forget) |

---

## Low-code não é "sem código"

É tirar a **orquestração** do **caminho crítico**.

- O consumer continua C# **testado**
- Ele só **delega** o pós-processamento
- A lógica de notificação vive **fora**, fácil de evoluir

<small>Régua de tomadas: o consumer "liga na tomada"; o que está plugado muda sem mexer na parede.</small>

---

## O que é o n8n

- Automação de workflow **open-source** e **self-hostable**
- O primo do Zapier/Make que roda na **sua** infra
- Workflow visual, mas **exportável como JSON** (versionável)
- Imagem oficial: `n8nio/n8n` no Docker Hub

<small>`infra/phase-04/post-purchase-notification.workflow.json` → você importa, não desenha do zero.</small>

---

## Nota de versão (honesta)

# `n8nio/n8n:latest`

- **Decisão de owner** (ADE-002 Inv 4): sempre a versão mais nova
- **Trade-off:** reprodutibilidade entre aulas **não** garantida
- **Mitigação:** revalidar o workflow no **início de cada aula** + gravar com a versão do dia

<small>Se a UI estiver diferente dos prints, é o `latest` — os conceitos não mudam.</small>

---

## Fire-and-forget (o coração da fase)

O consumer dispara o webhook do n8n e **não espera nem deixa quebrar**:

1. **Só em `Inserted`** (não em `Duplicate`) → idempotência
2. **Timeout 5s** → não bloqueia o Service Bus
3. **Qualquer falha → log, nunca re-throw**
4. **Defesa em profundidade**: try/catch no notifier **E** no consumer

---

## Por que isso importa

# A compra **nunca** vai ao DLQ por culpa do n8n.

- Compra = crítica, transacional, **garantida**
- Notificação = secundária, **best-effort**
- Misturar os dois níveis de garantia = erro de arquitetura

<small>Se o n8n cair, a compra continua gravada. A notificação simplesmente não acontece desta vez.</small>

---

## Bloco 2 — Container Apps Environment + Azure Files

### O "datacenter lógico" e o disco persistente

---

## Por que Container Apps

| Opção | Por que (não) |
|---|---|
| **Container Apps** ✅ | serverless, scale-to-zero, HTTPS pronto — **mesmo do YARP (F2)** |
| AKS | K8s é desproporcional p/ 1 container |
| App Service | ok, mas ACA é o padrão do epic (ADE-003) |
| ACI | sem ingress/scale geridos |

<small>Você já fez isso na F2. É reuso de conhecimento.</small>

---

## Container é efêmero

```
restart  →  disco interno apagado  →  workflows somem
```

- O n8n guarda tudo em **`/home/node/.n8n`**
- Solução: montar um **Azure Files** nesse caminho

<small>O container é um quarto de hotel; o Azure Files é o cofre na recepção.</small>

---

## Azure Files: o cofre

```
Azure Files share "n8n-data"  ──montado em──>  /home/node/.n8n
```

- File share **`n8n-data`** na Storage Account
- Registrado no CAE como storage lógico **`n8ndata`**
- `DB_TYPE=sqlite` → o banco vive nesse disco persistido

<small>Sem o mount: todo restart apaga os seus workflows.</small>

---

## Bloco 3 — n8n Container App

### Imagem · env vars · basic auth · ingress · volume

---

## A configuração (IaC real)

`infra/phase-04/n8n-containerapp.yaml`

- **Imagem:** `n8nio/n8n:latest`
- **Recursos:** 0.5 vCPU · 1 Gi
- **Réplicas:** min **0** / max **2** (scale-to-zero)
- **Ingress:** external · **HTTPS only** · target port **5678**

---

## Env vars do n8n (doc oficial — AC-13)

| Var | Valor |
|---|---|
| `N8N_BASIC_AUTH_ACTIVE` | `true` |
| `N8N_BASIC_AUTH_USER` | `admin` |
| `N8N_BASIC_AUTH_PASSWORD` | (secret) |
| `N8N_HOST` / `WEBHOOK_URL` | `<fqdn>` / `https://<fqdn>` |
| `N8N_PROTOCOL` / `N8N_PORT` | `https` / `5678` |
| `DB_TYPE` / `GENERIC_TIMEZONE` | `sqlite` / `America/Sao_Paulo` |

<small>Todas rastreadas a docs.n8n.io/hosting/environment-variables/ — nada inventado.</small>

---

## Segurança (AC-10)

- **basic auth obrigatório** — senha como **secret**, nunca em texto
- **HTTPS only** — `allowInsecure: false` (ACA redireciona HTTP→HTTPS)
- Prova:

```bash
curl -s -o /dev/null -w '%{http_code}' https://<fqdn>/
# → 401  (sem auth)
```

<small>Painel admin exposto na internet SEM auth = não. Nunca.</small>

---

## Cold start (scale-to-zero)

- `minReplicas: 0` → n8n "dorme" quando ocioso (custo ~US$0)
- 1ª requisição depois de dormir → paga o **cold start**
- Por isso: **timeout 5s** no webhook + **warm-up** antes da demo

---

## Bloco 4 — O workflow `post-purchase-notification`

### 4 nodes · importe o JSON de referência

---

## Os 4 nodes

```
[Webhook (purchase)]
        │
   [Switch (VIP?)]
     │         │
 VIP │         │ senão
     ▼         ▼
[HTTP — VIP   [Set —
 email mock]   structured log]
```

---

## O que cada node faz

| Node | Faz |
|---|---|
| **Webhook (purchase)** | `POST` no path `purchase` → corpo em `$json.body` |
| **Switch (VIP?)** | `$json.body.category == "VIP"` → VIP; senão fallback |
| **HTTP — VIP email** | `POST httpbin.org/post` (mock e-mail premium) |
| **Set — structured log** | `{ correlationId, timestamp, notificationType, category }` |

---

## Armadilha #1: `$json.body`

O corpo do POST chega em **`$json.body`**, não `$json`.

```
✅  {{ $json.body.category }}
❌  {{ $json.category }}     ← Switch nunca bate
```

<small>Importar o JSON de referência evita esse erro.</small>

---

## Ativar e copiar a URL

1. **Save**
2. **Active** (toggle) ← sem isso, webhook = **404**
3. Copiar a **Production URL**: `https://<fqdn>/webhook/purchase`

---

## Bloco 5 — Integração: o consumer dispara o webhook

### fire-and-forget no `PurchaseConsumerFunction`

---

## Quem dispara? O **consumer**, não o gateway

```
[Gateway YARP] → [Entry] → [Service Bus] → [Consumer] → SQL
                                                 │
                                  InsertOutcome.Inserted
                                                 ▼
                                       POST webhook n8n   ◄── F4
```

<small>Só notifica uma compra que **de fato** aconteceu (gravada no SQL).</small>

---

## O payload (corpo JSON)

```jsonc
{
  "correlationId": "3fa85f64-...",  // nasce no gateway YARP (F2)
  "matchId": 1,
  "category": "VIP",                // usado no Switch
  "entraOid": "7c9e6679-..."        // claim oid (F3); null se sem Entra
}
```

<small>Sai do **corpo** da PurchaseMessage, não das Application Properties do SB.</small>

---

## Sem `amount` (Art. IV — No Invention)

- O blueprint pedia um campo `amount`
- Mas a `PurchaseMessage` **não carrega valor monetário** no corpo
- O `unit_price` só é resolvido no INSERT (JOIN em `ticket_categories`)

# Não inventamos o dado.

<small>Enviar `amount: 0` ou `null` confundiria quem consome o webhook. Só mande o que existe.</small>

---

## A regra do `outcome`

| `InsertOutcome` | Dispara n8n? | Por quê |
|---|---|---|
| **Inserted** | ✅ sim | compra nova gravada |
| **Duplicate** | ❌ não | idempotência (mesma compra 2x = 1 notificação) |
| **CategoryNotFound** | ❌ não | falha permanente → DLQ |

---

## O notifier (código real)

`N8nWebhookNotifier.cs` — procure um `throw`. **Não tem.**

```csharp
if (string.IsNullOrWhiteSpace(_webhookUrl)) { LogDebug(...); return; }  // no-op
timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));                        // timeout 5s
try { ... PostAsJsonAsync ... }
catch (Exception ex) { _logger.LogWarning(...); }                      // engole tudo
```

<small>+ try/catch também no consumer = defesa em profundidade.</small>

---

## O App Setting `N8N_WEBHOOK_URL`

- Lido por `IConfiguration["N8N_WEBHOOK_URL"]`
- **Nunca hardcoded** (AC-6)
- **Vazio = no-op silencioso** (F4 opcional)
- Valor: `https://<fqdn>/webhook/purchase`

> **Ordem:** workflow Active → copia URL → grava o App Setting

---

## Bloco 6 — Smoke ponta-a-ponta + segurança

### O correlationId atravessando os 5 hops

---

## O smoke (compra VIP)

```bash
curl -X POST https://<gateway-fqdn>/purchase \
  -H "Authorization: Bearer <token_entra>" \
  -d '{ "matchId":1, "category":"VIP", "userId":1, "quantity":1 }'
# → 202 { correlationId, status: "queued" }
```

Depois: **n8n → Executions** → execução verde com o `correlationId`.

---

## O fio de Ariadne

```
gateway → Entry → Service Bus → Consumer → n8n
   └──────────── mesmo correlationId ──────────┘
```

- App Insights: o mesmo `correlationId` em todos os hops
- É o que tornará possível o **Flow Visualizer da F6**

---

## Segurança ao vivo

```bash
curl ... https://<fqdn>/            # → 401 (sem auth)
curl -u admin:<senha> https://<fqdn>/   # → 200
```

<small>401 sem credencial, 200 com. Segurança de pé.</small>

---

## Bloco 7 — Retro + carry-over

---

## Código vs n8n (fechamento)

| Dimensão | Código (consumer) | n8n (low-code) |
|---|---|---|
| Garantia | transacional/crítica | best-effort |
| Evoluir | redeploy | editar o desenho |
| Risco no crítico | alto | nenhum |
| Quem mexe | devs C# | quem entende o negócio |

---

## Carry-over → F5

> Hoje o consumer disparou um webhook para uma **máquina** reagir a uma compra.

Na **F5**: uma **IA** plugada na sua plataforma —
MCP Server (.NET) expõe *tools*, um chatbot LLM decide quais chamar.

<small>Mesmo gateway YARP. Mesmo correlationId. Mesmo entraOid. O fio condutor continua.</small>

---

## Recapitulando a F4

- **n8n** self-hosted em **Container Apps** (`latest`, decisão de owner)
- **Azure Files** em `/home/node/.n8n` → persistência
- Workflow **4 nodes** (webhook → switch → e-mail mock | log)
- **Consumer** dispara **fire-and-forget**, só em **Inserted**
- **Nunca** manda a compra ao **DLQ**
- `N8N_WEBHOOK_URL` (App Setting, nunca hardcoded)

---

# Obrigado!

## Dúvidas?

`O consumer grava a compra; o n8n cuida do que vem depois.`

Próxima: **F5 — MCP Server + LLM**
