# Guia do Aluno — Aula 1 (F1: Service Bus + Functions) do zero

> **O que você vai construir nesta aula:** a **Fase 1 (F1)** do projeto FIFA 2026 Tickets — uma fila de mensagens (**Azure Service Bus**) e uma **Azure Function** que consome essa fila e grava as compras no banco. Tudo isso **no SEU próprio ambiente Azure**, criado do zero.
>
> **Importante (leia antes de começar):**
> - **Cada aluno cria TUDO no próprio Azure**: seu tenant, sua subscription, seu Resource Group, seus recursos, com **seus próprios nomes**. Não há reaproveitamento de ambiente de ninguém.
> - O **App Registration / Service Principal** é criado **no SEU tenant**. O **admin do SQL** é você.
> - Você vai fazer **fork do repositório do evento** (organização **TFTEC**) para a **sua conta** do GitHub. Tudo (Variables, Secrets, Actions) acontece **no SEU fork**. Você nunca dá push no repositório da TFTEC.

---

## Parte 0 — Fork + visão geral

### 0.1 Faça o fork do repositório

1. Acesse o repositório do evento na organização **TFTEC** (link fornecido pelo instrutor).
2. Clique em **`Fork`** (canto superior direito) → selecione **a sua conta** como destino → **`Create fork`**.
3. Pronto: agora existe uma cópia em `https://github.com/<seu-usuario>/<repo>`. **Todo o trabalho desta aula é nesse fork.**

> Você **não precisa** clonar localmente para esta aula — o deploy de código e as migrations rodam pelo **GitHub Actions** do seu fork. Clonar é opcional (só se quiser ler o código).

### 0.2 Como as peças se encaixam

Há **duas divisões de trabalho** bem distintas:

| O quê | Como é feito | Onde |
|---|---|---|
| **AMBIENTE** (RG, SQL, Service Bus, Function App, etc.) | **À mão, no Portal do Azure** | Portal (este guia) |
| **CÓDIGO + MIGRATIONS + FRONTEND** | **GitHub Actions** (workflow único `Lab Oitavas de Final`) | Seu fork |

```
                      VOCÊ (Portal Azure)                    SEU FORK (GitHub Actions)
                      ───────────────────                    ─────────────────────────
  ┌─────────────────────────────────────────────┐
  │  RG → SQL+DB → Service Bus+fila → Storage →   │
  │  Log Analytics → App Insights → Function App  │ ──┐
  │  + App Settings + SCM basic-auth              │   │  (1) você cria o ambiente vazio
  └─────────────────────────────────────────────┘   │
                                                      ▼
                              ┌───────────────────────────────────────────────┐
                              │  Workflow ÚNICO "Lab Oitavas de Final" com o    │
                              │  input `acao`:                                  │
                              │    migrations → aplica colunas no banco (idemp.)│
                              │    function   → publica a Function + smoke test │
                              │    frontend   → builda + publica o portal       │
                              │    tudo       → migrations → function → frontend │
                              └───────────────────────────────────────────────┘
                                                      │
                                                      ▼
   Fluxo em runtime:  POST /api/v2/purchase → 202 + correlationId
                      → mensagem na fila tickets-purchase
                      → Function consome → grava em purchases (source='v2')
                      → compra inválida → DLQ
```

A regra de ouro: **o Portal cria os recursos vazios; os Actions só publicam código e schema.**

---

## Convenção de nomes (preencha a SUA)

Os nomes abaixo são **seus** — escolha um prefixo pessoal (ex.: suas iniciais + `f1`) e preencha a coluna **"Seu valor"**. Anote, porque você vai reusar esses nomes o tempo todo (Portal, App Settings, Variables do GitHub).

| Recurso | Placeholder | Seu valor | Regras / observação |
|---|---|---|---|
| Subscription | `<sua-subscription>` | ________ | a sua subscription do Azure |
| Tenant ID | `<seu-tenant-id>` | ________ | Portal → **Microsoft Entra ID** → Overview → *Tenant ID* |
| Resource Group | `<seu-rg>` | ________ | você cria nesta aula |
| Região | `<sua-regiao>` | ________ | use **a mesma região para tudo** (evita latência) |
| SQL Server (logical) | `<seu-sql-server>` | ________ | único global; minúsculo; ex.: `sql-<prefixo>-001` |
| Database | `FIFA2026Tickets` | **FIFA2026Tickets** | **FIXO** — o código espera este nome |
| Service Bus namespace | `<seu-sb>` | ________ | único global; ex.: `sb-<prefixo>-001` |
| Fila | `tickets-purchase` | **tickets-purchase** | **FIXO** — o código espera este nome |
| Storage Account | `<seu-storage>` | ________ | **minúsculo, sem hífen, ≤24 chars**, único global |
| Log Analytics | `<seu-log>` | ________ | base do App Insights |
| Application Insights | `<seu-appi>` | ________ | telemetria da Function |
| Function App | `<seu-func>` | ________ | único global; ex.: `func-<prefixo>-001` |
| App Service plan | `<seu-plano>` | ________ | você cria nesta aula (B1, Windows) |

> 💡 **Dica:** nomes "globais únicos" (SQL Server, Service Bus, Storage, Function App) podem dar erro de "já existe". Se acontecer, adicione dígitos/iniciais (ex.: `-002`, `gpc`).
>
> 📋 No final do guia tem um **Apêndice com um exemplo real preenchido** (ambiente de referência validado) — use como modelo de como nomear.

---

## Referência rápida — Variáveis & Secrets do GitHub Actions

> 📌 **Tabela única de tudo que o workflow `Lab Oitavas de Final` consome.** Você **configura** esses valores ao longo das Fases 8 (migrations), 10 (function) e 12 (frontend) — esta seção é só o **mapa consolidado** para consulta/conferência. Local: seu fork → **Settings → Secrets and variables → Actions**.

### 🔑 Secrets (aba *Secrets*)

| Secret | Bloco do workflow | De onde vem | Fase do guia |
|---|---|---|---|
| `AZURE_CREDENTIALS` | migrations | JSON do Service Principal (`clientId`/`clientSecret`/`subscriptionId`/`tenantId`) | **8.1.D** |
| `SQL_CONNECTION_STRING` | migrations | connection string do banco (monte no Cloud Shell PowerShell) | **8.2** |
| `FUNCTION_PUBLISH_PROFILE` | function | publish profile da Function App (Portal *Get publish profile* ou CLI) | **10.1** |
| `AZURE_FRONTEND_PUBLISH_PROFILE` | frontend | publish profile do Web App do frontend (capture **depois** de ligar o SCM basic-auth) | **12.2** |

### 📋 Variables (aba *Variables*)

| Variable | Bloco | Valor (o SEU) | Default no workflow | Fase |
|---|---|---|---|---|
| `SQL_SERVER` | migrations | `<seu-sql-server>` (sem `.database.windows.net`) | `sql-dev-tk-cin-001` | **8.2** |
| `RESOURCE_GROUP` | migrations | `<seu-rg>` (RG do SQL) | `rg-hml-tik-cin-001` | **8.2** |
| `FUNCTION_APP_NAME` | function | `<seu-func>` | — | **10.1** |
| `FUNCTION_V2_URL` | frontend (build) | `https://<seu-func>.azurewebsites.net` (raiz, **sem** `/api`) | — | **10.1 / 12.2** |
| `FRONTEND_APP_NAME` | frontend | `<seu-frontend>` (Web App do portal) | — | **12.2** |
| `BACKEND_URL` | frontend (build) | `https://<seu-backend>.azurewebsites.net` (alimenta o proxy do `web.config`) | — | **10.3 / 12.2** |

> 💡 `VITE_API_URL` **não** é uma Variable do fork — é fixo `/api` (relativo), embutido no build. Só o `BACKEND_URL` é parametrizado por aluno (ver a lição em **10.3**).

### ⌨️ Inputs do *Run workflow* (overrides na hora de rodar)

`acao` (**obrigatório** — `tudo` / `migrations` / `function` / `frontend`) + opcionais `sql_server`, `resource_group`, `function_app_name`, `frontend_app_name` (sobrepõem as Variables só naquela execução). **Precedência:** input manual → Variable → default do workflow.

---

## Fase 1 — Resource Group

1. Portal → busque **"Resource groups"** → **`+ Create`**.
2. **Subscription:** `<sua-subscription>` · **Resource group:** `<seu-rg>` · **Region:** `<sua-regiao>`.
3. **`Review + create`** → **`Create`**.

✅ **Checkpoint:** seu RG aparece na lista. Daqui pra frente, **tudo** é criado dentro dele e na mesma região.

---

## Fase 2 — SQL Server + Database (FIFA2026Tickets)

A Function grava as compras numa tabela `purchases`. Você precisa do banco antes de tudo.

### 2.1 Criar o SQL Server (logical server)

1. Portal → **"SQL servers"** → **`+ Create`**.
2. **RG:** `<seu-rg>` · **Server name:** `<seu-sql-server>` · **Location:** `<sua-regiao>`.
3. **Authentication method:** **Use SQL authentication** (mais simples para o workshop).
   - **Server admin login:** ex.: `adminsql` · **Password:** escolha uma senha forte e **guarde** (vai virar segredo, nunca commit).
4. **`Review + create`** → **`Create`**.

### 2.2 Criar o Database

1. No servidor criado → **`+ Create database`** (ou Portal → **"SQL databases"** → **`+ Create`**).
2. **Database name:** **`FIFA2026Tickets`** (FIXO).
3. **Server:** `<seu-sql-server>` · **Compute + storage:** **Basic** ou **Serverless** (suficiente para o workshop e mais barato).
4. **`Review + create`** → **`Create`**.

### 2.3 Decisão de rede do SQL — escolha UM caminho

> ⚠️ **Decisão para o owner / instrutor:** o caminho **recomendado para alunos** é o **Público com firewall (Opção B)** — é muito mais simples e o workshop roda igual. O **Privado (Opção A)** é fiel à arquitetura de produção, mas exige VNet/Private Endpoint/DNS e mais tempo. Se a aula priorizar simplicidade, vá de **Opção B**.

#### Opção A — SQL **privado** (fiel à arquitetura de produção)

Deixa o banco sem acesso público; a Function alcança via VNet.

1. **VNet:** Portal → **"Virtual networks"** → **`+ Create`** → `<seu-rg>`, `<sua-regiao>`, espaço ex.: `10.1.0.0/16`.
2. **Subnets:** crie uma subnet para o **Private Endpoint do SQL** (ex.: `snet-sql`, `10.1.1.0/24`) e uma para a **integração da Function** (ex.: `snet-appsvc`, `10.1.2.0/24`, delegada a `Microsoft.Web/serverFarms`).
3. **Private Endpoint:** no SQL server → **Networking → Private access → `+ Create a private endpoint`** → coloque na `snet-sql` → habilite **Private DNS integration** (cria a zona `privatelink.database.windows.net`).
4. Em **Networking → Public access** do SQL: **Disable** (`Public network access = Disabled`).

> O workflow único `Lab Oitavas de Final` (ação `migrations`) **já sabe lidar com SQL privado**: ele liga o acesso público temporariamente só para o IP do runner, roda as migrations e **reverte tudo** ao final (inclusive em caso de falha).

#### Opção B — SQL **público com firewall** (mais simples — recomendado p/ alunos)

1. No SQL server → **Networking → Public access** → **Selected networks** (ou **All networks** no laboratório).
2. **Firewall rules:** marque **"Allow Azure services and resources to access this server"** (permite a Function alcançar) e adicione seu IP atual se quiser conectar pelo SSMS/Azure Data Studio.
3. `Public network access = Enabled`.

> Mesmo na Opção B, o workflow único (ação `migrations`) continua funcionando: ele abre/fecha o acesso de forma idempotente — se já estiver público, apenas garante a regra do runner e remove no final.

### 2.4 Popular o banco (schema + dados)

O banco precisa do **schema** e de **dados de referência** (seleções, estádios, jogos, categorias). Há duas formas:

- **Opção 1 — Bacpac (recomendado, traz schema + dados reais):** o arquivo `FIFA2026Tickets.bacpac` **foi removido do repositório** e é **distribuído via Azure Blob** (link/SAS fornecido pelo instrutor). Importe pelo Portal:
  - SQL server → **`Import database`** → aponte para o Storage/container onde está o `.bacpac` → **Database name:** `FIFA2026Tickets` → informe admin/senha → **`OK`**.
- **Opção 2 — Schema + seed do repositório (banco "magro"):** aplique os scripts do fork:
  - `fifa2026-api/database/schema.sql` (tabelas + FKs + índices)
  - `fifa2026-api/database/seed-admin.sql` (usuário admin)
  - Demais seeds de dados estão em `fifa2026-api/database/migrations/` (ex.: `2026-05-08-group-stage-72.sql`, `2026-05-08-real-fifa-prices.sql`, etc.).
  - Aplique via **Azure Data Studio / SSMS** conectado ao banco, ou via `sqlcmd`:
    ```bash
    sqlcmd -S <seu-sql-server>.database.windows.net -U adminsql -P <senha> -d FIFA2026Tickets -i schema.sql
    sqlcmd -S <seu-sql-server>.database.windows.net -U adminsql -P <senha> -d FIFA2026Tickets -i seed-admin.sql
    ```

> As **3 colunas que a F1 precisa** (`source`, `correlation_id`, `entra_oid`) **NÃO** entram aqui — elas são aplicadas depois, na **Fase 8 (migrations via Actions)**.

✅ **Checkpoint:** banco `FIFA2026Tickets` criado e populado; você consegue conectar e ver as tabelas (`matches`, `ticket_categories`, `purchases`, etc.).

---

## Fase 3 — App Service plan (B1, Windows)

A Function vai rodar num plano dedicado (não Consumption), igual à arquitetura do projeto.

1. Portal → **"App Service plans"** → **`+ Create`**.
2. **RG:** `<seu-rg>` · **Name:** `<seu-plano>` · **Region:** `<sua-regiao>`.
3. **Operating System:** **Windows** · **Pricing plan:** **B1** (Basic).
4. **`Review + create`** → **`Create`**.

✅ **Checkpoint:** plano B1 Windows criado no seu RG.

---

## Fase 4 — Service Bus (a fila)

### 4.1 Criar o Namespace

1. Portal → **"Service Bus"** → **`+ Create`**.
2. **Subscription:** `<sua-subscription>` · **Resource group:** `<seu-rg>`.
3. **Namespace name:** `<seu-sb>` · **Location:** `<sua-regiao>`.
4. **Pricing tier:** **Standard** ⚠️ (não Basic — Basic não suporta tópicos nem alguns recursos usados).
5. **`Review + create`** → **`Create`** → **`Go to resource`**.

### 4.2 Criar a fila

1. No namespace → **Entities → Queues** → **`+ Queue`**.
2. **Name:** `tickets-purchase` (FIXO).
3. **Max delivery count:** `10` · **Lock duration:** `30` segundos.
4. **`Create`**. *(A DLQ — dead-letter queue — é criada automaticamente.)*

✅ **Checkpoint:** fila `tickets-purchase` listada. *(A connection string a gente pega na Fase 9.)*

---

## Fase 5 — Storage Account

A Function precisa de um Storage para estado interno (triggers, locks, logs do host).

1. Portal → **"Storage accounts"** → **`+ Create`**.
2. **RG:** `<seu-rg>` · **Name:** `<seu-storage>` (minúsculo, sem hífen, ≤24 chars) · **Region:** `<sua-regiao>`.
3. **Performance:** Standard · **Redundancy:** **LRS** (mais barato, suficiente).
4. **`Review + create`** → **`Create`**.

✅ **Checkpoint:** Storage criado no seu RG.

---

## Fase 6 — Log Analytics + Application Insights

### 6.1 Log Analytics Workspace

1. Portal → **"Log Analytics workspaces"** → **`+ Create`**.
2. **RG:** `<seu-rg>` · **Name:** `<seu-log>` · **Region:** `<sua-regiao>` → **`Review + create`** → **`Create`**.

### 6.2 Application Insights

1. Portal → **"Application Insights"** → **`+ Create`**.
2. **RG:** `<seu-rg>` · **Name:** `<seu-appi>` · **Region:** `<sua-regiao>`.
3. **Workspace:** selecione `<seu-log>` (criado acima) → **`Review + create`** → **`Create`**.

✅ **Checkpoint:** Log Analytics + App Insights criados. *(Como vamos usar isso está detalhado na Fase 12.)*

---

## Fase 7 — Function App (.NET 8 isolated, Windows, no plano B1)

### 7.1 Criar a Function App

1. Portal → **"Function App"** → **`+ Create`** → escolha o tipo de hospedagem **"App Service plan"** (não Consumption, não Flex).
2. **Basics:**
   - **RG:** `<seu-rg>`
   - **Function App name:** `<seu-func>`
   - **Do you want to deploy code or container?** **Code**
   - **Runtime stack:** **.NET** · **Version:** **8 (isolated)**
   - **Region:** `<sua-regiao>`
   - **Operating System:** **Windows** (mesmo do plano B1)
3. **Hosting / Plan:**
   - **App Service plan:** selecione o seu **`<seu-plano>`** (não crie outro).
4. **Storage:** selecione `<seu-storage>`.
5. **Monitoring:** Application Insights = **Yes** → `<seu-appi>`.
6. **`Review + create`** → **`Create`**.

> **Alternativa — criar a Function App via Cloud Shell (PowerShell).** Se preferir CLI ao Portal, abra o **Cloud Shell** no modo **PowerShell** e rode o bloco abaixo. Pré-requisito: o **App Service plan** (Fase 3), o **Storage** (Fase 5) e o **Application Insights** (Fase 6) já criados.
>
> Em vez de digitar os nomes à mão (e arriscar erro de digitação), **descubra** automaticamente os nomes dos recursos já criados no seu RG e confira antes de criar a Function. Você só precisa digitar **dois** valores: o **RG** e o **nome novo** da Function App.
> ```powershell
> # --- Você digita só estes dois ---
> $rg   = "<seu-rg>"        # o RG onde você criou tudo (Fases 1-6)
> $func = "<seu-func>"      # nome GLOBAL único da Function App (ainda NÃO existe — você escolhe)
>
> # --- Descobre os nomes dos recursos existentes no RG ---
> $loc     = az group show -n $rg --query location -o tsv
> $plano   = az appservice plan list -g $rg --query "[0].name" -o tsv
> $storage = az storage account list -g $rg --query "[0].name" -o tsv
> $appi    = az resource list -g $rg --resource-type microsoft.insights/components --query "[0].name" -o tsv
> $sql     = az sql server list -g $rg --query "[0].name" -o tsv
> $sb      = az servicebus namespace list -g $rg --query "[0].name" -o tsv
>
> # --- Confere o que foi descoberto ANTES de criar (jeito PowerShell, sem printf) ---
> [pscustomobject]@{ RG=$rg; LOC=$loc; PLAN=$plano; STORAGE=$storage; APPI=$appi; SQL=$sql; SB=$sb } | Format-List
>
> # --- Cria a Function App no plano B1 (Windows, .NET 8 isolated, Functions v4) ---
> az functionapp create `
>   --resource-group $rg `
>   --name $func `
>   --plan $plano `
>   --storage-account $storage `
>   --app-insights $appi `
>   --runtime dotnet-isolated `
>   --runtime-version 8 `
>   --functions-version 4 `
>   --os-type Windows
>
> # 7.3 — Always On (necessário p/ o trigger do Service Bus em plano dedicado)
> az functionapp config set --resource-group $rg --name $func --always-on true
>
> # 7.4 — SCM Basic Auth On (necessário p/ o deploy via Actions / publish profile)
> az resource update `
>   --resource-group $rg `
>   --namespace Microsoft.Web `
>   --resource-type basicPublishingCredentialsPolicies `
>   --name scm --parent "sites/$func" `
>   --set properties.allow=true
> ```
> A etapa de **descoberta** (`az ... list --query`) só funciona se os recursos das Fases 3/5/6 **já existirem** no `$rg` — ela apenas **lê** os nomes; `$func` é o único que você escolhe (a Function ainda não existe). Confira a saída do `Format-List`: se algum campo vier **vazio**, o recurso correspondente não está no RG (revise a fase). Esse bloco cobre, de uma vez, a criação (7.1) **e** as configurações de **Always On** (7.3) e **SCM basic-auth** (7.4). Se for **Opção A (SQL privado)**, ainda faça a VNet integration da 7.2 (abaixo).

### 7.2 (Somente Opção A — SQL privado) Ligar a Function na VNet

> Pule esta etapa se você escolheu o **SQL público (Opção B)**.

1. Abra a `<seu-func>` → menu **Networking**.
2. Em **Outbound traffic / VNet integration** → **Add VNet integration**.
3. **VNet:** sua VNet · **Subnet:** a subnet do App Service (`snet-appsvc`) → **`Connect`**.
4. Confirme que `WEBSITE_VNET_ROUTE_ALL` fica habilitado (roteia o tráfego de saída pela VNet → alcança o SQL privado).

### 7.3 Ligar o "Always On"

1. Function → **Settings → Configuration → General settings**.
2. **Always On:** **On** → **`Save`**. *(Necessário para o gatilho do Service Bus funcionar em plano dedicado — sem isso, a Function "dorme" e não consome a fila.)*

### 7.4 Habilitar o SCM Basic Auth (necessário para o deploy via Actions)

1. Function → **Settings → Configuration → General settings**.
2. **SCM Basic Auth Publishing Credentials:** **On** → **`Save`**.

> Sem isso, o deploy da Function (ação `function` do workflow único) falha com **401** ao publicar via publish profile.

✅ **Checkpoint:** Function criada no plano B1, Always On ligado, SCM basic-auth ligado (e VNet integration se for Opção A).

---

## Fase 8 — Migrations do banco (via GitHub Actions)

A Function consumer grava em `purchases` usando colunas que **ainda não existem** no banco recém-criado:
`source`, `correlation_id` (migration `phase-01.sql`) e `entra_oid` (migration `phase-03.sql` — **obrigatória mesmo na F1**, senão o `INSERT` falha e a mensagem cai na DLQ).

Os scripts estão em `fifa2026-api/database/migrations/phase-01.sql` e `phase-03.sql` — **aditivos e idempotentes** (rodar de novo não causa efeito colateral).

> **Por que via Actions e não na mão?** Se você escolheu **SQL privado (Opção A)**, um runner do GitHub (internet pública) não alcança o banco. O workflow único `Lab Oitavas de Final` (ação `migrations`) resolve isso de forma reproduzível: liga o acesso público + abre o firewall **só para o IP do runner**, roda as migrations e **reverte tudo** (remove a regra + desliga o público), **mesmo em caso de falha**. É um passo **pré-workshop** (roda uma vez por ambiente). No **SQL público (Opção B)** o mesmo workflow também funciona, apenas garantindo/removendo a regra do runner.

### 8.1 Pré-requisito — Service Principal (App Registration) pelo Portal (no SEU tenant)

O workflow precisa de uma credencial Azure para ligar/desligar o acesso ao SQL. Crie via Portal (sem CLI), **no seu próprio tenant**:

**A) Registrar o app (Microsoft Entra ID)**
1. Portal → **Microsoft Entra ID** → **App registrations** → **`+ New registration`**.
2. **Name:** `sp-fifa2026-migrate` · **Supported account types:** *Single tenant* → **`Register`**.
3. Na **Overview**, copie **Application (client) ID** e **Directory (tenant) ID**.

**B) Criar o client secret**
1. No app → **Certificates & secrets** → **`+ New client secret`** → descrição + expiração → **`Add`**.
2. **Copie na hora o `Value`** do secret (ele some depois que você sai da tela).

**C) Dar permissão no Resource Group**
1. Portal → RG **`<seu-rg>`** → **Access control (IAM)** → **`+ Add` → `Add role assignment`**.
2. **Role:** **Contributor** (ou, mais estreito, **SQL Server Contributor**) → **`Next`**.
3. **Assign access to:** *User, group, or service principal* → **`+ Select members`** → busque `sp-fifa2026-migrate` → selecione → **`Review + assign`**.

**D) Montar o JSON do `AZURE_CREDENTIALS`**
Com os valores acima + o **Subscription ID** (Portal → **Subscriptions**), monte o JSON que vai no secret:
```json
{
  "clientId": "<Application (client) ID>",
  "clientSecret": "<Value do client secret>",
  "subscriptionId": "<Subscription ID>",
  "tenantId": "<Directory (tenant) ID>"
}
```

> Em produção real, prefira **OIDC / Federated Credential** em vez de client secret de longa duração.

### 8.2 Configurar Secrets + Variables no fork

No **seu fork** → **Settings → Secrets and variables → Actions**:

| Tipo | Nome | O que é | Onde você pega o SEU valor |
|---|---|---|---|
| Secret | `AZURE_CREDENTIALS` | JSON do Service Principal | passo 8.1.D |
| Secret | `SQL_CONNECTION_STRING` | connection string do banco | monte conforme o bloco abaixo |
| Variable | `SQL_SERVER` | nome do SQL server (sem sufixo) | `<seu-sql-server>` |
| Variable | `RESOURCE_GROUP` | RG do SQL | `<seu-rg>` |

> ⚠️ A senha entra **só** no secret `SQL_CONNECTION_STRING` — **nunca** commitada no código.

> **Montar a `SQL_CONNECTION_STRING` (Cloud Shell PowerShell):** abra o **Cloud Shell** no Portal em modo **PowerShell** e monte a string (substitua server/senha):
> ```powershell
> $server = "<seu-sql-server>"
> $senha  = "<senha-do-adminsql>"
> "Server=$server.database.windows.net,1433;Database=FIFA2026Tickets;User Id=adminsql;Password=$senha;Encrypt=true;TrustServerCertificate=true"
> ```
> Copie a saída e cole no secret `SQL_CONNECTION_STRING`.

### 8.3 Rodar o workflow

No seu fork → **Actions → "Lab Oitavas de Final" → `Run workflow`** → em **`acao`** escolha **`migrations`** (escolha a branch `main`).

> 🖱️ **Disparo manual apenas:** este workflow **não roda sozinho** (só tem `workflow_dispatch`). Você precisa clicar em **Run workflow** explicitamente e escolher a ação.

O workflow (ação `migrations`) faz: `az login` (SP) → liga público + abre firewall do runner → aplica `phase-01.sql` e `phase-03.sql` (via `azure/sql-action`, que entende os batches `GO`) → **reverte** o acesso. Confira no log dos steps `[migrations]` as colunas `source`, `correlation_id`, `entra_oid` e os índices `UQ_purchases_correlation_id` / `IX_purchases_entra_oid`.

✅ **Checkpoint:** workflow verde; as 3 colunas existem na tabela `purchases`.

---

## Fase 9 — App Settings da Function (parametrização via Portal)

> Function → **Settings → Environment variables / Application settings** → adicionar cada uma → **`Save`**.

| Nome do App Setting | Valor | De onde vem |
|---|---|---|
| `ServiceBusConnection` | connection string do namespace `<seu-sb>` **SEM `EntityPath`** | Service Bus → **Shared access policies** → `RootManageSharedAccessKey` → Primary Connection String |
| `SqlConnectionString` | `Server=<seu-sql-server>.database.windows.net,1433;Database=FIFA2026Tickets;User Id=adminsql;Password=<senha>;Encrypt=true;TrustServerCertificate=true` | a mesma do banco que você criou |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` | fixo |
| `FUNCTIONS_EXTENSION_VERSION` | `~4` | fixo |

> ⚠️ **Armadilha do `EntityPath`:** copie a connection string **do namespace** (RootManageSharedAccessKey), **não** da fila. Se vier `;EntityPath=tickets-purchase` no final, **remova** essa parte — senão o trigger do Service Bus não liga corretamente.
>
> ⚠️ **Segredo:** a senha do banco entra só aqui, no App Setting (ou, idealmente, como referência a um Key Vault). Nunca commit.

✅ **Checkpoint:** 4 App Settings salvos; a Function reinicia sozinha.

---

## Fase 10 — Deploy do código (GitHub Actions)

> Esta é a **única** parte de código. Não publique pelo Portal — use o workflow da fase.

### 10.1 Configurar a publicação no fork

No **seu fork** → **Settings → Secrets and variables → Actions**:

| Tipo | Nome | O que é | Onde você pega o SEU valor |
|---|---|---|---|
| Variable | `FUNCTION_APP_NAME` | nome da Function App de destino | `<seu-func>` |
| Variable | `FUNCTION_V2_URL` | URL **raiz** da Function (sem `/api`) — embutida no frontend para a compra v2 async | `https://<seu-func>.azurewebsites.net` |
| Secret | `FUNCTION_PUBLISH_PROFILE` | publish profile da Function | pelo Portal (**Overview → `Get publish profile`**) ou via Cloud Shell PowerShell (bloco abaixo) |

> Garanta que o **SCM Basic Auth** está **On** na Function (Fase 7.4) — senão a action retorna **401**.

> ⚠️ **CORS na Function (compra v2 do navegador):** no fluxo das Oitavas o **navegador chama a Function direto** (URL `FUNCTION_V2_URL`), então a Function precisa permitir a origem do frontend. Pelo Portal: Function → **API → CORS** → adicione `https://<seu-frontend>.azurewebsites.net` em **Allowed Origins** → **`Save`**. (Via CLI: `az functionapp cors add -g <seu-rg> -n <seu-func> --allowed-origins "https://<seu-frontend>.azurewebsites.net"`.) Sem isso, a compra v2 falha no browser com erro de CORS.

> **Pegar o publish profile (Cloud Shell PowerShell):** abra o **Cloud Shell** no modo **PowerShell** e rode (substitua RG e Function App):
> ```powershell
> az functionapp deployment list-publishing-profiles `
>   -g "<seu-rg>" -n "<seu-func>" --xml
> ```
> Copie **todo** o XML retornado e cole no secret `FUNCTION_PUBLISH_PROFILE`.

### 10.2 Disparar o deploy

No seu fork → **Actions → "Lab Oitavas de Final" → `Run workflow`** → em **`acao`** escolha **`function`** (branch `main`).
O workflow (ação `function`) faz: restore → build → test → publish → deploy → **smoke test** (`POST /api/v2/purchase`, valida que a resposta tem `.correlationId`).

> 🖱️ **Disparo manual apenas:** este workflow **não roda sozinho** (só tem `workflow_dispatch`). Nada é publicado até você clicar em **Run workflow** e escolher a ação.

✅ **Checkpoint:** workflow verde; o step **"[function] Smoke test (AC-10)"** mostra `Smoke test OK — .correlationId presente`.

### 10.3 Deploy do frontend (o portal) — e a lição do `VITE_API_URL`

Rode o workflow de novo com **`acao=frontend`** (ou `tudo`). O step `[frontend]` builda o Vite e publica o portal no Web App do frontend.

> ⚠️ **Pré-requisito (igual à Function, Fase 7.4):** ligue o **SCM Basic Auth `On`** no App Service do **frontend** e capture o secret `AZURE_FRONTEND_PUBLISH_PROFILE` **depois** de ligar — senão o deploy falha com `Publish profile is invalid for app-name`.

> 🧭 **Lição de conectividade — `VITE_API_URL=/api` (RELATIVO), nunca a URL absoluta do backend.**
>
> O navegador do aluno **não alcança o backend diretamente** quando o backend é **privado** (`publicNetworkAccess=Disabled`, atrás de VNet/Private Endpoint): uma chamada à URL **absoluta** `https://<seu-backend>.azurewebsites.net/api` resulta em **`Failed to fetch`** no browser e a lista de jogos vem **vazia** ("0 jogos encontrados").
>
> O caminho correto é **same-origin**: o bundle chama **`/api`** (relativo) → o **`web.config`** do frontend faz o **proxy reverso** para o backend, *server-side*, através da VNet (`^api/(.*)` → `__BACKEND_URL__/api/{R:1}`).
>
> Por isso o build define **`VITE_API_URL: /api`** (relativo). E isto **continua parametrizável** — o nome real do backend vive na Variable **`BACKEND_URL`**, que o `scripts/set-backend-url.mjs` injeta no `web.config` (`__BACKEND_URL__` → sua URL):
> - **`VITE_API_URL=/api`** → fixo e relativo (não é URL, é caminho same-origin; **igual para todo aluno**).
> - **`BACKEND_URL=https://<seu-backend>…`** → **parametrizado por aluno** (alimenta o proxy do `web.config`).
>
> ❌ **Nunca** usar a URL absoluta em `VITE_API_URL` (ex.: `${{ vars.BACKEND_URL }}/api`): isso **embute** o endereço do backend no JS e só funciona se o backend for **público**. Com backend privado, **quebra** (matches vazio). A regra: o aluno parametriza **só** `BACKEND_URL`; o `/api` nunca muda.

✅ **Checkpoint:** abra `https://<seu-frontend>.azurewebsites.net/matches` → a lista de **jogos carrega** (não "0 jogos encontrados"). Se vier vazia, confira nesta ordem: (1) `VITE_API_URL` está `/api` (relativo, não absoluto)? (2) a Variable `BACKEND_URL` aponta para o **seu** backend? (3) o frontend está **integrado à VNet** para alcançar o backend privado? (4) no DevTools (F12 → Network), a chamada de `matches` sai do **mesmo host do frontend** (`<seu-frontend>/api/matches`) e retorna **200**.

---

## Fase 11 — Application Insights: o que é e como vamos usar

### 11.1 O que é

**Application Insights (App Insights)** é o serviço de **APM (Application Performance Monitoring) / telemetria** do Azure. Ele coleta automaticamente, da sua Function: **requisições, dependências (chamadas ao SQL e ao Service Bus), exceções, logs e métricas de performance**. Os dados ficam guardados no **Log Analytics Workspace** (`<seu-log>`) que você criou na Fase 6, e podem ser consultados com a linguagem **KQL** (Kusto Query Language).

### 11.2 Por que ele entra na F1

A F1 é **assíncrona**: você faz um `POST` e recebe `202 Accepted` — mas o trabalho de verdade (consumir a fila e gravar no banco) acontece **depois, em background**, dentro da Function. Sem telemetria, você fica **cego**: não sabe se a mensagem foi consumida, se o `INSERT` no banco falhou, ou se a compra inválida caiu na DLQ. O App Insights é a **janela** para enxergar esse fluxo invisível.

### 11.3 Como vamos usar (Portal → seu App Insights `<seu-appi>`)

- **Live Metrics** (menu **Investigate → Live Metrics**): painel em **tempo real**. Dispare um `POST /api/v2/purchase` e veja a Function "acordar", processar a mensagem e as dependências (SQL) acenderem ao vivo. Ótimo para o checkpoint do workshop.
- **Transaction search** (menu **Investigate → Transaction search**): busca individual de execuções. Pesquise por uma execução e abra a **timeline** dela — você vê a chamada ao Service Bus, a dependência do SQL e quanto tempo cada etapa levou.
- **Failures** (menu **Investigate → Failures**): agrupa as **exceções**. Quando uma compra inválida cai na DLQ, a exceção que causou isso aparece aqui.
- **Logs / KQL** (menu **Monitoring → Logs**): consultas livres. Exemplos úteis para acompanhar a F1:
  ```kusto
  // Execuções da Function nos últimos 30 min
  requests
  | where timestamp > ago(30m)
  | order by timestamp desc

  // Rastrear uma compra pelo correlationId (o rastro ponta a ponta)
  union traces, requests, dependencies, exceptions
  | where customDimensions.correlationId == "<correlationId-da-resposta>"
  | order by timestamp asc

  // Falhas que provavelmente foram para a DLQ
  exceptions
  | where timestamp > ago(1h)
  | order by timestamp desc
  ```

> 🔎 **correlationId:** cada compra recebe um `correlationId` (devolvido na resposta `202`). Esse mesmo id é propagado pela mensagem e gravado na coluna `correlation_id` do banco — é a sua "chave de rastreamento" para seguir uma compra do `POST` até o `INSERT` (ou até a DLQ) no App Insights.

✅ **Checkpoint:** você consegue abrir o Live Metrics e ver atividade quando dispara um `POST`.

---

## Fase 12 — Checkpoint final (teste ponta a ponta)

### 12.1 — Backend: compra **single** via curl

Dispare uma compra válida (ajuste a URL para a SUA Function):

```bash
curl -sS "https://<seu-func>.azurewebsites.net/api/v2/purchase" \
  -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
```

✅ **Tudo certo se:**
1. A resposta é **`202`** com um **`correlationId`** no corpo.
2. A mensagem **aparece e é consumida** na fila `tickets-purchase` (Service Bus → Queues → métricas; ou Live Metrics do App Insights).
3. A tabela **`purchases`** recebe um registro novo com **`source='v2'`** e o `correlation_id` da resposta.
4. Uma compra **inválida** (ex.: `matchId` inexistente) vai para a **DLQ** após as tentativas de entrega (max delivery count = 10), e a exceção aparece em **App Insights → Failures**.

### 12.2 — Compra v2 **multi-item** (carrinho inteiro) — fan-out no Service Bus

> Esta é a **melhoria visível** das Oitavas: o fluxo v2 processa o **carrinho inteiro** (N linhas), não só 1 ingresso. Um único `POST` vira **N mensagens** no Service Bus (**fan-out**) — cada linha gravada como uma compra com seu próprio `correlationId`, todas compartilhando o mesmo `orderId` (o protocolo do pedido).

**Pré-requisito — deploy do frontend.** Rode o workflow com **`acao=frontend`** (ou `tudo`). Vars/secrets do frontend no fork: `FRONTEND_APP_NAME`, `BACKEND_URL`, `FUNCTION_V2_URL` (Variables) + `AZURE_FRONTEND_PUBLISH_PROFILE` (Secret).

> ⚠️ **Mesma lição do basic-auth da Function (Fase 7.4) — agora no frontend.** Garanta o **SCM Basic Auth `On`** também no **App Service do frontend** (`<seu-frontend>`) e capture o publish profile `AZURE_FRONTEND_PUBLISH_PROFILE` **DEPOIS** de ligar o basic-auth. Se o profile foi pego com o basic-auth `Off`, o deploy falha com `##[error]Deployment Failed, Error: Publish profile is invalid for app-name and slot-name provided`. Correção: ligue o basic-auth, **recapture** o publish profile (Portal → App Service do front → `Get publish profile`, ou `az webapp deployment list-publishing-profiles -g <seu-rg> -n <seu-frontend> --xml`), atualize o secret e reode `acao=frontend`.

**Teste via API (carrinho de 2 linhas):**

```bash
curl -sS "https://<seu-func>.azurewebsites.net/api/v2/purchase" \
  -H "Content-Type: application/json" \
  -d '{"userId":1,"items":[{"matchId":1,"category":"VIP","quantity":2},{"matchId":2,"category":"Cat1","quantity":1}]}'
```

✅ **Tudo certo se:**
1. A resposta é **`202`** com **`orderId`**, **`status:"queued"`** e um array **`correlationIds`** com **2** GUIDs **distintos** (1 por linha). O campo singular `correlationId` vem **`null`** — ele só aparece quando o carrinho tem **1** linha (backward-compat do smoke).
2. As **2 mensagens** aparecem e são consumidas na fila `tickets-purchase` (fan-out real).
3. A tabela **`purchases`** recebe **2 registros** novos (`source='v2'`), um por linha, cada um com seu `correlation_id`.
4. **Backward-compat:** o teste single da **12.1** (`{matchId,category,userId,quantity}`) **continua** retornando `202` com `correlationId` (singular) presente.

**Teste pelo app (browser):** abra `https://<seu-frontend>.azurewebsites.net`, faça login, **adicione 2+ jogos ao carrinho** e finalize a compra. Você verá o **protocolo (`orderId`)**, a tela de recibo e o **polling** de status até a confirmação de todas as linhas.

**Rastrear o pedido no App Insights (Logs / KQL):** todas as linhas do mesmo carrinho compartilham o `orderId` no escopo de log:

```kusto
// Todas as linhas de um pedido (carrinho) pelo orderId
union traces, requests, dependencies
| where customDimensions.OrderId == "<orderId-da-resposta>"
| order by timestamp asc
```

> 🔎 **orderId vs correlationId:** o `orderId` agrupa o **pedido** (carrinho inteiro); cada `correlationId` rastreia **uma linha** do pedido. **Não há** tabela/coluna `order_id` no banco — o `orderId` vive na mensagem e nos logs (rastreabilidade), por design (sem migration).

---

## Fase 13 — Validar o fan-out multi-item no Portal (Service Bus + App Insights)

> Esta fase mostra **como provar, no Portal do Azure**, que uma compra de carrinho com **N itens** gera **N mensagens** no Service Bus e **N gravações** no banco — o coração da feature "Oitavas". Faça uma compra real de **2 jogos** no portal (`https://<seu-frontend>` — use o **custom domain**, se houver) e acompanhe.

### 13.1 — A "impressão digital" de uma compra de 2 itens

Uma compra de carrinho com 2 linhas deixa este rastro (exatamente o validado no ambiente de referência em 2026-06-25):

| Camada | O que aparece | Qtde (carrinho de 2) |
|---|---|---|
| HTTP (resposta 202) | `{ orderId, correlationIds:[2], correlationId:null }` | 1 orderId, 2 correlationIds |
| **Service Bus** | mensagens publicadas em `tickets-purchase` | **2 Incoming** |
| **Service Bus** | mensagens consumidas | **2 Outgoing** |
| **App Insights** (requests) | `PurchaseEntryFunction` (POST) | 1 |
| **App Insights** (requests) | `PurchaseConsumerFunction` (trigger Service Bus) | **2** ← o fan-out |
| **App Insights** (requests) | `PurchaseStatusFunction` (GET, polling) | 2 (1 por correlationId) |
| **App Insights** (dependencies) | envio p/ `tickets-purchase` (Queue Message) | 2 |
| **App Insights** (dependencies) | `INSERT`/`SELECT` em `FIFA2026Tickets` (SQL) | 2+ |

### 13.2 — No Portal: **Service Bus**

1. Portal → seu **Service Bus namespace** (`<seu-sb>`) → **Entities → Queues → `tickets-purchase`**.
2. **Overview** da fila — mostre aos alunos:
   - **Active message count = 0** após a compra → o **consumidor drenou** a fila (mensagens processadas).
   - **Dead-letter message count** → mensagens que falharam (ex.: compra inválida) param aqui após **10 tentativas** (Max Delivery Count). Ótimo para demonstrar o caminho de falha.
3. **Metrics** (Monitoring → Metrics): adicione **Incoming Messages** e **Outgoing Messages** (agregação **Total**), com escopo na fila `tickets-purchase`. Dispare a compra de 2 itens e mostre **+2 em Incoming** e **+2 em Outgoing** → produção e consumo batendo.

> 💡 As mensagens são consumidas em ~1s, então **não ficam paradas** para "peek". O que prova o fan-out é **Incoming/Outgoing = N** (métricas) + os **N PurchaseConsumerFunction** no App Insights (abaixo), não o conteúdo parado na fila.

### 13.3 — No Portal: **Application Insights** (`<seu-appi>`)

- **Application Map** (Investigate → Application Map): topologia automática — Function → **Service Bus (`tickets-purchase`)** → **SQL (`FIFA2026Tickets`)**. Visual perfeito para a aula.
- **Transaction search** (Investigate → Transaction search): filtre pelo período da compra e veja, em sequência:
  - 1× `PurchaseEntryFunction` (POST `/api/v2/purchase`)
  - **2× `PurchaseConsumerFunction`** (trigger Service Bus, `message_bus.destination = tickets-purchase`) ← **o fan-out**
  - 2× `PurchaseStatusFunction` (GET `/api/v2/purchase/{correlationId}` — o HttpPath traz o correlationId de cada linha)
  Clique numa transação → **timeline** com as dependências (Service Bus + SQL).
- **Live Metrics** (Investigate → Live Metrics): abra **antes** de comprar e dispare ao vivo — as Functions "acendem" em tempo real.
- **Logs (KQL)** (Monitoring → Logs) — queries prontas:

```kusto
// 1) A "impressão digital": 1 entry → N consumers → N status
requests
| where timestamp > ago(30m)
| where name startswith "Purchase"
| project timestamp, name, success, http=tostring(customDimensions.HttpPath)
| order by timestamp asc
```

```kusto
// 2) O fan-out: nº de mensagens consumidas = nº de itens do carrinho
requests
| where timestamp > ago(30m) and name == "PurchaseConsumerFunction"
| summarize mensagens_consumidas = count()
```

```kusto
// 3) Dependências: envio ao Service Bus + gravação no SQL
dependencies
| where timestamp > ago(30m)
| where target has "tickets-purchase" or target has "FIFA2026Tickets"
| summarize chamadas = count() by type, target
```

```kusto
// 4) Rastro de APLICAÇÃO por pedido (logs do worker com BeginScope):
//    entry (1 log por linha do carrinho) → consumer (processando + gravado).
//    Troque <orderId> pelo valor da resposta 202.
traces
| where timestamp > ago(30m)
| where tostring(customDimensions.OrderId) == "<orderId>"
   or tostring(customDimensions.CorrelationId) in ("<correlationId-1>", "<correlationId-2>")
| project timestamp, message, oid=tostring(customDimensions.OrderId), cid=tostring(customDimensions.CorrelationId)
| order by timestamp asc
```

### 13.4 — Features validadas (✅ ambiente de referência, 2026-06-25)

| Feature | Como foi provado |
|---|---|
| **Compra multi-item (fan-out)** | Carrinho de 2 linhas → `orderId` único + 2 `correlationIds` distintos → **2 PurchaseConsumerFunction** + **2 Incoming** + **2 SQL**. |
| **Cada linha = 1 compra** | 2 status `completed` (PurchaseStatusFunction lê `purchases` por `correlation_id`). |
| **Backward-compat (1 item)** | Shape legado `{matchId,category,userId,quantity}` → 202 com `correlationId` singular (smoke do workflow). |
| **Idempotência** | UNIQUE `correlation_id` no banco; reentrega não duplica. |
| **Caminho de falha** | Compra inválida → **DLQ** após 10 tentativas (visível no Overview da fila). |

> 🛠️ **Gotcha do isolated worker (resolvido):** por padrão, o `.NET isolated` **descarta os logs `Information` do worker** no provider do Application Insights — o `AddApplicationInsightsTelemetryWorkerService()` instala uma regra de filtro que limita o provider a **Warning**. Por isso os `ILogger.LogInformation` com `BeginScope` de `OrderId`/`correlationId` **não apareciam** (só request/dependency do host apareciam). O `host.json` **não** controla isso. O fix está no `Program.cs` — um `.ConfigureLogging(...)` que **remove** essa regra (`ProviderName == "...ApplicationInsightsLoggerProvider"`). Depois disso, a query (4) acima retorna o rastro de aplicação ponta-a-ponta. *(Validado no ambiente de referência em 2026-06-25.)*

---

## Resumo do que você criou nesta aula

| Camada | Recursos criados (todos no SEU `<seu-rg>`) |
|---|---|
| Dados | SQL Server `<seu-sql-server>` + DB `FIFA2026Tickets` (schema + seed + 3 colunas da F1) |
| Mensageria | Service Bus `<seu-sb>` (Standard) + fila `tickets-purchase` (+ DLQ) |
| Compute | App Service plan `<seu-plano>` (B1) + Function App `<seu-func>` (.NET 8 isolated) |
| Apoio | Storage `<seu-storage>` |
| Observabilidade | Log Analytics `<seu-log>` + App Insights `<seu-appi>` |
| Identidade | App Registration `sp-fifa2026-migrate` (no seu tenant) |
| Automação | Fork configurado: Variables + Secrets + workflow único `Lab Oitavas de Final` (ação `migrations` / `function` / `frontend` / `tudo`) |

---

## Apêndice — Exemplo concreto (ambiente de referência validado em 2026-06-24)

Estes foram os **nomes reais** usados no ambiente de referência que **funcionou ponta a ponta** — use como **modelo de preenchimento** da tabela de convenção de nomes (não como valores a copiar: cada aluno cria os seus).

| Recurso | Valor de referência |
|---|---|
| Subscription | `SUBS - HML` (id `d970133e-…`) |
| Resource Group | `rg-hml-tik-cin-001` |
| Região | **Central India** |
| SQL Server | `sql-dev-tk-cin-001` |
| Database | `FIFA2026Tickets` (FIXO) |
| Service Bus namespace | `sb-dev-tk-cin-001` |
| Fila | `tickets-purchase` (FIXO) |
| Storage Account | `stdevtkcin001` |
| Log Analytics | `log-dev-tk-cin-001` |
| Application Insights | `appi-dev-tk-cin-001` |
| Function App | `func-dev-tk-cin-001` |
| App Service plan | `asp-prd-tk-cin-001` (B1, Windows) |

> Note o padrão **CAF** (Cloud Adoption Framework): `<tipo>-<ambiente>-<projeto>-<região>-<instância>`. Você não precisa seguir exatamente esse padrão — só seja consistente e use um prefixo seu.
