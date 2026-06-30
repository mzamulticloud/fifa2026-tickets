# Blueprint — Workshop "Living Lab" Azure-Native sobre FIFA 2026 Tickets

> **Documento de co-design produzido por:** Atlas (Analyst) em sessão com Guilherme Prux Campos · **Data:** 2026-05-24 · **Status:** Blueprint pronto para virar EPIC-002 via `@pm`.

---

## 1. Executive Summary

**O quê:** Workshop educacional de **~40 horas** durante a Copa do Mundo FIFA 2026, no qual o aplicativo **FIFA 2026 Tickets** atua como **laboratório vivo** evoluindo fase a fase. Cada fase introduz uma capacidade Azure-native sob a forma de **microsserviço .NET**, **sem tocar no backend Node original**. O participante sai do workshop com aplicação modernizada em sua própria subscription Azure.

**Por quê:** Ensinar Azure moderno (PaaS, serverless, mensageria, gateway, IAM, AI) num contexto material e progressivo — alunos não criam um "hello world" descartável; evoluem um produto real durante a Copa.

**Como:** **6 fases cumulativas** (F1–F6), cada uma entregando uma branch Git executável via CI/CD do GitHub Actions, mais artefatos didáticos consistentes (README aluno, Portal Guide passo-a-passo, Speaker Notes, slides, vídeo intro, branch executável).

**Custo do evento:** ~US$0 compartilhado (gateway agora é YARP em código, custo zero — ver ADE-004) + US$5-15 por aluno (Service Bus, Functions, Container Apps, App Insights). Mitigação obrigatória: Budget Alerts + script teardown ao final.

**Próximo passo:** `@pm` cria **EPIC-002 — Living Lab Workshop** com 7 stories (6 fases + 1 transversal Flow Visualizer) referenciando este documento.

---

## 2. Visão da "Living Lab"

### Conceito em uma frase

> Cada fase do workshop (1...6) entrega aos alunos uma branch Git executável via CI/CD que evolui cumulativamente o FIFA 2026 Tickets agregando 1 capacidade Azure-native via microsserviço .NET; o sistema original (Node + React + SQL) permanece imutável e os novos serviços conversam com ele por contratos bem definidos, com uma UI de observabilidade dedicada mostrando o fluxo de cada compra atravessando os componentes em tempo real.

### Forma material (arquitetura-alvo após F6)

```
┌──────────────────────────────────────────────────────────────────────┐
│  Frontend Vite+React (intocado)                                       │
│  + nova rota /flow → Flow Visualizer (UI didática)                    │
└──────────────────────────────────────────────────────────────────────┘
            │                                  │
            │ compra v1 (original)             │ compra v2 (didática)
            ▼                                  ▼
┌─────────────────────────┐         ┌──────────────────────────────────┐
│  Node/Express (intocado)│         │  Gateway YARP (.NET, Container)  │
│  → SQL Server           │         │  → Function .NET (entrypoint)    │
│                         │         │  → Service Bus (fila + DLQ)      │
└─────────────────────────┘         │  → Function .NET (consumer)      │
                                    │  → n8n (orquestração)            │
                                    │  → Azure SQL DB (mesma DB, marker)│
                                    │  + Chatbot (LLM via MCP server)  │
                                    │  + Entra External ID/CIAM (auth  │
                                    │    cliente) + workforce (admin)  │
                                    │  + App Insights (correlation IDs)│
                                    └──────────────────────────────────┘
```

### Princípio rector

Todo aluno termina com o app rodando em sua subscription Azure, com o **fluxo v2 funcional** comparando lado-a-lado com o **fluxo v1 original** — o paralelismo é didático intencional ("antes vs depois").

---

## 3. Stack Azure-Native (decisões fechadas)

| Componente | Tier | Custo estimado | Justificativa pedagógica |
|---|---|---|---|
| **Azure Service Bus** | Standard | ~US$10/mês base | Messaging assíncrono, queues, topics, DLQ |
| **Gateway YARP (ASP.NET Core)** | **Open-source self-hosted** (Container App Consumption, por aluno) | **US$0** (scale-to-zero) | Gateway em código C# — rate-limit, cache, transform, JWT validation reimplementados em `Yarp.ReverseProxy` + middleware ASP.NET Core. Transparência didática ("gateway por dentro") e reforço do fio condutor "microsserviço .NET". Substitui APIM Developer — ver ADE-004 |
| **Azure Functions** | Consumption (.NET 8 isolated) | ~grátis em 1M execs/mês | Serverless, bindings, durable functions |
| **n8n self-hosted** | Azure Container Apps (Consumption) | ~US$5-15/mês | Workflow automation low-code + ensina containers |
| **Microsoft Entra External ID (CIAM) — camada CLIENTE** | Trial 30 dias (sem subscription/cartão, até 10K objetos) · free 50K MAU | US$0 | Identidade B2C real do cliente final: tenant CIAM separado (`<tenant>.ciamlogin.com`), 1 user flow self-service sign-up/sign-in, social login Google + email/OTP fallback; App Reg SPA (Auth Code + PKCE). Produto CIAM correto p/ consumidor (sucessor do Azure AD B2C). Ver ADE-007 (supersede ADE-005) |
| **App Registration workforce + App Roles — camada ADMIN** | Free | US$0 | Identidade B2B do operador no tenant Entra ID workforce (`login.microsoftonline.com`); App Roles (`Admin`/`Operator`/`Viewer`) **construídos hands-on**. Dois mundos de identidade coexistindo (cliente CIAM + admin workforce). Ver ADE-007 |
| **Easy Auth (App Service Authentication)** | Free | US$0 | Proteção opcional/complementar do front em App Service. Alternativa "zero-código-de-auth" — ver ADE-005/ADE-007 |
| **MCP Server** | Auto-hospedado em Function .NET | Incluído | Protocolo de tools para LLM — assunto-quente 2026 |
| **Chatbot frontend** | Componente React | US$0 | Integração LLM no produto |
| **LLM provider** | Google Gemini 2.0 Flash | US$0 | Único LLM moderno gratuito e estável em escala de turma; sem cartão, sem aprovação |
| **Application Insights** | Pay-per-GB | ~US$0-5/mês | Observabilidade + fonte do Flow Visualizer |
| **GitHub Actions** | Free tier público | US$0 | CI/CD por fase |
| **Azure SignalR Service** | Free tier (20 conexões) | US$0 | Real-time do Flow Visualizer |

### Cost Model

- **Custo compartilhado do evento: ~US$0.** Com a substituição do APIM Developer por YARP em código (ADE-004), o gateway deixa de ser um recurso compartilhado: passa a ser um Container App por aluno em Consumption (scale-to-zero ~US$0). Não há mais o "cenário A — APIM compartilhado (~US$50-80)"; ele deixou de existir. O único custo compartilhado residual é o domínio personalizado opcional (~US$12/ano), que não é obrigatório.
- **Demais recursos por aluno (incl. o Container App do gateway YARP):** US$5-15 em 40h.
- **Identidade (Entra External ID/CIAM no cliente + workforce no admin, ADE-007):** US$0. O tenant CIAM usa **trial 30 dias sem subscription/cartão**, pré-provisionado pelo instrutor fora do relógio da aula (não é recurso compartilhado pago); o admin usa o tenant workforce que o aluno já tem.
- **Mitigação obrigatória:** Azure Budget Alert + script `teardown.ps1` que destrói RG inteiro ao final.

---

## 4. Phasing Detalhado

### Visão geral das 6 fases (cumulativas)

| # | Branch | Tempo | Tema | Novidade |
|---|---|---|---|---|
| F1 | `phase-01-servicebus-functions` | 6h | Mensageria desacoplada | Service Bus + Function entry + Function consumer |
| F2 | `phase-02-gateway` | 6h | Gateway em código | Gateway YARP (.NET) em Container App — rate-limit, cache, transform, JWT validation em código (ver ADE-004) |
| F3 | `phase-03-identity` | 6h | Identidade moderna (dois mundos) | Cliente no **Entra External ID / CIAM** (`ciamlogin.com`, user flow, Google + OTP) + admin no **workforce** (App Roles); gateway YARP valida o JWT do CIAM; migração `users` v1→CIAM hands-on. Ver ADE-007 (supersede ADE-005) |
| F4 | `phase-04-orchestration` | 6h | Workflow visual | n8n em Container Apps; orquestra notificação pós-compra |
| F5 | `phase-05-ai-mcp` | 8h | Inteligência conversacional | MCP server (Function .NET) + chatbot + Gemini 2.0 Flash |
| F6 | `phase-06-flow-visualizer` | 8h | Observabilidade didática | Flow Visualizer UI com correlation ID animado em tempo real via SignalR |
| main | merge final | — | Consolidação | Merge F6 → main, retrospectiva, cleanup |

**Total:** 40h (com 4h de buffer para retrospectivas e overhead).

### Padrão de artefatos por fase (6 artefatos, todos obrigatórios)

| Artefato | Audiência | Quando usar |
|---|---|---|
| `README.md` | Aluno | Leitura prévia (semana anterior) |
| `PORTAL-GUIDE.md` | Aluno | Durante o hands-on, segue ao vivo |
| `SPEAKER-NOTES.md` | Facilitador | Antes (preparo) + durante (referência) |
| `slides.pdf` / Reveal.js | Apresentação | Durante a aula |
| `intro-video.mp4` (~5min) | Aluno | Antes da aula (assíncrono) |
| Branch + workflow CI/CD | Aluno | Hands-on + pós-aula |

> **Padrão pedagógico:** Provisioning de recursos Azure **sempre via Portal passo-a-passo** com prints, durante demo guiada (instrutor projeta, aluno replica). Bicep/IaC vira apêndice opcional ("se sobrar tempo / curiosidade").

---

### 🎯 F1 — Mensageria Desacoplada (fase-piloto detalhada)

**Branch:** `phase-01-servicebus-functions` · **Duração:** 6h · **Pré-req:** subscription Azure + free trial US$200 + C# básico + Git

#### F1.① Objetivos pedagógicos

Ao final, o aluno consegue:

- Diferenciar comunicação **síncrona (HTTP)** vs **assíncrona (queue)** e quando usar cada uma
- Comparar **Service Bus vs Storage Queue vs Event Grid** (matriz de decisão)
- Descrever a anatomia do Service Bus: **namespace, queue, topic, subscription, DLQ**
- Explicar **at-least-once delivery** e por que isso obriga **idempotência no consumer**
- Usar **Function bindings declarativos** (`[ServiceBusOutput]`, `[ServiceBusTrigger]`)
- Detectar e mitigar **cold start** do Consumption plan
- Configurar **lock duration, max delivery count, dead-lettering**
- Configurar **App Insights** com correlation ID atravessando entry + consumer

#### F1.② Arquitetura técnica delta

```
[Browser]
   │ POST /api/v2/purchase
   ▼
[Function App: fifa2026-v2-functions (.NET 8 isolated, Consumption)]
   ├─ PurchaseEntryFunction (HTTP trigger)
   │     └─ envia msg →
   │                     [Service Bus Namespace: sb-fifa2026-<aluno> (Standard)]
   │                       └─ Queue: tickets-purchase (lock 30s, max delivery 10)
   │                             └─ DLQ auto: tickets-purchase/$DeadLetterQueue
   └─ PurchaseConsumerFunction (Service Bus trigger)
         └─ INSERT em SQL Server (mesmo DB do v1)
              └─ tabela purchases (coluna nova source='v2', correlation_id)
```

- **Recursos novos:** 1 Function App, 1 Service Bus namespace, 1 queue + DLQ
- **NÃO toca:** Node API, frontend Vite (só adiciona botão "Comprar v2"), SQL schema só ganha 2 colunas idempotentes

#### F1.③ Endpoints novos

| Verbo | Path | Resposta | Latência alvo |
|---|---|---|---|
| `POST` | `/api/v2/purchase` | `{ correlationId: uuid, status: "queued" }` | < 100ms |
| `GET` | `/api/v2/purchase/{correlationId}` | `{ status: queued\|processing\|completed\|failed, ticketId?: int }` | < 200ms |

Body do POST: `{ matchId: int, category: "VIP"|"Cat1"|"Cat2", userId: int, quantity: int }`.

#### F1.④ Schema delta (migration `phase-01.sql`, idempotente)

```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='source' AND Object_ID=Object_ID('purchases'))
    ALTER TABLE purchases ADD source NVARCHAR(20) NOT NULL DEFAULT 'v1';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='correlation_id' AND Object_ID=Object_ID('purchases'))
    ALTER TABLE purchases ADD correlation_id UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE Name='IX_purchases_correlation_id')
    CREATE INDEX IX_purchases_correlation_id ON purchases(correlation_id) WHERE correlation_id IS NOT NULL;
```

> Migração rodada em **pré-workshop**, NÃO durante a aula (evita atrito).

#### F1.⑤ GitHub Actions workflow (esqueleto)

```yaml
name: Deploy Phase 01 — Service Bus + Functions
on:
  push:
    branches: [phase-01-servicebus-functions]
  workflow_dispatch:
jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet publish src/Fifa2026.V2.Functions -c Release -o ./publish
      - uses: Azure/functions-action@v1
        with:
          app-name: ${{ vars.PHASE01_FUNCTION_APP_NAME }}
          package: ./publish
          publish-profile: ${{ secrets.PHASE01_FUNCTION_PUBLISH_PROFILE }}
      - name: Smoke test
        run: |
          curl -fsS https://${{ vars.PHASE01_FUNCTION_APP_NAME }}.azurewebsites.net/api/v2/purchase \
            -H "Content-Type: application/json" \
            -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}' | tee response.json
          jq -e '.correlationId' response.json
```

#### F1.⑥ Roteiro de aula (6h = 360min)

| # | Bloco | Tempo | Modo | Marco |
|---|---|---|---|---|
| 1 | Conceitos (slides): sync vs async, SB vs SQ vs EG, anatomia SB | 50min | Aula expositiva + Q&A | Aluno entende quando usar mensageria |
| 2 | **Provisioning SB via Portal Azure (passo-a-passo)** | 45min | Demo guiada Portal — instrutor projeta, alunos replicam seguindo `PORTAL-GUIDE.md` | Aluno tem namespace + queue + DLQ visíveis no Portal |
| 3 | PurchaseEntryFunction (live coding) | 60min | Live coding + replicação | Mensagem chega na queue |
| ☕ | Coffee break | 15min | — | — |
| 4 | PurchaseConsumerFunction (live coding) | 60min | Live coding + replicação | Registro grava em SQL com idempotência |
| 5 | DLQ + Failures lab (forçar exceptions) | 45min | Lab investigativo | Aluno reprocessa mensagem do DLQ |
| 6 | CI/CD via GitHub Actions + smoke test | 40min | Hands-on | Branch verde com deploy automático |
| 7 | Retro + Q&A + carry-over para F2 | 45min | Conversa | Aluno pronto para F2 |

#### F1.⑦ DoD do aluno

- [ ] Service Bus namespace + queue + DLQ provisionados **via Portal** (aluno demonstra navegação Portal → SB namespace → queue → DLQ)
- [ ] `PurchaseEntryFunction` responde `POST /api/v2/purchase` com `correlationId` em < 100ms
- [ ] Mensagem visível na queue (Service Bus Explorer)
- [ ] `PurchaseConsumerFunction` consome e grava em `purchases` com `source='v2'`
- [ ] Teste de idempotência: mesma mensagem 2x = 1 registro
- [ ] Teste de falha: mensagem com `matchId` inválido vai para DLQ após 10 entregas
- [ ] App Insights mostra trace com correlationId atravessando entry + consumer
- [ ] GitHub Actions workflow do branch `phase-01-servicebus-functions` em verde

#### F1.⑧ Troubleshooting esperado (mapa antecipado)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| Função não consome mensagem | Connection string com `EntityPath=...` no App Setting (deveria ser sem) | Remover `EntityPath`, deixar só no atributo do binding |
| Mensagem reentrega infinita | Lock duration < tempo de processamento | Aumentar para 60s ou chamar `RenewLockAsync` |
| `CommandTimeout` no SQL | Connection pool exaurido | `MaxConcurrentCalls=4` no `host.json` |
| Cold start de 10s na 1ª chamada | Consumption plan free | Aceitar (didático); Premium não nesta fase |
| 401 ao chamar Function | `authLevel: Function` sem chave no header | Usar `authLevel: Anonymous` em F1 (segurança vira F2) |
| Mensagem como string vs JSON | Content-Type errado no send | Forçar `application/json` no `ServiceBusMessage` |
| Erro "Managed Identity not enabled" | Tentativa precoce de usar MI | F1 usa connection string; MI fica para F3 |
| `purchase` table not found | Nome real é `purchases` (minúsculo) | Confirmar e padronizar |

#### F1.⑨ Esqueletos `SPEAKER-NOTES.md` e `PORTAL-GUIDE.md`

**`SPEAKER-NOTES.md` (excerto Bloco 2):**

```markdown
# SPEAKER NOTES — F1, Bloco 2: Provisioning Service Bus

**Duração:** 45min (±5min)
**Objetivo:** turma sai com namespace + queue + DLQ funcionando

## Pontos a enfatizar
- "Standard tier libera topics — em Basic só queue"
- "Lock duration 30s é default; em produção depende do tempo de processamento"
- "DLQ é AUTOMÁTICA — não precisa criar, basta consumir de `<queue>/$DeadLetterQueue`"

## Perguntas pra turma (escolher 1-2)
- "Quem aqui já usou RabbitMQ ou Kafka? Vamos comparar."
- "Por que escolheriam Standard em vez de Premium aqui?"

## Armadilhas (acompanhar)
- ⚠️ Região errada — East US 2 é o padrão do workshop
- ⚠️ Naming: namespace tem que ser globally unique
- ⚠️ Pricing tier: alguns escolherão Basic; reforçar Standard

## Se sobrar tempo (15min)
- Service Bus Explorer enviando mensagem manual
- Métricas no Portal (mensagens ativas, DLQ count)

## Se faltar tempo (-10min)
- Pular Standard vs Premium (mencionar e seguir)
- Pular Bicep teaser do apêndice

## Transição para Bloco 3
"Recursos criados. Agora vamos escrever a Function que vai publicar mensagens nessa queue."
```

**`PORTAL-GUIDE.md` (excerto, primeiros steps):**

```markdown
# PORTAL GUIDE — F1: Provisioning Service Bus

**Pré-req:** Azure subscription ativa, login em portal.azure.com

## Step 1 — Criar Resource Group (3min)
1. Portal → busca → "Resource groups"
2. `+ Create`
3. Subscription: <sua>
4. Resource group name: `rg-fifa2026-workshop-<iniciais>`
5. Region: **East US 2** (padrão do workshop)
6. `Review + create` → `Create`
[PRINT: tela final do RG]

## Step 2 — Criar Service Bus Namespace (8min)
1. Portal → busca → "Service Bus"
2. `+ Create`
3. Subscription / Resource group: do Step 1
4. Namespace name: `sb-fifa2026-<iniciais>-<3 dígitos>` (único global)
5. Location: **East US 2**
6. Pricing tier: **Standard** ⚠️ (não Basic)
7. `Review + create` → `Create` (~2min)
[PRINT: namespace criado]

## Step 3 — Criar Queue `tickets-purchase` (4min)
[... steps continuam ...]

## Step 4 — Copiar Connection String (3min)
[... steps continuam ...]

## Validation
✅ Namespace visível no RG
✅ Queue `tickets-purchase` listada
✅ DLQ `tickets-purchase/$DeadLetterQueue` visível em Service Bus Explorer
✅ Connection string copiada
```

> F1 completa. **F2-F6 herdam o mesmo molde** (9 sub-itens) e o mesmo padrão de 6 artefatos.

---

### F2 — Gateway em Código YARP (esqueleto)

**Branch:** `phase-02-gateway` · **Duração:** 6h · **Novidade:** Gateway YARP (.NET) em Container App (ver ADE-004)

**Escopo:** introduzir um gateway **YARP (`Yarp.ReverseProxy`) em ASP.NET Core**, hospedado em **Azure Container App (Consumption, por aluno)** à frente das Functions de F1. Em vez de policies XML proprietárias do APIM, o aluno **lê e escreve em C#** os equivalentes (contrato de paridade em ADE-004 Invariante 3): `rate-limit-by-key` → `AddRateLimiter` (partição por chave); `cache` → `AddOutputCache`; `set-header` (ex.: `X-Correlation-ID`) → YARP `Transforms`/`AddRequestTransform`; `rewrite-uri`/path strip → `PathRemovePrefix`/`PathPattern`; `cors` → `AddCors`/`UseCors` (origem = front); `validate-jwt` → `AddJwtBearer` (ativado só em F3, ADE-005). O projeto YARP é versionado no repo e deployado via mesmo CI/CD por fase. Hosting default é Container App; Function .NET isolated fica como alternativa documentada (ponto de validação no design da story 2.2). Custo ~US$0, deploy em segundos (sem os 30-45min de provisioning do APIM).

**DoD aluno (resumo):** sobe o Container App do gateway YARP por aluno; chama compra v2 via URL do gateway; vê rate limiter em código retornar 429 acima do limite; vê output cache responder a 2ª chamada `GET` mais rápido (header de cache hit); CORS restrito ao front e `X-Correlation-ID` propagado downstream; tracing end-to-end via logs do Container App + App Insights. **Nota didática:** "em produção corporativa o equivalente gerenciado é o APIM; aqui ensinamos o conceito em código para transparência" (vira slide/SPEAKER-NOTES).

### F3 — Identidade Moderna: dois mundos (esqueleto)

**Branch:** `phase-03-identity` · **Duração:** 6h (ver nota de duração estendida das Quartas abaixo) · **Novidade:** cliente no **Entra External ID / CIAM** + admin no **workforce**; migração `users` v1→CIAM hands-on (ver ADE-007, supersede ADE-005)

> **Re-escopo (ADE-007, 2026-06-25):** a identidade do **cliente** volta para o **Microsoft Entra External ID (CIAM)** — o produto B2C correto para o consumidor final, sucessor oficial do Azure AD B2C. O tenant workforce **não some**: ele é reposicionado para a camada **admin**. Esse é o desenho canônico de produto B2C: cliente externo no CIAM (`ciamlogin.com`), funcionário interno no workforce (`login.microsoftonline.com`). A premissa de atrito que a ADE-005 usou para descartar o CIAM caiu — hoje há **trial sem subscription/cartão + extensão VS Code**, então o instrutor **pré-provisiona** o tenant/user flow/social IdP fora do relógio da aula.

**Escopo (dois mundos de identidade num lab):**
- **Cliente (B2C) — Entra External ID / CIAM:** tenant CIAM **separado** com **1 user flow** self-service sign-up/sign-in, **social login Google** (pré-configurado pelo instrutor) + **email/OTP** como fallback. O aluno cria uma **App Registration tipo SPA** no tenant CIAM e pluga `authority = <tenant>.ciamlogin.com` no MSAL (`@azure/msal-browser`/`-react`, Auth Code + PKCE). O **gateway YARP valida o JWT do CIAM** (`AddJwtBearer` → discovery `ciamlogin.com`, ADE-004 Inv 4 preservada), extrai o `oid` e propaga `X-Entra-OID` downstream — **o encadeamento Gateway→Function→SQL não muda** (issuer-agnóstico; só a string da authority/issuer muda).
- **Admin (B2B) — workforce + App Roles, construído hands-on:** App Registration no tenant Entra ID workforce com **App Roles** (`Admin`/`Operator`/`Viewer`) — login de admin separado do cliente. O gateway aceita os **dois** issuers (configuração, não reescrita).
- **Migração `users` v1 → CIAM (passo prático do lab):** importa/vincula os usuários `users` v1 ao tenant CIAM e liga o `entra_oid` resultante ao registro existente. É **aditiva e idempotente** — não apaga `users` nem o bcrypt; demonstra a **convivência** v1/v2, não a substituição.
- **Coexistência didática:** o v1 (bcrypt+JWT local) permanece intacto. O contraste **identidade homegrown** (você gerencia hash/reset/MFA) vs **identidade gerenciada CIAM** (Microsoft cuida) é a lição central das Quartas. **Mapping resolvido:** o `oid` (GUID estável, agora emitido pelo CIAM) é a chave — coluna aditiva `entra_oid` em `purchases`/`users`; sem tabela de mapping (ADE-007 Inv 3, herda ADE-005).

> **⚠️ Nota de duração — sessão única "longa" (decisão do owner, 2026-06-25):** Gateway YARP + cliente CIAM + admin workforce + migração hands-on somam **~7,5–9,5h**, acima do padrão ~6h/fase. O owner optou conscientemente por **sessão única completa** (não dividir A/B), preservando todas as decisões hands-on. O roteiro deve sinalizar a duração estendida e prever pontos de pausa naturais (ao fim do bloco do cliente CIAM) caso a turma precise quebrar em 2 encontros na prática.

**DoD aluno (resumo):** login CIAM (sign-up self-service + Google) via MSAL no SPA → access token com authority `ciamlogin.com` → gateway YARP valida o JWT do CIAM e extrai `oid` → Function v2 grava `entra_oid` ao lado do v1; admin (workforce + App Roles) construído e funcional; migração `users` v1→CIAM executada (mesmo usuário com bcrypt v1 + `entra_oid` CIAM). B2C legado (Azure AD B2C) **não** provisionado.

### F4 — Workflow Visual (esqueleto)

**Branch:** `phase-04-orchestration` · **Duração:** 6h · **Novidade:** n8n self-hosted em Azure Container Apps

**Escopo:** subir n8n via container em Azure Container Apps (Consumption plan, basic auth obrigatório). Orquestração: após compra v2 ser consumida e gravada, Function consumer dispara webhook n8n → workflow visual: (a) log estruturado, (b) e-mail mock (SendGrid free tier ou MailHog), (c) post em endpoint externo (httpbin), (d) opção condicional (se VIP, enviar a webhook diferente). Aluno aprende workflow automation low-code + container hosting.

**DoD aluno (resumo):** n8n acessível em URL HTTPS dedicada, workflow visualmente desenhado, executa quando recebe webhook, histórico de execuções visível no n8n UI.

### F5 — Inteligência Conversacional (esqueleto)

**Branch:** `phase-05-ai-mcp` · **Duração:** 8h · **Novidade:** MCP server + chatbot frontend + Gemini 2.0 Flash

**Escopo:** Function .NET dedicada vira MCP server expondo 3 tools: `consultar_disponibilidade(jogo)`, `verificar_ingresso(qr)`, `consultar_bracket(rodada)`. Componente React de chatbot no frontend conversa com Gemini 2.0 Flash; Gemini chama o MCP server via JSON-RPC quando precisa de dados; respostas chegam ao usuário. Conteúdo bônus: trocar Gemini por Groq/Llama via env var (portabilidade entre LLMs).

**DoD aluno (resumo):** pergunta "tem ingresso pra Brasil x Argentina dia 15?" no chat → bot consulta SQL via MCP → responde com disponibilidade + categorias + preços.

### F6 — Observabilidade Didática (esqueleto)

**Branch:** `phase-06-flow-visualizer` · **Duração:** 8h · **Novidade:** Flow Visualizer UI com correlation ID animado em tempo real

**Escopo:** nova rota `/flow` no frontend Vite. Diagrama dos componentes (Gateway YARP, Function entry, Service Bus, Function consumer, n8n, SQL) como nós. Ao executar compra v2, "bolinha" animada percorre o diagrama em tempo real, alimentada por SignalR. Backend: Function dedicada que consulta App Insights via SDK por correlation ID e empurra eventos via SignalR. Animação com framer-motion.

**DoD aluno (resumo):** compra v2 → vê bolinha percorrer Gateway YARP → SB → Function → n8n → SQL com tempo gasto em cada nó e payload inspecionável.

---

## 5. Estratégia de Branching + CI/CD

### Modelo linear cumulativo

```
main ──────────────────────────────────────────────── (estado pré-workshop, EPIC-001 done)
  └─ phase-01-servicebus-functions ──────────────────
       └─ phase-02-gateway ───────────────────────────
            └─ phase-03-identity ─────────────────────
                 └─ phase-04-orchestration ───────────
                      └─ phase-05-ai-mcp ─────────────
                           └─ phase-06-flow-visualizer
                                └─ merge → main (pós-workshop)
```

### Regras

- Cada branch tem seu próprio workflow `.github/workflows/deploy-phase-NN.yml`
- Cada fase deploya em recursos dedicados (slot OU resource group próprio): `fifa2026-web-phaseNN.azurewebsites.net`
- Aluno faz fork ou usa devcontainer pré-configurado
- Tag automática `vN.0.0-phaseNN` ao merge
- `main` é **congelada** durante o workshop (sem hotfixes em paralelo)

### Risco e mitigação

**Risco:** divergência entre fases se houver hotfix em F1 após F2 ser criada.
**Mitigação:** cherry-pick e rebase explícitos em script de bootstrap de cada fase (que reseta o estado para o aluno).

---

## 6. Flow Visualizer — Especificação

### Mecânica visual

- Tela única (`/flow`) com diagrama dos componentes como nós
- Compra v2 dispara "bolinha" animada percorrendo o diagrama em tempo real
- Cada nó mostra: tempo gasto, status (ok/erro), payload (clicável)
- Histórico das últimas 50 compras, pesquisável por correlation ID

### Mecânica técnica

- Componentes emitem eventos para App Insights com correlation ID propagado (W3C Trace Context)
- Endpoint `/api/flow/{correlationId}` em Function dedicada consulta App Insights via SDK
- **SignalR (Azure SignalR Service free tier — 20 conexões)** com fallback polling 2s se necessário
- Animação: framer-motion na stack atual (React 18)

### Por que é a estrela didática

Ensina em um só lugar: **distributed tracing, correlation IDs, observabilidade, mensageria assíncrona e UX de devtools** — todos os meta-conceitos que justificam cada peça do stack.

---

## 7. LLM Strategy + MCP

### Provider padrão: Google Gemini 2.0 Flash

| Fator | Valor |
|---|---|
| Free tier | 15 RPM · 1.5K req/dia · 1M tokens/dia |
| Cartão de crédito | Não exigido |
| Latência | ~500ms |
| Estabilidade | Alta |

### MCP server como abstração

- 3 tools expostas: `consultar_disponibilidade(jogo)`, `verificar_ingresso(qr)`, `consultar_bracket(rodada)`
- Implementado em Function .NET dedicada (parte de F5)
- Conversa via JSON-RPC com qualquer LLM compatível com MCP

### Conteúdo pedagógico bônus

Trocar LLM por env var (Gemini → Groq → Mistral) sem mexer no código do chatbot. Ensina **portabilidade entre LLMs via protocolo aberto**.

### Plano B (caso Gemini caia em aula)

- Groq (Llama 3.x) como fallback documentado
- Cache de respostas para queries comuns (degradação graciosa)

---

## 8. Identity Strategy — dois mundos (cliente CIAM + admin workforce)

> **Re-escopo (ADE-007, supersede ADE-005):** a identidade do **cliente** volta para o **Microsoft Entra External ID (CIAM)** — o produto B2C correto para o consumidor final. O tenant workforce é **reposicionado para o admin**, não removido. Esse é o desenho canônico de produto B2C: **cliente externo no CIAM (`<tenant>.ciamlogin.com`), funcionário interno no workforce (`login.microsoftonline.com`)**. Custo US$0 (trial CIAM sem subscription/cartão + free 50K MAU). A premissa de "atrito alto" que a ADE-005 usou para descartar o CIAM caiu: o instrutor **pré-provisiona** o tenant/user flow/social IdP fora do relógio da aula.
>
> **Esclarecimento de terminologia (vai para o slide de abertura de F2):** **Entra Connect** = sync AD on-prem → nuvem (irrelevante aqui); **Entra ID** = crachá de funcionário (workforce/B2B); **Entra External ID** = cadastro de cliente (CIAM/B2C). O comprador entra pelo External ID; o admin pelo workforce.

### Camada CLIENTE (B2C): Microsoft Entra External ID / CIAM

- **Tenant CIAM separado** (trial 30 dias, sem subscription/cartão, até 10K objetos) pré-provisionado pelo instrutor com **1 user flow** self-service sign-up/sign-in.
- **Social login Google** pré-configurado como identity provider + **email/OTP** como fallback de zero dependência.
- O aluno cria uma **App Registration tipo SPA** no tenant CIAM e usa **`@azure/msal-browser`/`@azure/msal-react`** com **Auth Code + PKCE**; a única string que muda vs o caminho anterior é **`authority = <tenant>.ciamlogin.com`** (não `login.microsoftonline.com`).
- O access token CIAM é enviado como `Authorization: Bearer` e **validado no gateway YARP** (`AddJwtBearer` → discovery `ciamlogin.com`, ADE-004 Inv 4 preservada) — ponto único de validação de identidade. O gateway é **issuer-agnóstico**: validar o token CIAM usa a mesma mecânica que validaria o workforce.
- **Azure AD B2C legado está depreciado** (fim de venda 2025-05-01) — **não usar**; External ID é o sucessor oficial.

### Camada ADMIN (B2B): workforce + App Roles, construído hands-on

- App Registration no **tenant Entra ID workforce** com **App Roles** (`Admin`, `Operator`, `Viewer`) — login de admin **construído como entregável do lab**, não só conceito.
- OAuth2 Authorization Code Flow; tokens validados no gateway YARP (`AddJwtBearer`), que aceita os **dois** issuers (CIAM + workforce) por configuração.

### Migração `users` v1 → CIAM (passo prático, aditivo)

- Importa/vincula os usuários `users` v1 ao tenant CIAM e liga o `entra_oid` resultante ao registro existente.
- É **aditiva e idempotente**: não apaga `users` nem o bcrypt. Demonstra a **convivência** v1/v2 — o ápice didático (mesmo usuário com bcrypt v1 + `entra_oid` CIAM).
- O mecanismo exato (Graph import/link) é detalhe a confirmar no draft da story com @data-engineer (ADE-007 Inv 6).

### Easy Auth (App Service Authentication) — alternativa / camada complementar

- Caminho alternativo "zero-código-de-auth": Easy Auth protege o App Service do front, expõe `/.auth/me` e header `X-MS-CLIENT-PRINCIPAL`, callback `/.auth/login/aad/callback`, secret gerenciado (elegível a Key Vault, ADE-003 Inv 3).
- Pressupõe **front em App Service** — coerente com a baseline PaaS (ADE-003) após EPIC-001 S4. Permanece opção válida; o caminho principal é MSAL no SPA (validação uniforme no gateway).

### Mapping de identidade — RESOLVIDO (ADE-007 herda ADE-005, supersede ADE-001)

> **O claim `oid` (Object ID, GUID estável do usuário) continua a chave canônica de identidade do v2** — agora emitido pelo **tenant CIAM** em vez do workforce. A tabela `purchases`/`users` mantém a coluna aditiva e idempotente `entra_oid UNIQUEIDENTIFIER` (+ índice), populada com o `oid` propagado pelo gateway (`X-Entra-OID`). **A coluna não muda de schema — só muda a origem do GUID.** Não há tabela de mapping nem estratégia GUID↔int a inventar (ADE-007 Inv 3).

---

## 9. Riscos + Mitigações

| # | Risco | Impacto | Mitigação |
|---|---|---|---|
| 1 | Custo Azure ultrapassa budget do evento | Alto | Gateway YARP custo ~US$0 (sem APIM, ADE-004) + Budget Alert + script teardown ao final |
| 2 | Atrito de setup do Entra External ID / CIAM em F3 (reaberto pela ADE-007) | Médio | **MITIGADO** (não eliminado): trial CIAM sem subscription/cartão + **instrutor pré-provisiona** tenant/user flow/Google IdP fora do relógio da aula; email/OTP como fallback de zero dependência. Trial expira em 30d → recriar por turma via script/VS Code. |
| 3 | n8n exposto sem auth na free config | Alto | Basic auth obrigatório no provisioning; documentado em PORTAL-GUIDE |
| 4 | MCP é tecnologia recente — quebra de spec | Médio | Pinning de versão do SDK MCP em todas as fases |
| 5 | Drift entre branches (hotfix em F1 após F2 criada) | Médio | Congelar `main` pré-workshop; cherry-pick scriptado |
| 6 | ~~Mapping de IDs Entra ↔ local~~ — **RESOLVIDO** (ADE-007 herda ADE-005) | — | Claim `oid` é a chave (agora emitido pelo CIAM); coluna aditiva `entra_oid` — só muda a origem do GUID. Sem tabela de mapping. Risco fechado. |
| 7 | 40h de workshop causa fadiga | Médio | Formato sugerido: 4 finais-de-semana × 10h ou 5 dias × 8h |
| 8 | Gemini cai durante aula F5 | Alto | Fallback Groq + cache local pré-configurado |
| 9 | Cold start de Functions trava demo ao vivo | Baixo | Warmup automático 5min antes de cada bloco hands-on |
| 10 | Alunos não cumprem pré-req (sem subscription Azure) | Médio | Checklist enviado 1 semana antes; bootcamp de setup opcional 2h antes |

---

## 10. Cost Model (detalhe)

### Estimativa por aluno em 40h

| Recurso | Custo estimado | Observação |
|---|---|---|
| Function App (Consumption) | ~US$0 | Dentro de 1M execuções/mês |
| Service Bus Standard | ~US$2-5 | Pro-rata para 1-2 meses |
| Container App (n8n) | ~US$3-8 | Consumption scale-to-zero |
| Container App (gateway YARP) | ~US$0 | Consumption scale-to-zero (ADE-004); substitui o APIM compartilhado |
| Azure SQL Database | US$0 | Mesma DB do v1 (estado pós-EPIC-001 S4); v2 exige Azure SQL DB, não SQL em VM (ADE-003 Inv 2) |
| App Insights | US$0-2 | Pay-per-GB ingerido |
| SignalR Service | US$0 | Free tier 20 conexões |
| Storage (Function backing) | ~US$0.50 | Trivial |
| **Total por aluno** | **~US$5-15** | — |

### Custo compartilhado (todo o evento)

| Recurso | Custo |
|---|---|
| Gateway (antes APIM Developer) | **US$0** — agora YARP em Container App por aluno (ADE-004); deixou de ser recurso compartilhado |
| Identidade — cliente CIAM + admin workforce (ADE-007) | US$0 — tenant CIAM em **trial sem subscription/cartão** (pré-provisionado pelo instrutor, não pago/compartilhado) + tenant workforce que o aluno já tem |
| Domínio personalizado (opcional) | ~US$12/ano |
| **Total compartilhado** | **~US$0** (apenas o domínio opcional ~US$12/ano, se adotado) |

### Guard rails obrigatórios

- Azure Budget Alert: US$30 por aluno (alerta em 80%)
- Script `teardown.ps1` destruindo RG inteiro ao final (entregue como story extra)
- Pre-flight checklist: aluno confirma free trial US$200 ativo

---

## 11. Próximos Passos — Handoff para `@pm`

Este blueprint está pronto para virar epic. Sugerido:

### EPIC-002 — Living Lab Workshop Azure-Native

**Stories propostas (7):**

| Story | Título | Tipo | Pré-req |
|---|---|---|---|
| 2.1 | F1 — Service Bus + Functions | Phase | EPIC-001 done |
| 2.2 | F2 — Gateway YARP + policies em código | Phase | 2.1 |
| 2.3 | F3 — Identidade: cliente CIAM + admin workforce + migração v1→CIAM | Phase | 2.2 |
| 2.4 | F4 — n8n em Container Apps | Phase | 2.3 |
| 2.5 | F5 — MCP server + chatbot + Gemini | Phase | 2.4 |
| 2.6 | F6 — Flow Visualizer | Phase | 2.5 |
| 2.7 | Materiais didáticos + speaker notes (transversal) | Documentation | paralelo |

**Handoff YAML gerado em:** `.aiox/handoffs/handoff-2026-05-24-analyst-to-pm-living-lab.yaml`

---

## 12. Apêndice — FAQs e Decisões Acumuladas

### Decisões fechadas nesta sessão (10/10)

| # | Decisão | Resolução |
|---|---|---|
| 1 | Hospedagem n8n | Azure Container Apps (Consumption) com basic auth |
| 2 | LLM padrão | Gemini 2.0 Flash + MCP para portabilidade |
| 3 | Quantas fases | 6 (F1-F6) + merge final |
| 4 | Escopo identidade v2 | **Cliente no Entra External ID / CIAM** (`ciamlogin.com`) + **admin no workforce** (App Roles) + **migração `users` v1→CIAM** hands-on; v1 mantém p/ comparação (re-escopo ADE-007, supersede ADE-005). *Evolução: a ADE-005 [2026-06-03] havia removido o External ID por atrito; a ADE-007 [2026-06-25] o reintroduz para o cliente — premissa de atrito revista (trial sem cartão + pré-provisionamento).* |
| 5 | Tools do MCP | `consultar_disponibilidade`, `verificar_ingresso`, `consultar_bracket` |
| 6 | Flow Visualizer real-time | SignalR free tier + fallback polling |
| 7 | Gateway + custo | **Sem APIM** — gateway YARP em código (.NET) em Container App por aluno, custo ~US$0; custo compartilhado do gateway eliminado (re-escopo ADE-004) |
| 8 | Pré-req aluno | C# básico + Git + Azure free trial US$200 |
| 9 | Audiência | Devs polyglot com background cloud (não exige .NET prévio) |
| 10 | Material por fase | 6 artefatos: README + PORTAL-GUIDE + SPEAKER-NOTES + slides + vídeo + branch |

### Decisões resolvidas pós-blueprint (re-escopo 2026-06-03, revisado 2026-06-25)

| Decisão | Resolução | Ref |
|---|---|---|
| Mapping IDs Entra ↔ local | **RESOLVIDA** — claim `oid` é a chave; coluna aditiva `entra_oid` (sem tabela de mapping). Só muda a origem do GUID (CIAM) | ADE-007 herda ADE-005 (supersede ADE-001) |
| Gateway (APIM vs código) | **RESOLVIDA** — YARP em código, sem APIM | ADE-004 |
| Identidade do CLIENTE (External ID vs workforce) | **REVISADA** — volta para **Entra External ID / CIAM** (cliente) + **workforce no admin**; premissa de atrito da ADE-005 revista | ADE-007 (supersede ADE-005) |

### Decisões pendentes (carry-forward para fases específicas)

| Decisão | Quando resolver | Responsável |
|---|---|---|
| Formato calendário (4×10h ou 5×8h) | Pré-evento | `@pm` |
| Custom domain do gateway (opcional) | Pré-evento | `@devops` |
| Pinning de versão MCP SDK | F5 design | `@architect` |

### FAQ antecipado para alunos

- **Q:** Preciso saber .NET pra fazer o workshop?
  **A:** Não. Demonstramos com .NET 8, mas você só precisa C# básico (variáveis, métodos, async/await). O conteúdo Azure é o foco.

- **Q:** Tenho subscription Azure pessoal?
  **A:** Sim. O free trial US$200 da Microsoft cobre o evento. Sem cartão? Não dá pra ativar — solicite com 1 semana de antecedência.

- **Q:** Sou de infra/ops, posso participar?
  **A:** Sim. Você vai ver IaC (apêndice Bicep), CI/CD, observabilidade, segurança — todos centrais ao papel.

- **Q:** O app vai ficar no ar depois?
  **A:** A versão merged em `main` fica no ar até a próxima Copa. Sua versão pessoal você destrói com `teardown.ps1`.

---

**Documento gerado em sessão de co-design facilitada por Atlas (Analyst). Pronto para handoff a `@pm`.**
