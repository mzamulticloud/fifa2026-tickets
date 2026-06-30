# ADE-003 — Baseline de Infraestrutura do Living Lab v2 (PaaS + Azure SQL Database)

> **Tipo:** Architecture Decision Entry (infrastructure baseline)
> **Status:** ✅ Accepted
> **Date:** 2026-06-03
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 (todas as fases F1-F6) — pré-condição física de toda a arquitetura v2
> **Supersedes:** N/A
> **Related:** ADE-000 (microsserviço paralelo — Invariante 1 e 6), ADE-004 (gateway YARP), ADE-005 (identidade Easy Auth), EPIC-001 S4 (migração para Azure SQL Database)

---

## Context

Esta ADE foi gerada em sessão de re-escopo do EPIC-002 com o owner (Guilherme Prux Campos) em 2026-06-03. Ela formaliza a **baseline de infraestrutura** sobre a qual a arquitetura v2 (microsserviços .NET) é construída — uma camada que o blueprint original (`docs/workshops/2026-blueprint-living-lab-azure.md`) e o ADE-000 deixaram implícita, mas que se mostrou **acoplada à camada de dados** de forma que precisa ser explicitada antes de qualquer fase começar.

A questão central: **de onde os microsserviços v2 leem/gravam dados?** O blueprint assume "SQL Server (mesma DB)", mas não define se esse SQL está em VM (estado inicial do EPIC-001) ou em Azure SQL Database (estado final do EPIC-001 S4). Essa distinção **não é cosmética** — ela determina se a arquitetura v2 sequer funciona, por causa de uma restrição de rede dura das Azure Functions em Consumption plan.

O owner confirmou nesta sessão que **o aluno completa o EPIC-001 inteiro (VM → App Service + Azure SQL Database) ANTES de iniciar o EPIC-002**. O EPIC-002, portanto, parte de um estado 100% PaaS.

## Decision

A baseline de infraestrutura do Living Lab v2 adota o pattern **"Cloud-Agnostic Hosting, Data-Coupled Baseline"** com 4 invariantes:

### Invariante 1: Hosting do app v1 é indiferente (VM ou App Service)

A arquitetura v2 (microsserviços .NET via Functions) **NÃO depende de onde o app v1 (Node/Express + Vite/React) está hospedado**. VM ou App Service são intercambiáveis para o v2, porque o v2 conversa com o v1 apenas por:

- **HTTP** (chamadas a endpoints v1, se necessárias), via variável `V1BackendUrl`.
- **Banco de dados compartilhado** (escrita na tabela `purchases`), via `SqlConnectionString`.

Isso é garantido por ADE-000 Invariante 1 (backend original intocado). Nenhum microsserviço v2 assume topologia de hosting do v1.

> **Exceção introduzida por ADE-005:** quando o front é protegido por Easy Auth (App Service Authentication), o **front passa a exigir App Service** (Easy Auth é um recurso de App Service, não existe em VM sem reverse-proxy custom). Ver ADE-005. Isso NÃO contradiz esta invariante para os microsserviços v2 (que continuam agnósticos), apenas adiciona um requisito de hosting ao **front** a partir de F3.

### Invariante 2: A camada de dados v2 EXIGE Azure SQL Database (não SQL em VM)

Os microsserviços v2 **DEVEM** apontar para **Azure SQL Database** (PaaS), nunca para SQL Server hospedado em VM privada. Esta é uma restrição **física, não preferencial**:

- Azure Functions em **Consumption plan NÃO estão em VNet** (VNet integration exige Premium/Dedicated plan). Logo, uma Function Consumption **não alcança** um SQL Server numa VM com IP privado / atrás de NSG / sem endpoint público.
- Azure SQL Database expõe endpoint público (`*.database.windows.net`) com firewall configurável; habilitar **"Allow Azure services and resources to access this server"** torna a SQL DB alcançável a partir da Function Consumption sem VNet.
- Azure SQL Database ainda habilita **Managed Identity** como mecanismo de autenticação (ver Invariante 4), que SQL em VM não oferece nativamente para Functions.

**Consequência operacional:** o EPIC-001 S4 (migração VM → App Service + Azure SQL DB) é **pré-condição física do EPIC-002**. Sem ele, F1 não consegue gravar no banco. Esta dependência é dura, não recomendada.

### Invariante 3: Connection string única, externalizada via Key Vault reference

Todos os microsserviços v2 leem a conexão de banco de uma **única App Setting** chamada `SqlConnectionString`, cujo valor é uma **Key Vault reference**:

```
SqlConnectionString = @Microsoft.KeyVault(SecretUri=https://kv-fifa2026-<iniciais>.vault.azure.net/secrets/sql-connection-string/)
```

Propriedades:

- **Origem do SQL muda em 1 setting, zero código:** trocar o servidor de banco (ou rotacionar senha) é editar 1 secret no Key Vault. Nenhum código de Function muda. Isso protege o workshop de qualquer mudança futura de origem de dados.
- **Endpoint v1 segue o mesmo princípio:** a URL do backend v1 é a App Setting `V1BackendUrl` (não hardcoded), também elegível para Key Vault reference se contiver segredo.
- **PROIBIDO:** connection string hardcoded em código, em `local.settings.json` versionado, ou duplicada em múltiplas Functions com valores divergentes.

### Invariante 4: Managed Identity → Azure SQL DB assim que disponível (elimina senha)

A baseline declara como **estado-alvo** a autenticação da Function à Azure SQL DB via **System-Assigned Managed Identity**, eliminando senha da connection string:

- `SqlConnectionString` evolui de `...User ID=...;Password=...` para `...Authentication=Active Directory Default;` (sem senha).
- A Managed Identity da Function App recebe `db_datareader` + `db_datawriter` (ou role custom) na Azure SQL DB via `CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER`.

**Faseamento:** F1 pode iniciar com connection string + senha (caminho de menor atrito didático, coerente com o troubleshooting "Managed Identity not enabled → F1 usa connection string; MI fica para fase posterior" já mapeado em S2.1). A migração para Managed Identity é registrada como evolução desejável — habilitada pela Invariante 2 (só Azure SQL DB suporta) e implementada quando a fase de identidade (F3) estabelecer o terreno de Entra ID. Não é mandatória em F1, mas é o destino arquitetural.

---

## Rationale

### Por que explicitar a baseline agora (e não deixar implícita)?

- O blueprint dizia apenas "SQL Server (mesma DB)" — ambíguo entre VM e PaaS. A ambiguidade esconde uma **falha de runtime garantida**: se o aluno tentar F1 com SQL ainda em VM, a Function Consumption simplesmente não conecta, e o sintoma (timeout de conexão sem erro claro) é difícil de diagnosticar ao vivo num workshop de 6h.
- Tornar a dependência explícita e dura ("EPIC-001 done é pré-condição física") evita o pior cenário pedagógico: turma travada no bloco 4 de F1 por um problema de rede invisível.

### Por que Azure SQL DB é obrigatório (e não apenas "recomendado")?

- A restrição vem da plataforma, não de preferência: **Consumption plan = sem VNet = sem rota para IP privado de VM**. As alternativas para contornar (Premium plan com VNet integration, ou Private Endpoint + VNet) **violam o cost model** do evento (Premium custa muito acima do Consumption ~grátis) e adicionam complexidade de rede que não é o objetivo didático de F1.
- Azure SQL DB com firewall "Allow Azure services" é o caminho de menor custo e menor atrito que **funciona** com Consumption — é a única opção dentro das restrições firmes do epic (evento gratuito, Portal-first, Functions Consumption).

### Por que connection string única via Key Vault reference?

- **Desacopla origem de dados de código:** o owner quer que "migração de origem do SQL mude 1 setting, zero código". Key Vault reference entrega exatamente isso — a Function resolve o secret em runtime; trocar o valor não exige redeploy.
- **Higiene de segurança:** segredo nunca vive no repo nem em App Settings em texto claro (App Settings com Key Vault reference mostram só a referência, não o valor).
- **Caminho natural para Managed Identity:** a mesma identidade que lê o Key Vault é a que autentica na SQL DB (Invariante 4) — consolida o modelo de identidade da Function.

### Por que Managed Identity como estado-alvo (não imediato)?

- **Elimina o maior risco de segurança do workshop:** senha de banco em connection string. Com MI, não há senha para vazar.
- **Faseado por pragmatismo didático:** ensinar MI + RBAC de SQL em F1 sobrecarregaria a primeira fase (que já carrega Service Bus + Functions + idempotência). MI encaixa naturalmente depois que F3 introduz o vocabulário de Entra ID. A baseline registra o destino sem forçar o atalho cedo.

---

## Consequences

### Positivas

- ✅ Elimina a falha de runtime "Function Consumption não conecta no SQL da VM" antes de F1 começar — risco invisível neutralizado.
- ✅ Arquitetura v2 fica 100% PaaS e serverless-friendly (sem VNet, sem custo de plano dedicado).
- ✅ Origem de dados intercambiável via 1 setting (Key Vault reference) — robustez operacional para o evento inteiro.
- ✅ Caminho de hardening (Managed Identity, zero-senha) já desenhado e habilitado.
- ✅ Sequência didática clara: EPIC-001 (modernização para PaaS) → EPIC-002 (cloud-native), com o estado final de um sendo a pré-condição do outro — narrativa coerente para o aluno.

### Negativas / Trade-offs aceitos

- ⚠️ **Dependência dura entre épicos:** EPIC-002 não pode começar sem EPIC-001 S4 concluído. Mitigado: documentar como pré-flight checklist do EPIC-002 ("confirme Azure SQL DB ativa antes de F1").
- ⚠️ **Endpoint público da SQL DB:** "Allow Azure services" abre a SQL DB para qualquer recurso Azure (não só os do aluno). Mitigado: para workshop é aceitável (dados mock, RG efêmero, teardown ao final); em produção real usaria Private Endpoint. Documentar como nota didática ("em prod, restrinja com Private Endpoint").
- ⚠️ **Key Vault adiciona 1 recurso a provisionar por aluno:** pequeno custo de setup. Mitigado: Key Vault é barato (~US$0 em volume de workshop) e o passo entra no PORTAL-GUIDE.

---

## Alternatives Considered (rejeitadas)

### Alt 1: Manter SQL em VM e usar Functions em Premium plan com VNet integration

- **Rejected porque:** Premium plan custa muito acima do Consumption (~grátis), estourando o cost model do evento (SC-5: ≤ US$15/aluno). Adiciona configuração de VNet/subnet/integration que não é objetivo didático de F1. Azure SQL DB resolve o mesmo problema sem custo de plano.

### Alt 2: SQL em VM com Private Endpoint + VNet + Functions Premium

- **Rejected porque:** mesma objeção de custo da Alt 1, agravada por complexidade de rede (Private DNS zones, NSGs) que afasta a audiência polyglot do foco da aula. Contradiz o pattern Portal-first simples.

### Alt 3: Connection string hardcoded por Function (sem Key Vault)

- **Rejected porque:** quebra o requisito do owner ("migração de origem muda 1 setting"); duplica a string em N Functions com risco de divergência; expõe senha em App Settings/repo. Anti-padrão de segurança.

### Alt 4: Banco separado para v2 (Azure SQL DB nova, não a mesma do v1)

- **Rejected porque:** quebra ADE-000 Invariante 1/2 (mesma DB, schema aditivo, paralelismo didático v1/v2 na mesma tabela `purchases`). Fragmentaria os dados e o Flow Visualizer (F6) perderia a comparação lado-a-lado.

---

## Validation

Esta baseline é considerada **validada** quando:

- [ ] Pré-flight checklist do EPIC-002 confirma Azure SQL DB ativa (não SQL em VM) antes de F1.
- [ ] `SqlConnectionString` em todas as Functions v2 é Key Vault reference (auditável via App Settings — nenhuma string em texto claro).
- [ ] `V1BackendUrl` existe como App Setting (não hardcoded em código).
- [ ] Smoke test de F1 grava em Azure SQL DB com sucesso a partir de Function Consumption (prova que a rota de rede funciona).
- [ ] (Estado-alvo) Managed Identity da Function App tem `db_datawriter` na Azure SQL DB — verificável quando F3 habilitar.

## Impact on EPIC-002

### Stories impactadas

| Story | Impacto | Ação |
|---|---|---|
| **2.1 (F1)** | Já está `Ready` e referencia "mesma DB do v1". Esta ADE **confirma** que essa DB é Azure SQL Database (estado pós-EPIC-001 S4), não SQL em VM. **Não força re-draft** — a story não contradiz a baseline; recomenda-se apenas adicionar nos Dev Notes a referência "SQL = Azure SQL DB conforme ADE-003 Inv 2" e a App Setting `SqlConnectionString` via Key Vault reference (ADE-003 Inv 3). Como 2.1 está `Ready`, qualquer ajuste de Dev Notes é a critério do @po/@sm (não bloqueia). |
| **2.2–2.6** | Herdam a baseline. Devem referenciar `SqlConnectionString` (Key Vault) e `V1BackendUrl` ao invés de assumir conexão direta. |

### Artefatos a atualizar (fora do escopo desta ADE — apontados para os owners)

- **EPIC-002 (`docs/epics/EPIC-002-living-lab-workshop.md`):** adicionar pré-flight "EPIC-001 S4 (Azure SQL DB) concluído" às pré-condições do epic — **@pm**.
- **Blueprint seção 3 / 10:** anotar que a camada de dados v2 é Azure SQL Database (não SQL em VM) e que isso é restrição de Consumption plan — **@pm**.
- **Pré-flight checklist do aluno:** incluir "confirme Azure SQL DB ativa" — **@analyst** (S2.7) / **@pm**.

---

**Authority:** Aria (Architect) — designado por @aiox-master para foundational/baseline patterns.
**Review cycle:** Imutável durante EPIC-002. Mudanças → nova ADE que a supersede.
