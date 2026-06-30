# PRE-WORKSHOP CHECKLIST — O que ter pronto ANTES do "Living Lab Azure-Native"

> **Artefato transversal** · Story [2.7](../stories/2.7.story.md) (AC-9) · Owner: **@analyst (Atlas)**
> **Para:** o aluno do workshop de 40h (6 fases). **Faça tudo abaixo ANTES da Fase 1.**
> **Tempo total estimado:** 60-90 min de setup + 30-60 min de pré-leitura.

Este checklist garante que você chegue na Fase 1 com tudo no lugar. A maior parte é tooling padrão. Há **uma pré-condição bloqueante** (Azure SQL Database) que, se faltar, **impede a Fase 1 de funcionar** — leia a seção 1 com atenção antes de qualquer outra coisa.

---

## 1. ⚠️ Pré-condição FÍSICA OBRIGATÓRIA — Azure SQL Database (ADE-003)

> ⚠️ **BLOQUEANTE — sem isto, a Fase 1 NÃO funciona.** Esta não é uma preferência; é uma restrição de plataforma.

**A regra:** a camada de dados do workshop v2 **DEVE** ser um **Azure SQL Database** (PaaS), **nunca** um SQL Server hospedado em VM com IP privado.

**Por quê (a restrição física):** as Azure Functions no plano **Consumption** (o plano gratuito/serverless que o workshop usa) **não estão em VNet**. Uma Function Consumption, portanto, **não alcança** um SQL Server em VM atrás de IP privado/NSG. O Azure SQL Database, ao contrário, expõe um endpoint público (`*.database.windows.net`) com firewall configurável — habilitar **"Allow Azure services and resources to access this server"** torna o banco alcançável a partir da Function Consumption, sem VNet. (Detalhe completo: [ADE-003 Invariante 2](../architecture/ade-003-v2-infrastructure-baseline.md).)

**Sintoma se você ignorar:** no Bloco 4 da Fase 1, a `PurchaseConsumerFunction` simplesmente **não conecta** ao banco — timeout de conexão sem erro claro. É um problema de rede invisível, dificílimo de diagnosticar ao vivo. Não passe por isso.

### Como satisfazer a pré-condição

Você precisa de **uma** das duas situações:

1. **Recomendado — você completou o EPIC-001 S4:** a migração VM → App Service + Azure SQL Database. Nesse caso seu ambiente já está 100% PaaS e você está pronto. O EPIC-002 (este workshop) parte exatamente desse estado final.
2. **Alternativa — provisione um Azure SQL Database manualmente:** crie uma Azure SQL Database na sua subscription, habilite "Allow Azure services and resources to access this server" no firewall do servidor, e tenha a **connection string funcional** em mãos.

### ✅ Validação da pré-condição (faça antes da Fase 1)

- [ ] Tenho um **Azure SQL Database** ativo (host `*.database.windows.net`) — **não** SQL em VM.
- [ ] O firewall do servidor tem **"Allow Azure services and resources to access this server"** habilitado.
- [ ] Consigo conectar com a **connection string** (testei com sqlcmd, Azure Data Studio ou SSMS).
- [ ] A tabela `purchases` existe (do app v1) — é onde o fluxo v2 grava lado a lado.

> 💡 **Em produção real**, você restringiria o acesso com Private Endpoint em vez de "Allow Azure services". Para um workshop (dados mock, Resource Group efêmero, teardown ao final) o caminho público é aceitável e de menor atrito.

---

## 2. Conta e subscription Azure

- [ ] **Subscription Azure ativa** (free trial US$200 serve para o workshop inteiro).
- [ ] Login funcionando em **portal.azure.com**.
- [ ] **Tenant Entra ID workforce** acessível (vem junto com a subscription) — necessário para a Fase 3 (App Registration + MSAL.js). Você **não** precisa de um tenant External ID/CIAM separado; usamos o workforce que você já tem ([ADE-005](../architecture/ade-005-identity-easy-auth.md)).
- [ ] **Budget Alert configurado** na subscription (ex.: alerta em US$10) — boa higiene de custo. O workshop é desenhado para ~US$0 (Consumption scale-to-zero), mas o alerta protege contra esquecer um recurso ligado.

### Verificação de quotas (a maioria das free trials já cobre)

- [ ] Quota de **Azure Container Apps** disponível na região do workshop (gateway YARP da F2, n8n da F4, MCP da F5 rodam aqui).
- [ ] Quota de **Azure Functions (Consumption)** disponível.
- [ ] Região do workshop definida: **East US 2** (use a mesma para todos os recursos — evita latência cruzada e simplifica troubleshooting).

---

## 3. Tooling local

Instale antes da aula:

- [ ] **.NET SDK 8** (o workshop é .NET 8 isolated worker).
- [ ] **Visual Studio 2022** OU **VS Code** (com a extensão C# Dev Kit, se VS Code).
- [ ] **Node.js LTS** (front Vite/React, MSAL.js na F3, chatbot React na F5).
- [ ] **Azure CLI** (`az`) — provisioning e troubleshooting via linha de comando.
- [ ] **GitHub CLI** (`gh`) — usado no bloco de CI/CD (GitHub Actions) de cada fase.
- [ ] **Git** configurado (`user.name` / `user.email`).
- [ ] Um cliente SQL: **Azure Data Studio**, **SSMS** ou **sqlcmd** (para rodar a migration `phase-01.sql` e inspecionar dados).

### ✅ Validação rápida de tooling

```bash
dotnet --version      # deve mostrar 8.x
node --version        # LTS (18+ ou 20+)
az --version          # Azure CLI presente
gh --version          # GitHub CLI presente
git --version         # Git presente
```

---

## 4. Pré-tarefa obrigatória da Fase 1 — rodar a migration

A **única** coisa hands-on que você faz antes da aula: rodar a migration `phase-01.sql` no seu **Azure SQL Database** (pré-condição da seção 1).

- [ ] Rodei `fifa2026-api/database/migrations/phase-01.sql` no Azure SQL DB.
- [ ] Vi a validação final OK: colunas `source` e `correlation_id` criadas + índice `UQ_purchases_correlation_id` (`is_unique = 1`, `has_filter = 1`).

> Detalhes de como rodar (sqlcmd ou Azure Data Studio) estão no [README da Fase 1, seção 8](./phase-01/README.md). A migration é **idempotente** e **somente aditiva** — rodar 2x não dá erro e não quebra o v1.

---

## 5. Pré-leitura sugerida (30-60 min)

Não precisa virar especialista — só chegar com o vocabulário aquecido. Leia o **README de cada fase** na véspera do respectivo dia. Para a abertura, revise os conceitos:

- [ ] **Mensageria** (síncrono vs assíncrono, fila, at-least-once) — base da F1.
- [ ] **Gateway / reverse proxy** (o que um gateway faz) — base da F2.
- [ ] **Identidade OIDC** (OAuth2, PKCE, claims, JWT) — base da F3.
- [ ] **Automação de workflow** (low-code, webhook) — base da F4.
- [ ] **LLM / function calling / MCP** — base da F5.
- [ ] **Observabilidade / correlação ponta-a-ponta** — base da F6.

> O [README da Fase 1](./phase-01/README.md) é a leitura prioritária — ele estabelece o molde (correlationId, idempotência, DLQ) que as 6 fases herdam.

---

## 6. Resumo — estou pronto quando...

- [ ] ✅ **Azure SQL Database ativo e alcançável** (seção 1 — BLOQUEANTE)
- [ ] Subscription Azure ativa + budget alert + tenant workforce acessível (seção 2)
- [ ] Tooling instalado e validado (seção 3)
- [ ] Migration `phase-01.sql` rodada e verificada (seção 4)
- [ ] README da Fase 1 lido (seção 5)

Cumpriu os 5? Nos vemos na Fase 1. Próximo artefato: [`phase-01/README.md`](./phase-01/README.md).
