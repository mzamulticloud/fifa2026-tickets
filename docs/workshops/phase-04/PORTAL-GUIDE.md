# PORTAL GUIDE — F4: n8n self-hosted em Container Apps + workflow visual

> **Guia passo-a-passo do Portal Azure + n8n UI** · Workshop "Living Lab Azure-Native" · Fase 4 de 6
> **Use junto com:** [`README.md`](./README.md) (leitura prévia), [`SPEAKER-NOTES.md`](./SPEAKER-NOTES.md) (roteiro 6h)
> **Story:** [2.4](../../stories/2.4.story.md) · **IaC de referência:** [`infra/phase-04/`](../../../infra/phase-04/) · **Decisão:** [ADE-002](../../architecture/ade-002-mcp-pinning.md) Inv 4 (n8n `latest`)

---

## 0. Antes de começar

**Pré-requisitos (da F1, F2, F3):**

- [ ] Sua **Function App** F1 no ar (com a `PurchaseConsumerFunction` lendo de `tickets-purchase`)
- [ ] Seu **gateway YARP** (F2) e a **identidade** (F3) funcionando — o fluxo de compra v2 grava no SQL
- [ ] Acesso a [portal.azure.com](https://portal.azure.com) com uma subscription ativa
- [ ] O **Resource Group** que você usa no workshop (o mesmo do gateway YARP — variável `PHASE02_RESOURCE_GROUP`)
- [ ] Substitua **`<iniciais>`** pelas suas iniciais em todos os nomes (ex.: `gpc`)

> **Região:** use **East US 2** (`eastus2`) — a mesma das fases anteriores, para manter tudo no mesmo lugar.

> **Convenção de nomes desta fase** (alinhada às variáveis do workflow [`deploy-phase-04.yml`](../../../.github/workflows/deploy-phase-04.yml)):
> | Recurso | Nome sugerido | Variável correspondente |
> |---|---|---|
> | Container Apps Environment | `cae-fifa2026-<iniciais>` | `PHASE04_CONTAINERAPP_ENV` |
> | Container App do n8n | `ca-n8n-fifa2026-<iniciais>` | `PHASE04_N8N_APP_NAME` |
> | Storage Account | `stn8n<iniciais>` (só minúsculas/números, ≤24 chars) | `PHASE04_STORAGE_ACCOUNT` |
> | File share | `n8n-data` | `PHASE04_FILE_SHARE` |
> | Storage lógico no CAE | `n8ndata` | `PHASE04_ACA_STORAGE_NAME` |

---

## Step 1 — Container Apps Environment (AC-2)

O **Environment** é o "datacenter lógico" onde seus Container Apps vivem. Se você já tem um do gateway YARP (F2), **pode reusá-lo** — neste caso, pule para o Step 2. Caso prefira um dedicado para a F4:

1. No Portal, busque **Container Apps** → **`+ Create`** → escolha **Create Container Apps Environment** (ou crie o app e o environment junto; aqui criamos o environment primeiro).
2. Na verdade, o caminho direto é: **Container Apps Environments** → **`+ Create`**.
3. Preencha:
   - **Subscription / Resource Group:** os seus (o mesmo RG da F2).
   - **Environment name:** `cae-fifa2026-<iniciais>`.
   - **Region:** **East US 2**.
   - **Plan / Zone redundancy:** **Consumption only** (sem zone redundancy — custo mínimo).
4. Na aba **Monitoring**, deixe criar um **Log Analytics workspace** associado (para observabilidade — AC-2). Aceite o default ou reutilize o existente.
5. **Review + create** → **Create**. Aguarde o provisionamento.

> 📸 **[PRINT 1: tela "Create Container Apps Environment" com `cae-fifa2026-<iniciais>`, East US 2, Consumption only]**

> **Por que reusar o do YARP é OK:** o Environment é compartilhável entre apps. O gateway e o n8n podem coexistir no mesmo CAE — é até bom para a observabilidade (mesmo Log Analytics).

---

## Step 2 — Storage Account + File share `n8n-data` (AC-4)

O n8n precisa de **disco persistente** para não perder os workflows em cada restart (ver [README seção 4](./README.md)). Isso é um **Azure Files share**.

### 2.1 Criar (ou reusar) a Storage Account

1. Portal → **Storage accounts** → **`+ Create`**.
2. Preencha:
   - **Resource Group:** o seu.
   - **Storage account name:** `stn8n<iniciais>` (só letras minúsculas e números, máx. 24 caracteres).
   - **Region:** **East US 2**.
   - **Performance:** Standard. **Redundancy:** **LRS** (mais barato; suficiente para o workshop).
3. **Review + create** → **Create**.

> 📸 **[PRINT 2: tela "Create storage account" com `stn8n<iniciais>`, Standard, LRS, East US 2]**

### 2.2 Criar o File share

1. Abra a Storage Account criada → menu lateral **Data storage** → **File shares** → **`+ File share`**.
2. **Name:** `n8n-data`. **Tier:** Transaction optimized (default). **Create**.

> 📸 **[PRINT 3: File share `n8n-data` criado dentro da Storage Account]**

### 2.3 Registrar o storage no Container Apps Environment

O CAE precisa "conhecer" esse file share antes de qualquer app montá-lo:

1. Abra o **Container Apps Environment** (`cae-fifa2026-<iniciais>`) → menu lateral **Settings** → **Azure Files** (ou **Storage**).
2. **`+ Add`**:
   - **Storage name (lógico no CAE):** `n8ndata` (este é o `PHASE04_ACA_STORAGE_NAME`).
   - **Storage account:** `stn8n<iniciais>`.
   - **File share:** `n8n-data`.
   - **Access mode:** **Read/Write**.
3. **Add**.

> 📸 **[PRINT 4: "Azure Files" do CAE com o storage lógico `n8ndata` apontando para o share `n8n-data`]**

> **Onde isso aparece no IaC:** veja [`infra/phase-04/n8n-containerapp.yaml`](../../../infra/phase-04/n8n-containerapp.yaml) (bloco `volumes:` → `storageType: AzureFile`, `storageName: <PHASE04_ACA_STORAGE_NAME>`). O Portal está fazendo o que o YAML descreve.

---

## Step 3 — Criar o Container App do n8n (AC-3, AC-10)

Agora subimos o n8n propriamente dito.

1. Portal → **Container Apps** → **`+ Create`**.
2. **Aba Basics:**
   - **Resource Group:** o seu. **Container app name:** `ca-n8n-fifa2026-<iniciais>`.
   - **Region:** East US 2. **Container Apps Environment:** selecione `cae-fifa2026-<iniciais>`.
3. **Aba Container:**
   - **Image source:** **Docker Hub or other registries**.
   - **Image type:** Public.
   - **Registry login server:** `docker.io`.
   - **Image and tag:** **`n8nio/n8n:latest`** ← decisão de owner ([ADE-002](../../architecture/ade-002-mcp-pinning.md) Inv 4 — sempre a versão mais nova).
   - **CPU and Memory:** **0.5 CPU cores, 1 Gi memory**.

> 📸 **[PRINT 5: aba Container com `n8nio/n8n:latest`, 0.5 vCPU, 1Gi]**

4. **Environment variables** (ainda na aba Container, seção **Environment variables**) — adicione **todas** as abaixo. **Todas conferidas na [doc oficial do n8n](https://docs.n8n.io/hosting/environment-variables/)** (AC-13 — não invente nenhuma):

   | Name | Source | Value |
   |---|---|---|
   | `N8N_BASIC_AUTH_ACTIVE` | Manual entry | `true` |
   | `N8N_BASIC_AUTH_USER` | Manual entry | `admin` |
   | `N8N_BASIC_AUTH_PASSWORD` | **Reference a secret** | (ver 3.1 abaixo) |
   | `N8N_HOST` | Manual entry | `<fqdn>` (preencha depois — ver nota) |
   | `WEBHOOK_URL` | Manual entry | `https://<fqdn>` (idem) |
   | `N8N_PROTOCOL` | Manual entry | `https` |
   | `N8N_PORT` | Manual entry | `5678` |
   | `DB_TYPE` | Manual entry | `sqlite` |
   | `GENERIC_TIMEZONE` | Manual entry | `America/Sao_Paulo` |

   > **Sobre o FQDN (`N8N_HOST` / `WEBHOOK_URL`):** o FQDN só é gerado **depois** que o app é criado com ingress. Estratégia: crie o app primeiro (Steps 3.1-3.4), copie o FQDN da aba **Overview**, e então **atualize** `N8N_HOST` e `WEBHOOK_URL` (Container App → **Containers** → **Edit and deploy** → editar env vars). Sem `WEBHOOK_URL` correto, o n8n constrói URLs de webhook erradas.

### 3.1 Definir a senha do basic auth como **secret** (AC-10)

A senha **nunca** vai em texto plano:

1. Antes de finalizar o create, vá em **Container App** → aba **Secrets** (ou, no wizard, a seção de secrets) → **`+ Add`**:
   - **Key:** `n8n-basic-auth-password`.
   - **Value:** uma senha forte gerada por você (anote-a — é o seu login no n8n).
2. Volte na env var `N8N_BASIC_AUTH_PASSWORD` → **Source: Reference a secret** → selecione `n8n-basic-auth-password`.

> 📸 **[PRINT 6: secret `n8n-basic-auth-password` e a env var `N8N_BASIC_AUTH_PASSWORD` referenciando-o]**

> **No IaC:** isto é o bloco `secrets:` + `secretRef: n8n-basic-auth-password` em [`n8n-containerapp.yaml`](../../../infra/phase-04/n8n-containerapp.yaml).

### 3.2 Ingress: external, HTTPS only, porta 5678 (AC-10)

1. **Aba Ingress:**
   - **Ingress:** **Enabled**.
   - **Ingress traffic:** **Accepting traffic from anywhere** (external).
   - **Ingress type:** HTTP.
   - **Target port:** **5678** (porta padrão do n8n).
   - **Transport:** Auto (HTTP/1).
   - **Insecure connections:** **desmarcado** (HTTPS only — o ACA redireciona HTTP→HTTPS).

> 📸 **[PRINT 7: aba Ingress — external, target port 5678, insecure desmarcado]**

### 3.3 Scale: min 0, max 2

1. **Aba Scale** (ou depois, em **Scale**): **Min replicas: 0**, **Max replicas: 2**.

> **Lembre do cold start:** com min=0, a primeira requisição depois de ocioso é lenta (o container sobe). Para a demo ao vivo, o facilitador faz um warm-up — ver [SPEAKER-NOTES](./SPEAKER-NOTES.md).

### 3.4 Volume mount Azure Files em `/home/node/.n8n` (AC-4)

O mount do volume é editado na **revisão** do app (nem todo wizard expõe na criação):

1. Crie o app (**Review + create** → **Create**) com os passos acima.
2. Depois de criado: **Container App** → **Application** → **Volumes** → **`+ Add`**:
   - **Volume type:** **Azure Files**.
   - **Name:** `n8n-data`.
   - **Storage name:** `n8ndata` (o storage lógico registrado no Step 2.3).
3. Vá em **Containers** → **Edit and deploy** → selecione o container `n8n` → aba **Volume mounts** → **`+ Add`**:
   - **Volume name:** `n8n-data`.
   - **Mount path:** **`/home/node/.n8n`**.
4. **Save** → **Create** (gera uma nova revisão).

> 📸 **[PRINT 8: Volume mount `n8n-data` → `/home/node/.n8n` no container n8n]**

> **No IaC:** bloco `volumeMounts:` (`mountPath: /home/node/.n8n`) + `volumes:` em [`n8n-containerapp.yaml`](../../../infra/phase-04/n8n-containerapp.yaml). O helper [`apply-volume-mount.py`](../../../infra/phase-04/apply-volume-mount.py) faz exatamente esse patch quando o deploy roda pelo workflow.

### 3.5 Pegar o FQDN e fechar as env vars

1. **Container App → Overview** → copie o **Application Url** (algo como `https://ca-n8n-fifa2026-<iniciais>.<sufixo>.eastus2.azurecontainerapps.io`). O domínio (sem `https://`) é o seu **FQDN**.
2. **Containers → Edit and deploy** → atualize `N8N_HOST = <fqdn>` e `WEBHOOK_URL = https://<fqdn>` → **Save** → **Create**.

> 📸 **[PRINT 9: Overview do Container App com a Application Url / FQDN]**

---

## Step 4 — Verificar basic auth (AC-10)

Antes de desenhar qualquer workflow, confirme que a segurança está de pé:

1. Abra `https://<fqdn>/` no browser **sem credenciais** — deve aparecer um prompt de **basic auth** (ou retornar **401**).
2. Pelo terminal:
   ```bash
   # Sem auth → deve responder 401
   curl -s -o /dev/null -w '%{http_code}\n' https://<fqdn>/
   # Esperado: 401

   # Com auth → deve responder 200 (ou redirecionar para a UI)
   curl -s -o /dev/null -w '%{http_code}\n' -u admin:<sua-senha> https://<fqdn>/
   ```
3. No browser, entre com **`admin`** / **a senha** que você gravou no secret. A UI do n8n abre.

> 📸 **[PRINT 10: prompt de basic auth do n8n + UI aberta após login]**

> **Se NÃO pedir auth (200 sem credenciais):** as env vars `N8N_BASIC_AUTH_*` não estão aplicadas — revise o Step 3 (especialmente `N8N_BASIC_AUTH_ACTIVE=true` e o secret).

---

## Step 5 — Importar e desenhar o workflow `post-purchase-notification` (AC-5, AC-8)

Você **não precisa desenhar do zero** — importe o workflow de referência versionado (reprodutível entre aulas, mitiga o `latest`):

### 5.1 Importar o JSON de referência

1. No n8n UI: **Workflows** → menu **⋯** (ou **`+`**) → **Import from File**.
2. Selecione [`infra/phase-04/post-purchase-notification.workflow.json`](../../../infra/phase-04/post-purchase-notification.workflow.json) (baixe-o do repo do projeto).
3. O workflow aparece com **4 nodes** conectados.

> 📸 **[PRINT 11: canvas do n8n com os 4 nodes — Webhook → Switch → (HTTP VIP | Set log)]**

### 5.2 Entender o que foi importado (os 4 nodes)

| Node | Tipo | Configuração principal |
|---|---|---|
| **Webhook (purchase)** | Webhook | `POST`, path **`purchase`**, response mode `onReceived` |
| **Switch (VIP?)** | Switch | condição `{{ $json.body.category }}` **equals** `VIP` → saída VIP; `fallbackOutput` → saída padrão |
| **HTTP — VIP email (mock)** | HTTP Request | `POST https://httpbin.org/post` com body `{ notificationType: "vip-premium-email", correlationId, matchId, category }` |
| **Set — structured log** | Set (Edit Fields) | monta `{ correlationId, timestamp (`$now.toISO()`), notificationType: "standard-log", category }` |

> **Atenção (armadilha de fidelidade):** as expressões usam **`$json.body.<campo>`** — porque o n8n entrega o corpo do POST em `$json.body`. Se você redesenhar e usar `$json.category` (sem `.body`), o Switch não baterá. Importar evita isso.

### 5.3 Desenhar do zero (opcional — se a turma quiser praticar)

Se o facilitador optar por construir manualmente em vez de importar:

1. **`+`** → **Webhook** → método `POST`, path `purchase`.
2. **`+`** → **Switch** → modo Rules → regra: `{{ $json.body.category }}` *is equal to* `VIP` (saída nomeada `VIP`); habilite **fallback output** (`extra`).
3. Na saída VIP: **`+`** → **HTTP Request** → `POST` `https://httpbin.org/post` → Body: JSON com os campos acima.
4. Na saída fallback: **`+`** → **Edit Fields (Set)** → adicione `correlationId` (`{{ $json.body.correlationId }}`), `timestamp` (`{{ $now.toISO() }}`), `notificationType` (`standard-log`), `category` (`{{ $json.body.category }}`).

### 5.4 Salvar, ativar e copiar a URL do webhook

1. Clique em **Save** (nomeie `post-purchase-notification`).
2. Ative com o toggle **Active** (canto superior direito).
3. Clique no node **Webhook (purchase)** → copie a **Production URL**: `https://<fqdn>/webhook/purchase`.

> 📸 **[PRINT 12: toggle "Active" ligado + a Production URL `https://<fqdn>/webhook/purchase`]**

> **Armadilha clássica (já no troubleshooting da story):** se o workflow **não** estiver **Active**, o webhook retorna **404**. Sempre ative antes de gravar a URL.

---

## Step 6 — Conectar o consumer F1 ao n8n: App Setting `N8N_WEBHOOK_URL` (AC-6)

Este é o elo que faz o `PurchaseConsumerFunction` disparar o workflow. **A URL nunca é hardcoded** — vai num **App Setting** da Function App.

1. Portal → sua **Function App** (a do F1) → menu **Settings** → **Environment variables** → aba **App settings** → **`+ Add`**:
   - **Name:** **`N8N_WEBHOOK_URL`** ← exatamente este nome (lido por `IConfiguration["N8N_WEBHOOK_URL"]` em [`N8nWebhookNotifier.cs`](../../../src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs)).
   - **Value:** `https://<fqdn>/webhook/purchase` (a Production URL do Step 5.4).
2. **Apply** → confirme o restart da Function App.

> 📸 **[PRINT 13: App Setting `N8N_WEBHOOK_URL` na Function App com a Production URL]**

> **Por que App Setting e não código:** AC-6 (NON-NEGOTIABLE). O código lê o setting; **vazio = no-op silencioso** (a Function não quebra se a F4 não estiver configurada). Veja a lógica em `N8nWebhookNotifier.cs`.

> **Ordem obrigatória:** workflow **Active** (Step 5.4) **antes** de gravar `N8N_WEBHOOK_URL`. URL gravada com workflow inativo → o consumer dispara, mas o n8n responde 404 (apenas um `LogWarning`; a compra continua gravada — fire-and-forget).

> **Alternativa por CI/CD:** o job `deploy-function` do workflow [`deploy-phase-04.yml`](../../../.github/workflows/deploy-phase-04.yml) aplica esse App Setting a partir do secret `N8N_WEBHOOK_URL` do repositório (`az functionapp config appsettings set ... --settings "N8N_WEBHOOK_URL=..."`). No workshop fazemos manualmente no Portal para visualizar; em produção, é o pipeline.

---

## Step 7 — Smoke test ponta-a-ponta (AC-9)

Hora de ver o fluxo inteiro funcionar.

1. **Warm-up** (por causa do scale-to-zero): abra `https://<fqdn>/` (com login) para acordar o n8n antes da demo.
2. **Dispare uma compra v2** pelo caminho real (gateway YARP → PurchaseEntryFunction → Service Bus → consumer), com um **Bearer token Entra válido** (F3). Use `category: "VIP"` para exercitar a branch A:
   ```bash
   curl -X POST https://<gateway-fqdn>/purchase \
     -H "Authorization: Bearer <access_token_entra>" \
     -H "Content-Type: application/json" \
     -d '{ "matchId": 1, "category": "VIP", "userId": 1, "quantity": 1 }'
   # Esperado: 202 { "correlationId": "...", "status": "queued" }
   ```
3. **No n8n UI** → **Executions** (histórico) → veja a execução nova: payload recebido, tempo, status (sucesso). Confirme que o **`correlationId`** no log do node **Set** é o **mesmo** retornado pelo gateway.

> 📸 **[PRINT 14: aba "Executions" do n8n com a execução verde + payload contendo o correlationId]**

4. **No App Insights** (da Function App) → busque o `correlationId` → confirme que ele atravessa **todos os hops**: gateway → PurchaseEntryFunction → Service Bus → PurchaseConsumerFunction → (log "Webhook n8n disparado com sucesso").

> 📸 **[PRINT 15: App Insights — o mesmo correlationId em gateway, consumer e log do webhook n8n]**

5. **Teste a branch B:** repita com `category: "Economy"` (não-VIP) → no n8n, a execução segue o node **Set — structured log** (`notificationType: "standard-log"`).

> **Idempotência (se sobrar tempo):** reentregar a mesma mensagem (mesma compra) → o consumer detecta `Duplicate` e **não** chama o n8n de novo. No histórico do n8n: **uma** execução para aquela compra, não duas.

---

## Apêndice — Tabela de troubleshooting (da story)

| Sintoma | Causa provável | Solução |
|---|---|---|
| n8n não persiste workflows após restart | Azure Files não montado | Conferir volume mount no CAE (Step 2.3 + 3.4); confirmar FQDN da Storage Account |
| Webhook retorna **404** | Workflow não **Active** | Ativar o workflow (Step 5.4) e copiar a URL gerada |
| Consumer não dispara o n8n | `N8N_WEBHOOK_URL` não configurado | Adicionar o App Setting na Function App (Step 6) |
| n8n retorna **401** no acesso | basic auth ok, mas você está sem credenciais | Use `admin` + a senha do secret (é o comportamento esperado de segurança) |
| n8n **200 sem pedir auth** | basic auth não configurado | Revisar `N8N_BASIC_AUTH_*` (Step 3) — `N8N_BASIC_AUTH_ACTIVE=true` + secret |
| Timeout no webhook (> 5s) | n8n em **cold start** (scale-to-zero) | Warm-up 5 min antes da demo; se preciso, suba `minReplicas` para 1 temporariamente |

---

> **Próximo artefato:** [`SPEAKER-NOTES.md`](./SPEAKER-NOTES.md) — o roteiro de 6h por bloco, com armadilhas e perguntas para a turma.
