# F5 — Portal Guide: provisionar o MCP Server + chatbot Gemini

> **Guia de execução passo-a-passo** · Workshop "Living Lab Azure-Native" · Fase 5 de 6
> **Story:** [2.5](../../stories/2.5.story.md) · **Arquitetura:** [ADE-002](../../architecture/ade-002-mcp-pinning.md) · **Workflow CI/CD:** [`.github/workflows/deploy-phase-05.yml`](../../../.github/workflows/deploy-phase-05.yml)
> **Pré-requisito:** F1-F4 provisionadas e funcionando (gateway YARP da F2 no ar, identidade Entra da F3 ativa, SQL com seed).

Este guia leva você de "branch criada" até "chatbot respondendo com dados reais do SQL". Há **dois entregáveis**: o **MCP Server** (Container App .NET) e o **frontend** (Web App com o chatbot). O proxy de LLM vive **dentro** do MCP Server, então as keys das LLMs entram como App Settings do MCP Server — **nunca no bundle do front**.

> **Convenção:** onde aparecer `<...>` substitua pelo seu valor (nome do recurso, GUID, etc.). Comandos usam Azure CLU (`az`). Você pode fazer tudo pelo Portal também — indico os equivalentes.

---

## Visão geral do que você vai criar

```
┌─────────────────────────────────────────────────────────────┐
│  Frontend (Web App)                                          │
│  VITE_GATEWAY_V2_URL, VITE_LLM_PROXY_URL, VITE_LLM_PROVIDER  │
│  (NUNCA a key da LLM)                                        │
└───────────────┬─────────────────────────────────────────────┘
                │ Bearer Entra (MSAL F3)
                ▼
┌─────────────────────────────────────────────────────────────┐
│  Gateway YARP (Container App da F2)                          │
│  rota /mcp  → cluster mcp-server                             │
│  rota /llm/** → cluster mcp-server                           │
│  valida JWT, propaga X-Entra-OID                             │
└───────────────┬─────────────────────────────────────────────┘
                ▼
┌─────────────────────────────────────────────────────────────┐
│  MCP Server (Container App NOVO desta fase)                  │
│  /mcp     → 3 tools MCP (Dapper → SQL)                       │
│  /llm/**  → proxy injeta GEMINI/GROQ/MISTRAL_API_KEY         │
│  /health  → smoke probe                                      │
│  App Settings: SqlConnectionString + as 3 keys              │
└─────────────────────────────────────────────────────────────┘
```

---

## Passo 0 — Branch e pré-checagens (5 min)

1. Garanta que está na branch da fase:

   ```bash
   git checkout phase-05-ai-mcp
   ```

2. Confirme que o **gateway YARP da F2** está no ar e que você consegue obter um token (Login v2 / MSAL da F3 funcionando). O chatbot **depende** disso — as tools exigem Bearer Entra.

3. Confirme que o **SQL** tem dados seedados (partidas, categorias, compras) — sem dados, as tools respondem "não encontrado", o que é correto, mas não impressiona na demo.

> **Por que isso primeiro?** F5 é cumulativa. Se a F2/F3 não estiverem firmes, o sintoma vai aparecer como "401 ao chamar o chatbot" e você perde tempo caçando no lugar errado.

---

## Passo 1 — Build e teste local do MCP Server (10 min)

Antes de subir nada, valide localmente. O MCP Server roda em ASP.NET Core na porta `5050` (a mesma que o cluster `mcp-server` do gateway aponta em dev — ver `appsettings.json` do gateway: `http://localhost:5050/`).

```bash
# Restore + build (Release)
dotnet build src/Fifa2026.V2.McpServer -c Release

# Testes (SEM --no-build: o test project precisa de restore+build próprio)
dotnet test src/Fifa2026.V2.McpServer.Tests -c Release
```

Esperado: **build 0 warnings / 0 errors** e **32/32 testes verdes**.

Para rodar o servidor local apontando ao seu SQL:

```bash
# Defina a connection string (App Setting) via env var local
export SqlConnectionString="Server=...;Database=...;User Id=...;Password=...;Encrypt=True"
dotnet run --project src/Fifa2026.V2.McpServer
```

Teste o health:

```bash
curl -s http://localhost:5050/health
# {"status":"healthy","service":"mcp-server"}
```

E teste o `tools/list` direto (JSON-RPC) — deve listar as 3 tools:

```bash
curl -s http://localhost:5050/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Você deve ver `consultar_disponibilidade`, `verificar_ingresso` e `consultar_bracket` com seus inputSchema derivados pelo SDK.

> **Observação:** localmente você chama `/mcp` direto (sem gateway, sem Bearer) só para validar o servidor. **Em produção, o front SEMPRE passa pelo gateway** com Bearer Entra — nunca direto.

---

## Passo 2 — Provisionar o Container App do MCP Server (15 min)

O MCP Server é um **Container App novo**, no **mesmo resource group** da F2 (reuso — `PHASE02_RESOURCE_GROUP`). Ele é um servidor HTTP de longa duração que serve streaming, então Container App é o host recomendado (ADE-002 Inv 2) — exatamente como o gateway YARP.

### 2.1 Pelo Azure CLI

```bash
RG=<seu-resource-group-da-F2>
ENV=<seu-container-apps-environment-da-F2>     # reuse do ambiente da F2
ACR=<seu-acr>.azurecr.io
MCP_APP=<nome-do-mcp-server>                    # ex.: mcp-server-aluno01

# 1. Build + push da imagem (multi-stage SDK 8.0 → aspnet 8.0 — Dockerfile já existe)
az acr login --name <seu-acr>
docker build -t "$ACR/mcp-server:f5" src/Fifa2026.V2.McpServer
docker push "$ACR/mcp-server:f5"

# 2. Criar o Container App (ingress externo para o gateway alcançar; porta 8080 do aspnet)
az containerapp create \
  --name "$MCP_APP" \
  --resource-group "$RG" \
  --environment "$ENV" \
  --image "$ACR/mcp-server:f5" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 1
```

> **`--min-replicas 0` (scale-to-zero):** econômico para workshop, mas causa **cold start** (~10-20s na primeira chamada após ociosidade). O smoke test do workflow já espera com `sleep 20`. Na demo ao vivo, faça um "warmup" chamando o `/health` antes de abrir o chat.

### 2.2 Pelo Portal (equivalente)

Container Apps → Create → mesmo Environment da F2 → Image do seu ACR → Ingress: External, Target port 8080 → Scale: min 0, max 1.

---

## Passo 3 — Configurar os App Settings (keys + SQL) — server-side (15 min)

Esta é a parte de **segurança**: as keys das LLMs entram como **secrets do Container App** e são lidas pelo proxy via `IConfiguration`. **Nada disso vai para o front.**

O MCP Server lê estes nomes de configuração (confirmados no código):

| App Setting | Lido por | Obrigatório? |
|---|---|---|
| `SqlConnectionString` | `FifaQueryRepository` (Dapper) | **Sim** — sem ela o servidor lança erro na 1ª query |
| `GEMINI_API_KEY` | proxy `/llm/gemini` | Sim (provider default) |
| `GROQ_API_KEY` | proxy `/llm/groq` | Para o fallback (AC-12) |
| `MISTRAL_API_KEY` | proxy `/llm/mistral` | Para a demo de portabilidade (AC-10) |

### 3.1 Onde obter cada key (todas com tier gratuito, sem cartão)

| Provider | Onde criar a key |
|---|---|
| Gemini | https://aistudio.google.com/ → "Get API key" (prefixo `AIza...`) |
| Groq | https://console.groq.com/keys (prefixo `gsk_...`) |
| Mistral | https://console.mistral.ai/ → API Keys |

### 3.2 Configurar como secrets + env vars (mesmo padrão do workflow)

```bash
# 1. Secrets (sensíveis — nunca em var/repo)
az containerapp secret set \
  --name "$MCP_APP" --resource-group "$RG" \
  --secrets \
    "sql-conn=<connection-string-do-SQL>" \
    "gemini-key=<AIza...>" \
    "groq-key=<gsk_...>" \
    "mistral-key=<mistral-key>"

# 2. Env vars referenciam os secrets (secretref) — assim IConfiguration lê os nomes certos
az containerapp update \
  --name "$MCP_APP" --resource-group "$RG" \
  --set-env-vars \
    "SqlConnectionString=secretref:sql-conn" \
    "GEMINI_API_KEY=secretref:gemini-key" \
    "GROQ_API_KEY=secretref:groq-key" \
    "MISTRAL_API_KEY=secretref:mistral-key"
```

> **Regra de ouro:** o nome da **env var** (`GEMINI_API_KEY`) é o que o código lê; o **secret** (`gemini-key`) é onde o valor mora cifrado. O `secretref:` liga os dois. Se você só criar o secret e esquecer o `--set-env-vars`, o proxy responde **503** ("não configurado") — sintoma comum.

### 3.3 Validar que a key NÃO está no front (defense-in-depth)

O workflow já tem um *guard* que falha o build se um prefixo de key aparecer no `dist/`. Para checar manualmente após um build do front:

```bash
grep -rE 'gsk_[A-Za-z0-9]|AIza[0-9A-Za-z_-]{20,}|GEMINI_API_KEY|GROQ_API_KEY|MISTRAL_API_KEY' \
  "Lovable/World Cup Tickets Hub/dist/assets/"*.js && echo "VAZOU!" || echo "OK — nenhuma key no bundle"
```

Deve imprimir **"OK"**. Se imprimir "VAZOU!", alguém embutiu a key no front — pare e corrija (a key só pode estar no servidor).

---

## Passo 4 — Conectar o gateway YARP ao MCP Server (10 min)

O gateway já tem as rotas `/mcp` e `/llm/**` apontando para o cluster `mcp-server` (ver `appsettings.json` do gateway). O que falta é o gateway saber a **URL real** do Container App do MCP Server. Isso é feito pela mesma estratégia da F2: um `IProxyConfigFilter` (`McpServerDestinationConfigFilter`) que injeta a URL via configuração externalizada — **nunca hardcoded**.

1. Obtenha o FQDN do MCP Server:

   ```bash
   MCP_FQDN=$(az containerapp show --name "$MCP_APP" --resource-group "$RG" \
     --query properties.configuration.ingress.fqdn -o tsv)
   echo "https://$MCP_FQDN"
   ```

2. Configure essa URL no **gateway** (App Setting que o `McpServerDestinationConfigFilter` lê — o nome segue o padrão da F2; confirme em `src/Fifa2026.V2.Gateway/Infrastructure/McpServerDestinationConfigFilter.cs`). Exemplo:

   ```bash
   GW_APP=<nome-do-gateway-da-F2>
   az containerapp update --name "$GW_APP" --resource-group "$RG" \
     --set-env-vars "McpServerUrl=https://$MCP_FQDN"
   ```

3. Teste o caminho **através do gateway** (com um token Entra válido — pegue um do navegador após "Login v2"):

   ```bash
   TOKEN="<bearer-entra-do-navegador>"
   curl -s "https://<gateway-fqdn>/mcp" \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -H "Accept: application/json, text/event-stream" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
   ```

   Deve listar as 3 tools. Sem o `Authorization`, deve dar **401** (o gateway é o guardião — F3).

> **Confirme a propagação de identidade:** com o token, o gateway extrai o `oid` e injeta `X-Entra-OID`. Nos logs do MCP Server você verá `oid=1234abcd…` (mascarado) — **nunca** o GUID completo (é PII).

---

## Passo 5 — Build e deploy do frontend com o chatbot (15 min)

O front é buildado com as `VITE_*` embutidas. **Nenhuma** delas é uma key — só URLs e o nome do provider.

| Env var de build | Valor | Para quê |
|---|---|---|
| `VITE_GATEWAY_V2_URL` | URL do gateway YARP | rota `/mcp` (tools/call) — `mcpClient.ts` |
| `VITE_LLM_PROXY_URL` | URL do gateway YARP (mesma) | rota `/llm/**` (proxy da LLM) — `proxy.ts` |
| `VITE_LLM_PROVIDER` | `gemini` (default) \| `groq` \| `mistral` | qual LLM usar — `lib/llm/index.ts` |

> Em produção, `VITE_LLM_PROXY_URL` aponta para o **gateway** (que tem a rota `/llm/**` → cluster mcp-server). O gateway repassa ao MCP Server, que injeta a key. Assim **tudo passa pelo gateway**, inclusive a LLM.

### 5.1 Build local (para testar)

```bash
cd "Lovable/World Cup Tickets Hub"
npm ci
VITE_GATEWAY_V2_URL="https://<gateway-fqdn>" \
VITE_LLM_PROXY_URL="https://<gateway-fqdn>" \
VITE_LLM_PROVIDER="gemini" \
npm run build
```

### 5.2 Deploy

O caminho recomendado é deixar o **workflow** fazer (push na branch `phase-05-ai-mcp` ou `workflow_dispatch`), pois ele já tem o *guard* de key. As variáveis/secrets do workflow estão documentadas no topo do `deploy-phase-05.yml`. Para deploy manual via publish profile, use o mesmo padrão da F2.

---

## Passo 6 — Smoke das 3 tools (ao vivo) (10 min)

Abra o frontend, faça **Login v2** (Entra/MSAL — F3), clique no botão flutuante do chatbot (canto inferior direito) e faça as **3 perguntas canônicas** (AC-11). Elas estão no placeholder do próprio chatbot.

| # | Pergunta | Tool que o LLM deve chamar | Resposta esperada |
|---|---|---|---|
| 1 | "Tem ingresso para Brasil x Argentina?" | `consultar_disponibilidade` | disponibilidade + preço por categoria (VIP/Cat1/Cat2) |
| 2 | "Esse ingresso ID 123 é válido?" | `verificar_ingresso` | válido/inválido + comprador, partida, categoria, data |
| 3 | "Quem está nas oitavas?" | `consultar_bracket` | jogos da rodada (round_of_16) + placares + classificados |

**O que observar:**

- O badge no topo do chatbot mostra o **provider ativo** (`gemini`) — prova viva da portabilidade.
- A resposta deve trazer **dados do SEU SQL**, não texto genérico. Se vier genérico, o LLM não chamou a tool (ajuste a pergunta ou veja troubleshooting).
- SLA-alvo: **< 10s** por pergunta (incluindo cold start na primeira). Mostre o estado "Consultando..." enquanto carrega.

---

## Passo 7 — Demonstrar a portabilidade (5 min, bônus)

Troque o provider **sem mudar código**:

1. Rebuild do front com `VITE_LLM_PROVIDER=groq` (ou `mistral`), ou troque a `var` do workflow e re-rode o job `deploy-frontend`.
2. Faça **a mesma pergunta 1** ("Tem ingresso para Brasil x Argentina?").
3. O badge agora mostra `groq` (ou `mistral`), e a resposta é **equivalente** — porque os fatos vêm do **mesmo** MCP Server.

> **A mensagem:** o MCP desacopla o LLM dos dados. O MCP Server nem sabe qual LLM o chamou. Trocar de "cérebro" é trocar uma env var.

---

## Troubleshooting (mapeado ao código real)

| Sintoma | Causa provável | Solução |
|---|---|---|
| Chatbot diz "Proxy de LLM não configurado" | `VITE_LLM_PROXY_URL` ausente no build do front | Defina a env var de build (Passo 5) — `proxy.ts` exige; nunca embute key |
| `503` do proxy / "não configurado no servidor" | `GEMINI/GROQ/MISTRAL_API_KEY` ausente como App Setting | Passo 3.2 — crie o secret **e** o `--set-env-vars` (o `secretref` liga os dois) |
| `401` ao chamar o chatbot | Token Entra ausente/expirado | "Login v2" (MSAL F3); o gateway exige Bearer válido em todas as rotas |
| `X-Entra-OID` ausente nos logs do MCP Server | Request não autenticado (gateway só propaga p/ requests com JWT válido) | Confirme login; o gateway remove qualquer `X-Entra-OID` forjado pelo cliente |
| LLM responde genérico (não chamou a tool) | Modelo não reconheceu a intenção | Reforce a `description` da tool / refraseie a pergunta para citar o jogo/ID/rodada |
| `consultar_disponibilidade` falha com 1 argumento | (já corrigido) SDK 1.4.0 exige default em nullable | Garanta `= null` nos params (coberto por `McpToolCallIntegrationTests`) |
| Primeira chamada lenta (~15s) | Cold start (scale-to-zero) | Warmup do `/health` antes da demo; ou suba `--min-replicas 1` durante a aula |
| Tool retorna "não encontrado" sempre | SQL sem seed ou rótulos de categoria divergentes | Confirme seed; `consultar_disponibilidade` faz PIVOT por `VIP`/`Cat1`/`Cat2` (rótulos reais) |
| Build do front falha no guard de key | Alguém embutiu a key no código do front | A key só pode estar no servidor (proxy); remova-a do front |

---

## Checklist de conclusão da F5

- [ ] MCP Server (Container App) no ar; `/health` responde `healthy`.
- [ ] `tools/list` via gateway (com Bearer) lista as 3 tools.
- [ ] App Settings configurados: `SqlConnectionString` + as 3 keys (secretref).
- [ ] Front buildado com `VITE_GATEWAY_V2_URL`, `VITE_LLM_PROXY_URL`, `VITE_LLM_PROVIDER`.
- [ ] Guard de bundle passou (nenhuma key no `dist/`).
- [ ] 3 smoke tests respondem com dados do SQL (< 10s).
- [ ] Demo de portabilidade (gemini → groq/mistral) funcionou só trocando a env var.
- [ ] `X-Entra-OID` aparece **mascarado** nos logs do MCP Server (nunca completo).

> **Próximo:** F6 conecta a observabilidade de ponta a ponta (Flow Visualizer) — o chatbot vira mais um nó do fluxo, com o `correlationId` que nasceu no gateway e o `entraOid` que você já propaga. Ver SPEAKER-NOTES, bloco de transição.
