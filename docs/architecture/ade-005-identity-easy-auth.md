# ADE-005 — Identidade via App Registration (tenant workforce) + Easy Auth (substitui Entra External ID)

> **Tipo:** Architecture Decision Entry (component substitution + decisão de mapping de identidade)
> **Status:** ✅ Accepted
> **Date:** 2026-06-03
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 F3 (`phase-03-identity`) e fases dependentes (F5 chatbot, F6 visualizer)
> **Supersedes:** **ADE-001** (mapping IDs Entra GUID ↔ IDs locais int — pendente, agora resolvido) · Decisão de stack "Microsoft Entra External ID" do blueprint seção 3/8
> **Related:** ADE-000 (microsserviço paralelo, Invariante 1), ADE-003 (baseline PaaS + front em App Service), ADE-004 (validação de JWT no gateway YARP)

---

## Context

Esta ADE foi gerada em sessão de re-escopo do EPIC-002 com o owner (Guilherme Prux Campos) em 2026-06-03.

O blueprint original (seção 3, seção 8 e fase F3) adotava **Microsoft Entra External ID** (CIAM, free tier 50K MAU) como provedor de identidade do customer no fluxo v2, com social login e OIDC, substituindo o JWT+bcrypt local apenas no v2. A story 2.3 (`phase-03-identity`, Draft) materializa isso e carrega a **decisão arquitetural pendente** sobre mapping de IDs Entra (GUID) ↔ IDs locais (int) — que estava reservada para a futura **ADE-001** (referenciada em ADE-000, na story 2.3 AC-7/Task 2, e nos riscos #2/#6 do epic e #6 do blueprint).

Na sessão de re-escopo, o owner identificou que o **Entra External ID introduz atrito alto e desproporcional** para um evento gratuito:

- Exige **tenant External ID separado** (CIAM tenant distinto do tenant workforce), pré-criado e gerido pelo instrutor (Risco #2 do epic: "atrito de setup External ID").
- Exige **user flows** (sign-up/sign-in flows, branding, attribute collection) — cerimônia de configuração que consome tempo de aula sem ser o objetivo central.

A decisão (fechada com o owner — esta ADE apenas a formaliza) é **remover o Entra External ID e usar o tenant Entra ID workforce que o aluno já possui (o da sua subscription Azure free trial), via App Registration + Easy Auth (App Service Authentication)**. Isso entrega OIDC/OAuth2 + social login (Google/GitHub) com custo US$0, sem tenant externo e sem código de autenticação manual.

**Consequência arquitetural-chave:** ao usar o tenant workforce, o claim `oid` (object id do usuário no Entra) é uma chave de identidade estável e já existente — **isso resolve e aposenta a ADE-001**: não há mais necessidade de inventar uma estratégia de mapping GUID↔int separada; o `oid` é a chave. Esta ADE, portanto, **supersede a ADE-001** e fecha a decisão pendente.

## Decision

Adotamos o pattern **"Workforce Entra + Easy Auth no front, JWT validado no gateway"** com 5 invariantes:

### Invariante 1: Identidade do v2 é o tenant Entra ID workforce do aluno + App Registration — não External ID

A identidade do fluxo v2 usa o **tenant Entra ID workforce** que o aluno já tem (vinculado à sua subscription Azure free trial). O aluno cria uma **App Registration** nesse tenant (`student-<iniciais>-v2`). Não há tenant External ID, não há user flows. Social login (Google/GitHub) é configurado como **identity provider federado** no Easy Auth / na App Registration, sem tenant CIAM separado.

A camada **admin** continua coberta por uma App Registration com **App Roles** (`Admin`, `Operator`, `Viewer`) no mesmo tenant workforce — exatamente como o blueprint já previa para o admin (a parte "App Registration interno" do blueprint nunca dependeu de External ID).

### Invariante 2: O front (SPA Vite/React) é protegido por OIDC; caminho RECOMENDADO = MSAL.js (SPA + PKCE), com Easy Auth como alternativa

Há dois caminhos viáveis para autenticar o SPA. **Recomendo o caminho (b) MSAL.js**, marcado como **ponto de validação no design da story 2.3**:

**Caminho (b) — RECOMENDADO — MSAL.js no browser + App Registration tipo SPA (PKCE):**
- App Registration do tipo **SPA** (platform = Single-page application), com **Authorization Code Flow + PKCE** (sem client secret no browser).
- `@azure/msal-browser` (+ `@azure/msal-react`) no front faz login, obtém o **access token** e o envia como `Authorization: Bearer <token>` nas chamadas à API.
- O **gateway YARP valida o JWT** (`AddJwtBearer`, ADE-004 Invariante 4) contra o discovery do issuer Entra; extrai e propaga `oid` downstream.
- Coerente com a story 2.3 atual (Task 8.1 já previa "Botão Login com MSAL.js") e com ADE-004 (validação de token em código no gateway).

**Caminho (a) — ALTERNATIVA documentada — Easy Auth server-side no App Service do front:**
- Easy Auth (App Service Authentication) protege o App Service que serve o SPA; o usuário é redirecionado ao login Entra automaticamente, sem código de auth.
- O SPA lê o usuário em `/.auth/me`; chamadas server-side recebem o header `X-MS-CLIENT-PRINCIPAL` (Base64 com claims, incl. `oid`).
- Requer que **front e API compartilhem o contexto de Easy Auth** (mesmo App Service ou propagação do principal) — acopla a proteção da API ao mecanismo de header injetado pelo App Service, em vez de validar JWT de forma uniforme no gateway.

**Por que recomendo (b):** valida o token **uniformemente no gateway YARP** (ADE-004) — o mesmo lugar onde rate-limit/cache/transform já vivem — em vez de depender do header `X-MS-CLIENT-PRINCIPAL` injetado pelo App Service, que só existe se a API estiver atrás do mesmo Easy Auth. (b) mantém o gateway como ponto único de validação de identidade (objetivo pedagógico de F2/F3 preservado: "o gateway valida o JWT"), alinha com o código MSAL.js já esboçado na story 2.3, e desacopla a API de uma topologia específica de hosting do front. O caminho (a) fica documentado como alternativa "zero-código-de-auth" para quem priorizar simplicidade de front sobre uniformidade de validação.

> **Easy Auth ainda tem papel mesmo no caminho (b):** proteger o **App Service do front** com Easy Auth (login Entra antes de servir o SPA) é opcional e complementar — útil como camada de "ninguém acessa o front sem estar logado". Mas a **autorização das chamadas à API** é feita pelo Bearer token validado no YARP, não pelo Easy Auth. Onde Easy Auth proteger o front, isso **pressupõe o front em App Service** (Easy Auth é recurso de App Service), o que é coerente com ADE-003 (baseline PaaS — front em App Service após EPIC-001).

### Invariante 3: O claim `oid` do Entra workforce é a chave de identidade do usuário — ADE-001 fica resolvida

O **`oid`** (Object ID, GUID estável do usuário no tenant) é a chave canônica de identidade do v2. A tabela `purchases` (e/ou `users`) recebe o `oid` diretamente:

- Schema delta (idempotente, conforme ADE-000 Invariante 2): `ALTER TABLE purchases ADD entra_oid UNIQUEIDENTIFIER NULL;` (e índice). A coluna em `users` (`users.entra_oid`) é a opção limpa se o vínculo for por usuário.
- **Não há mapping GUID↔int a inventar:** o `oid` é estável e único; vincula-se diretamente como coluna. Isso é exatamente a "opção B" que a ADE-001 listaria (coluna `entra_oid`) — agora decidida aqui sem precisar de uma ADE-001 separada, porque o uso do tenant workforce torna o `oid` a chave natural. **Esta invariante supersede a ADE-001.**

### Invariante 4: O gateway YARP valida o JWT e propaga `oid` downstream (integração com ADE-004)

A validação de token acontece no **gateway YARP** (ADE-004 Invariante 4), não em policy APIM:

- YARP usa `AddAuthentication().AddJwtBearer(...)` apontando para o discovery do issuer Entra workforce: `https://login.microsoftonline.com/<tenant-id>/v2.0/.well-known/openid-configuration`.
- Valida `iss`, `aud` (= client id / App ID URI da App Registration), assinatura (chaves do JWKS) e expiração.
- Extrai o claim `oid` e propaga downstream para a Function como header `X-Entra-OID` (a Function grava `entra_oid` no SQL conforme Invariante 3). A Function NUNCA confia em header de identidade que não tenha passado pela validação do gateway.

### Invariante 5: Requisitos de configuração de identidade (contrato de setup)

A configuração de identidade do v2 exige (contrato a refletir no PORTAL-GUIDE da F3):

**App Registration (tenant workforce do aluno):**
- App `student-<iniciais>-v2`, platform **SPA** (para caminho b) com redirect URI do front (localhost dev + URL prod do App Service/Static Web App).
- Expor um scope/API (App ID URI) que o access token carregará no `aud`, para o YARP validar.
- App Roles (`Admin`, `Operator`, `Viewer`) para a camada admin.
- Identity providers sociais (Google e/ou GitHub) federados.

**Se usar Easy Auth (caminho a, ou proteção complementar do front no caminho b) — App Service Authentication:**
- Auth provider = Microsoft (aad) usando a App Registration acima.
- Redirect URI do callback: **`/.auth/login/aad/callback`**.
- **Client secret gerenciado** pelo App Service (Easy Auth armazena o secret da App Registration na configuração de auth do App Service — não no código/repo; pode referenciar Key Vault, coerente com ADE-003 Invariante 3).
- Social providers (Google/GitHub) habilitados como identity providers adicionais no painel Authentication do App Service.
- Endpoints expostos pelo Easy Auth: `/.auth/me` (claims do usuário), `/.auth/login/<provider>`, `/.auth/logout`; header server-side `X-MS-CLIENT-PRINCIPAL`.

---

## Rationale

### Por que tenant workforce + App Registration + Easy Auth (vs External ID)?

- **Zero tenant externo:** o aluno usa o tenant Entra que **já existe** na sua subscription. External ID exigiria criar/gerir um tenant CIAM separado (Risco #2 do epic) — atrito que some inteiramente.
- **Zero user flows:** External ID precisa de sign-up/sign-in flows configurados; com App Registration + Easy Auth o login Entra/social funciona sem essa cerimônia.
- **Custo US$0:** ambos são free, mas o caminho workforce elimina a infra extra do CIAM tenant.
- **Social login mantido:** Google/GitHub federados cobrem o objetivo "social login" do blueprint sem External ID.
- **`oid` resolve o mapping de graça:** ao usar o tenant workforce, o `oid` é a chave de identidade estável — aposenta a decisão pendente da ADE-001 (não precisa inventar tabela de mapping nem estratégia de duplicação).

### Por que o caminho (b) MSAL.js é recomendado (vs Easy Auth server-side)?

- **Uniformidade de validação:** o JWT é validado **no gateway YARP**, o mesmo ponto onde rate-limit/cache/transform vivem (ADE-004). Um único lugar valida identidade — objetivo pedagógico claro ("o gateway é o guardião").
- **Desacopla API de hosting do front:** (a) exige que a API receba `X-MS-CLIENT-PRINCIPAL`, o que pressupõe a API atrás do mesmo Easy Auth/App Service do front — acoplamento de topologia. (b) só precisa de um Bearer token, agnóstico de onde o front roda.
- **Coerência com a story 2.3 já esboçada:** Task 8.1 da 2.3 já citava MSAL.js. (b) confirma o caminho com menor reescrita.
- **PKCE sem secret no browser:** o fluxo SPA + PKCE é o padrão moderno e seguro para front público (sem client secret exposto).
- Easy Auth (a) continua excelente para **proteger o front** com zero código — por isso fica como alternativa/complemento, não descartado.

### Por que `oid` como chave (vs tabela de mapping da ADE-001)?

- `oid` é **estável e único por usuário no tenant** — propriedade ideal de chave. Uma tabela de mapping seria indireção desnecessária quando a própria coluna `entra_oid` resolve.
- Mantém o schema delta **aditivo e idempotente** (ADE-000 Invariante 2): só uma coluna nova + índice.
- Preserva a comparação didática v1/v2: o v1 segue com `user_id` int + bcrypt; o v2 grava `entra_oid` ao lado — lado-a-lado na mesma tabela `purchases` (ADE-000 Invariante 1).

---

## Consequences

### Positivas

- ✅ Elimina o Risco #2 do epic ("atrito de setup External ID") — não há tenant CIAM nem user flows.
- ✅ Resolve e fecha a decisão pendente de mapping (ADE-001) com `oid` como chave — uma coluna aditiva, zero indireção.
- ✅ Custo US$0, sem infra extra de CIAM tenant.
- ✅ Validação de JWT uniforme no gateway YARP (ADE-004) — identidade tem um único guardião.
- ✅ Social login (Google/GitHub) preservado; objetivo pedagógico de CIAM/OIDC mantido (o aluno ainda aprende OIDC, OAuth2 code flow + PKCE, JWT, claims, App Roles).
- ✅ Coerente com a baseline PaaS (ADE-003): front em App Service habilita Easy Auth como camada complementar.

### Negativas / Trade-offs aceitos

- ⚠️ **Perde-se o produto "External ID" do currículo:** quem queria especificamente CIAM/External ID não o verá. Mitigado: o aluno aprende os mesmos conceitos (OIDC, social login, scopes, claims) via tenant workforce; documentar nota didática "em produtos B2C reais, o equivalente é o Entra External ID — aqui usamos o tenant workforce para reduzir atrito".
- ⚠️ **Usuários de negócio reais usariam um tenant workforce como provedor de "customer":** semanticamente, workforce ≠ CIAM. Mitigado: para um workshop com usuários-aluno (que já têm conta Entra), é apropriado e didaticamente honesto, desde que a nota acima seja feita.
- ⚠️ **Caminho (b) exige código MSAL.js no front** (vs zero-código do Easy Auth). Mitigado: é pouco código, padrão de mercado, e ensina o fluxo OIDC SPA explicitamente — ganho didático.
- ⚠️ **Easy Auth como proteção do front pressupõe front em App Service** (não VM). Mitigado: já garantido pela baseline ADE-003 (front em App Service após EPIC-001 S4). Registrado como exceção em ADE-003 Invariante 1.

---

## Alternatives Considered (rejeitadas)

### Alt 1: Manter Microsoft Entra External ID (decisão original do blueprint)

- **Rejected porque:** exige tenant CIAM separado + user flows (Risco #2, atrito alto) — desproporcional para evento gratuito de 6h/fase. App Registration no tenant workforce + Easy Auth entrega OIDC + social login sem essa infra. Decisão original explicitamente substituída por esta ADE.

### Alt 2: ADE-001 com tabela de mapping `user_identity_mapping` (GUID↔int)

- **Rejected porque:** indireção desnecessária. Com o tenant workforce, o `oid` é chave estável e única — uma coluna `entra_oid` (a "opção B" da ADE-001) resolve sem tabela extra nem JOIN adicional. Esta ADE supersede a ADE-001 adotando a coluna direta.

### Alt 3: Duplicar registro de usuário em v2 (opção C da ADE-001)

- **Rejected porque:** fragmenta a identidade do usuário entre v1 e v2, quebra a comparação lado-a-lado na mesma tabela `purchases` (ADE-000 Invariante 1) e cria risco de divergência de dados. Coluna `entra_oid` é mais simples e aditiva.

### Alt 4: Caminho (a) Easy Auth server-side como solução principal (em vez de MSAL.js)

- **Rejected como principal (mantido como alternativa)** porque: acopla a validação de identidade da API ao header `X-MS-CLIENT-PRINCIPAL` injetado pelo App Service, exigindo a API atrás do mesmo Easy Auth — quebra a uniformidade de "o gateway YARP valida o JWT" (ADE-004) e acopla topologia de hosting. Mantido como opção válida "zero-código-de-auth" para proteção do front.

---

## Validation

Esta substituição é considerada **validada** quando:

- [ ] App Registration tipo SPA criada no tenant workforce do aluno; nenhum tenant External ID provisionado.
- [ ] Login OIDC (incl. social Google/GitHub) funciona no SPA via MSAL.js (caminho b) — aluno demonstra fluxo no browser.
- [ ] Access token chega ao gateway YARP como `Authorization: Bearer`; YARP valida `iss`/`aud`/assinatura e extrai `oid` (integração com ADE-004 Invariante 4).
- [ ] Function recebe `X-Entra-OID` (propagado pelo gateway) e grava `entra_oid` em `purchases`/`users` (Invariante 3).
- [ ] Schema delta `phase-03.sql` adiciona `entra_oid` de forma idempotente (ADE-000 Invariante 2).
- [ ] App Roles (Admin/Operator/Viewer) presentes na App Registration admin.
- [ ] (Se usado) Easy Auth protege o App Service do front com callback `/.auth/login/aad/callback` e secret gerenciado.
- [ ] README de F3 mantém a tabela comparativa v1 (bcrypt+JWT local) vs v2 (OIDC Entra) — ajustada para "tenant workforce" em vez de "External ID".

## Impact on EPIC-002

### Stories que precisam de re-draft

| Story | Impacto | Ação (executor) |
|---|---|---|
| **2.3 (F3)** | **Re-draft significativo.** Está como "External ID + App Registration" (Status Draft). Precisa virar "App Registration (workforce) + Easy Auth/MSAL": AC-2 (tenant External ID pré-provisionado) **removida** — não há tenant externo; AC-3/AC-4 viram "App Registration SPA no tenant workforce + social providers"; AC-6 (`validate-jwt`) passa a referenciar o **YARP `AddJwtBearer`** (ADE-004), não policy APIM; **AC-7 muda de alvo:** a decisão de mapping não vai mais para uma `ade-001-entra-id-mapping.md` a criar — está **resolvida nesta ADE-005** (coluna `entra_oid`, `oid` como chave); AC-9 lê `X-Entra-OID` propagado pelo YARP; Task 2 (criar ADE-001) **removida/substituída** por "seguir ADE-005"; Task 8.1 (MSAL.js) confirmada como caminho (b). Como está em Draft, re-draft é livre — **@sm**, com consulta a @architect já satisfeita por esta ADE. |
| **2.5 / 2.6 (F5/F6)** | Impacto leve: onde dependiam de identidade do usuário, usam `oid`/`entra_oid` — sem mudança estrutural. Referenciar ADE-005 nos Dev Notes — **@sm** quando draftar. |

> **NÃO re-drafto as stories** (autoridade de @sm). Esta ADE aponta o impacto e fecha a decisão arquitetural que a 2.3 esperava do @architect.

### Artefatos a atualizar (apontados para os owners)

- **Blueprint seção 3 (stack table):** substituir "Microsoft Entra External ID (Free 50K MAU)" por "App Registration (tenant workforce) + Easy Auth / MSAL.js — US$0" — **@pm**.
- **Blueprint seção 8 (Identity Strategy):** reescrever camada customer (workforce + App Reg, não External ID); **remover a "Decisão arquitetural pendente"** de mapping — está resolvida nesta ADE — **@pm**.
- **Blueprint seção 4 F3:** reescrever escopo de F3 (sem tenant External ID) — **@pm**.
- **EPIC-002:** stack list (linha 25), tabela de stories S3 (título), **Risco #2** (atrito External ID — eliminado), **Risco #6 + linha de "Decisões pendentes" sobre mapping Entra↔local** (resolvido por esta ADE) — **@pm**.
- **ADE-000 "Related":** a referência "ADE-001 (Entra ID mapping — pendente S2.3)" deve passar a apontar "resolvida/superseded por ADE-005" — **@architect** (registrado aqui).
- **ADE-001:** não existe arquivo a editar (nunca foi criada). Esta ADE-005 ocupa formalmente a decisão que a ADE-001 cobriria; a numeração ADE-001 fica **retirada/aposentada** (não reutilizar para outro assunto). ADE-002 (MCP SDK pinning) permanece pendente, intocada.

---

**Authority:** Aria (Architect) — designado por @aiox-master para decisões de seleção de tecnologia, identidade e integração.
**Review cycle:** Imutável durante EPIC-002. Mudanças → nova ADE que a supersede.
