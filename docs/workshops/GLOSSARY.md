# GLOSSARY — Glossário consolidado do Workshop "Living Lab Azure-Native"

> **Artefato transversal** · Story [2.7](../stories/2.7.story.md) (AC-8) · Owner: **@analyst (Atlas)**
> Todos os termos técnicos das 6 fases, em ordem alfabética. Cada verbete: 1-3 linhas + a(s) fase(s) onde aparece.
> Nomenclatura fiel ao re-escopo (ADE-004/ADE-005): **Gateway YARP** (nunca APIM como componente ativo) · **App Registration + MSAL.js** (nunca External ID como componente ativo) · claim **`oid`** / coluna **`entra_oid`**.

---

| Termo | Significado | Fase(s) |
|---|---|---|
| **App Insights (Application Insights)** | Serviço de observabilidade do Azure que coleta telemetria (traces, logs, métricas). No workshop, o `FlowEvents` o consulta via SDK `Azure.Monitor.Query` para montar o raio-x da compra. | F1, F6 |
| **App Registration** | Registro de aplicação no Entra ID que habilita OIDC/OAuth2. No v2, criada no **tenant workforce** do aluno (tipo SPA para o front, + uma admin com App Roles). Substitui o Entra External ID no fluxo v2 (ADE-005). | F3, F5, F6 |
| **App Roles** | Papéis (`Admin`, `Operator`, `Viewer`) definidos numa App Registration para autorização baseada em função. | F3 |
| **At-least-once** | Garantia de entrega em que cada mensagem chega **pelo menos uma vez** (pode duplicar). É o que obriga o consumer a ser idempotente. | F1 |
| **`Authorization: Bearer`** | Header HTTP que carrega o access token JWT do front para o gateway. O Gateway YARP valida o token antes de encaminhar. | F3, F5 |
| **`Azure.Monitor.Query`** | SDK .NET usado pelo serviço `FlowEvents` (F6) para consultar a telemetria de uma compra no App Insights por `correlation ID`. | F6 |
| **Azure Container Apps** | Hosting serverless para containers (scale-to-zero). Roda o Gateway YARP (F2), o n8n (F4) e o MCP Server (F5). | F2, F4, F5 |
| **Azure Functions (Consumption)** | Compute serverless event-driven, plano gratuito/scale-to-zero. **Não está em VNet** — por isso a camada de dados exige Azure SQL Database (ADE-003). | F1 |
| **Azure SQL Database** | Banco PaaS (`*.database.windows.net`) com endpoint público + firewall. **Pré-condição física** do workshop: Functions Consumption só alcançam SQL aqui, não em VM (ADE-003). | Todas |
| **Binding** | Forma declarativa do Azure Functions conectar a triggers/saídas (`[ServiceBusTrigger]`, `[ServiceBusOutput]`). | F1 |
| **Broker** | Intermediário confiável entre produtor e consumidor de mensagens. No workshop, o Azure Service Bus. | F1 |
| **CIAM (Customer Identity and Access Management)** | Categoria de identidade para clientes externos (B2C); o que o Entra External ID é. **Nota cultural** no workshop — não usado no fluxo v2 (usamos tenant workforce). | F3 |
| **Cold start** | Latência extra (~5-10s) na 1ª chamada de uma Function no plano Consumption após ociosidade. | F1 |
| **Container App** | Ver **Azure Container Apps**. | F2, F4, F5 |
| **Correlation ID** | GUID que identifica e rastreia uma compra por **todas** as camadas. Nasce no Gateway YARP (nó zero, F6) / na entry Function (F1) e viaja até o SQL. Chave da idempotência e do tracing. | F1, F2, F6 |
| **customDimensions** | Campos customizados na telemetria do App Insights. O `correlation ID` vira uma customDimension via `ILogger.BeginScope`. | F1, F6 |
| **Dead-Letter Queue (DLQ)** | Sub-fila automática (`<queue>/$DeadLetterQueue`) onde mensagens que falharam demais (max delivery) ou expiraram são estacionadas para investigação. | F1 |
| **Easy Auth (App Service Authentication)** | Autenticação sem código provida pelo App Service. No v2 é alternativa/complemento ao MSAL.js para proteger o **front** (ADE-005); a validação da API fica no Gateway YARP. | F3 |
| **`entra_oid`** | Coluna `UNIQUEIDENTIFIER` (aditiva, idempotente) em `purchases`/`users`, populada com o claim `oid`. É a chave de identidade do v2 — **não há tabela de mapping GUID↔int** (ADE-005). | F3, F4, F5, F6 |
| **Entra External ID** | Tenant CIAM separado para clientes externos (B2C). É o equivalente "real" que o workshop **simplifica** — **nota cultural**, não usado no fluxo v2. | F3 |
| **Event Grid** | Roteador de **eventos** (pub/sub reativo) do Azure. Comparado a Service Bus/Storage Queue na F1; não usado no fluxo v2. | F1 |
| **External Identities (blade do Portal)** | Nome do blade do Azure Portal (em **Microsoft Entra ID → External Identities → All identity providers**) usado para **federar Google/GitHub** num tenant workforce. **Não** é o produto "Entra External ID". | F3 |
| **Function calling (tool use)** | Capacidade do LLM de decidir chamar uma ferramenta externa para obter dados reais em vez de inventar. Base do chatbot da F5. | F5 |
| **Gateway YARP** | A porta de entrada do v2: um gateway **em código** (ASP.NET Core + `Yarp.ReverseProxy`) rodando em Container App por aluno. Faz rate-limit, cache, transform e **valida o JWT** (`AddJwtBearer`). Em F6 é o **nó zero** que injeta o `X-Correlation-ID`. Substitui o APIM no fluxo v2 (ADE-004). | F2, F3, F5, F6 |
| **Gemini 2.0 Flash** | LLM padrão do chatbot (F5). Faz function calling para chamar as tools do MCP Server. Trocável por Groq/Mistral via env var (bônus de portabilidade). | F5 |
| **Idempotência** | Propriedade de processar a mesma mensagem 2x ter o mesmo efeito que 1x. No workshop, garantida por índice UNIQUE filtrado + INSERT-catch (erro SQL 2627/2601). | F1 |
| **`ILogger.BeginScope`** | API de logging .NET que injeta propriedades de escopo (ex.: `CorrelationId`) em todos os logs subsequentes — vira customDimension no App Insights. | F1, F6 |
| **Isolated worker** | Modelo do .NET 8 em que a Function roda em processo separado do host. | F1 |
| **JSON-RPC 2.0** | Protocolo de chamada remota usado pelo MCP (`tools/list`, `tools/call`). | F5 |
| **JWT (JSON Web Token)** | Token assinado que carrega claims de identidade. Validado **um lugar só**: o Gateway YARP (`iss`, `aud`, assinatura, expiração). | F3, F5 |
| **Key Vault reference** | Mecanismo que aponta uma App Setting para um secret no Key Vault (`@Microsoft.KeyVault(SecretUri=...)`). `SqlConnectionString` usa isso — trocar a origem do dado é editar 1 secret, zero código (ADE-003). | Todas |
| **Lock duration** | Tempo que uma mensagem fica travada/invisível para um consumer antes de voltar à fila (30s na F1). Lock < tempo de processamento = reentrega indevida. | F1 |
| **Log Analytics** | Workspace de consultas (KQL) por trás do App Insights. | F6 |
| **Managed Identity** | Identidade gerenciada pelo Azure que elimina senha da connection string (estado-alvo da camada de dados, ADE-003 Inv 4) e autentica o `FlowEvents` no App Insights (F6). | F1, F6 |
| **Max delivery count** | Nº de tentativas de entrega antes da mensagem ir para a DLQ (10 na F1). | F1 |
| **MCP (Model Context Protocol)** | Protocolo padrão (JSON-RPC 2.0) que expõe **tools** a um LLM. O MCP Server .NET (SDK `1.4.0`, ADE-002) expõe 3 tools sobre o SQL na F5. | F5 |
| **MSAL.js (`@azure/msal-browser`)** | Biblioteca que faz login OIDC + PKCE no SPA, obtém o access token e o envia como Bearer. Caminho **recomendado** de autenticação do front (ADE-005). | F3, F5 |
| **n8n** | Plataforma de automação de workflow low-code, self-hosted em Container App (F4). Disparada por webhook pelo consumer F1 em padrão fire-and-forget (best-effort). | F4 |
| **Numbered options** | Convenção editorial: toda escolha oferecida ao leitor é apresentada em opções numeradas (ver CONTENT-TEMPLATE). | Todas |
| **`oid` (Object ID)** | Claim do Entra ID: GUID estável do usuário no tenant workforce. **Chave canônica de identidade do v2** — propagado pelo gateway como `X-Entra-OID` e gravado na coluna `entra_oid` (ADE-005). | F3, F4, F5, F6 |
| **PKCE (Proof Key for Code Exchange)** | Extensão do OAuth2 Authorization Code Flow para clientes públicos (SPA) sem client secret no browser. Usado pelo MSAL.js. | F3 |
| **Poison message** | Mensagem que falha sempre por mais que se reprocesse (ex.: `matchId` inexistente, JSON corrompido). Vai para a DLQ após o max delivery. | F1 |
| **Producer / Consumer** | Quem publica / quem processa a mensagem na fila. | F1 |
| **Queue (fila)** | Canal ponto-a-ponto (1 mensagem → 1 consumidor lógico). No workshop: `tickets-purchase`. | F1 |
| **Reverse proxy / BFF** | Padrão em que um servidor intermedia o cliente e os backends (esconde URLs reais, agrega, transforma). É o que o Gateway YARP faz. | F2 |
| **Service Bus** | Message broker enterprise do Azure (queue + topic, DLQ, at-least-once). Tier **Standard** no workshop. | F1 |
| **SignalR** | Serviço Azure de mensagens em tempo real (WebSocket). Em F6 (Service Mode Default) empurra eventos de telemetria ao navegador para animar o fluxo da compra. | F6 |
| **`SqlConnectionString`** | App Setting única (via Key Vault reference) que todos os microsserviços v2 usam para conectar ao Azure SQL Database (ADE-003 Inv 3). | Todas |
| **Storage Queue** | Fila simples e barata do Azure (sem features avançadas). Comparada a Service Bus na F1; não usada no fluxo v2. | F1 |
| **Tenant workforce** | O tenant Entra ID que já vem com a subscription Azure do aluno. É o tenant de identidade do v2 (App Registration aqui) — em vez de criar um tenant External ID/CIAM (ADE-005). | F3 |
| **TOCTOU (Time-Of-Check to Time-Of-Use)** | Race condition entre "checar" e "usar" — o motivo de evitar SELECT-then-INSERT e usar UNIQUE + INSERT-catch na idempotência. | F1 |
| **`tools/call` · `tools/list`** | Métodos JSON-RPC do MCP: listar as tools disponíveis e invocar uma tool. | F5 |
| **X-Correlation-ID** | Header HTTP que carrega o correlation ID entre os hops. Injetado pelo Gateway YARP (nó zero). | F2, F6 |
| **X-Entra-OID** | Header HTTP em que o Gateway YARP propaga o claim `oid` downstream para a Function (que grava `entra_oid`). A Function nunca confia em header de identidade não validado pelo gateway. | F3, F4, F5, F6 |
| **YARP (Yet Another Reverse Proxy)** | Biblioteca .NET (`Yarp.ReverseProxy`) com que o gateway do workshop é construído em código. Ver **Gateway YARP**. | F2 |
