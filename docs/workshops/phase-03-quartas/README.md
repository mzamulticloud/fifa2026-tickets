# Quartas de Final — Identidade dois-mundos (cliente CIAM + admin workforce + migração v1→CIAM)

> **Leitura prévia obrigatória** · Workshop "Living Lab Azure-Native" · Lab "Quartas de Final" (F2/F3 unificados)
> **Tempo estimado de leitura:** 35-45 min · **Faça ANTES da aula.**
> **Story:** [2.11](../../stories/2.11.story.md) · **Decisões de arquitetura:** [ADE-007 v1.1](../../architecture/ade-007-identity-external-id.md) (Invariantes 1-7, supersede ADE-005) · [migração](../../architecture/migration-v1-ciam-design.md) (@data-engineer) · [ADE-004](../../architecture/ade-004-gateway-yarp.md) (gateway issuer-agnóstico)
> **Continuidade:** parte cumulativa da [F1 (Oitavas)](../phase-01/README.md) e da [F2 (Gateway YARP)](../phase-02/README.md) — a identidade entra **na frente** do gateway que você construiu, e usa as Functions da F1 sem alterá-las.

---

## 0. Por que você está lendo isto antes da aula

Na F2 (Gateway YARP), você deixou uma porta **instalada e destrancada**: o `AddJwtBearer` estava configurado no gateway, mas as rotas eram anônimas — não havia `RequireAuthorization()`. Era de propósito: identidade não era o tema da F2. Agora é.

Nas **Quartas de Final** a porta ganha a chave — e a chave tem **duas fechaduras**, porque o app de ingressos tem **dois tipos de gente** entrando:

- O **cliente** (quem compra ingressos) — uma pessoa qualquer da internet, que faz **cadastro self-service**.
- O **admin** (operador interno) — um funcionário da sua organização, que já tem um "crachá corporativo".

Esses dois mundos usam **produtos diferentes** da Microsoft, com **URLs de login diferentes**, e é exatamente aqui que mora a confusão que esta leitura quer eliminar **antes** da aula. Se você chegar entendendo a diferença entre **Entra Connect**, **Entra ID** e **Entra External ID**, as ~7,5–9,5h de hands-on rendem o dobro.

> A frase âncora do lab: **"o cliente entra pelo External ID (`ciamlogin.com`); o funcionário entra pelo workforce (`login.microsoftonline.com`); o gateway valida os dois com a mesma mecânica — só muda a string da authority."**

Esta leitura cobre:

1. O **desenho canônico B2C** — dois mundos de identidade e por que eles são separados
2. A **nota de terminologia** que evita o erro nº1 do lab: Connect vs Entra ID vs External ID
3. **OIDC + PKCE** — como um SPA público faz login sem guardar segredo
4. **External ID vs Azure AD B2C legado** (depreciado — **não usar**)
5. O **gateway como guardião issuer-agnóstico** (o que muda vs a F2/F3)
6. A **tabela comparativa v1 (bcrypt homegrown) vs v2 (CIAM gerenciado)**
7. A **migração aditiva** v1→CIAM e a prova de coexistência na mesma linha de `users`
8. Glossário e checklist de pré-aula

> **Pré-requisitos de conhecimento:** você fez a F1 (fluxo `POST /purchase` → fila → consumer → SQL) e a F2 (gateway YARP com rate-limit, cache, CORS, JWT placeholder). Você programa em qualquer linguagem; não exigimos .NET nem experiência prévia com OAuth/OIDC. Se você já integrou "Login com Google" ou usou Auth0/Cognito/Firebase Auth, vai reconhecer os conceitos com nomes diferentes.

---

## 1. O desenho canônico B2C: dois mundos de identidade

### 1.1 O problema: quem é "o usuário"?

No fluxo v1 (homegrown), "usuário" era uma linha na tabela `users`: um `email`, uma senha em `bcrypt`, um `id` inteiro. Simples — e **todo o trabalho de identidade é seu**: você guarda o hash, faz o reset de senha, defende contra força bruta, implementa MFA se quiser. Isso é **identidade homegrown**.

Mas num produto B2C real existem **dois públicos completamente diferentes**, e tratá-los igual é um erro de arquitetura:

| | **Cliente (comprador)** | **Admin (operador)** |
|---|---|---|
| Quem é | Pessoa externa, anônima até se cadastrar | Funcionário interno da sua organização |
| Como entra | **Cadastro self-service** (cria a própria conta) | Já existe no diretório corporativo |
| Quantos | Milhões, em teoria | Punhado, controlado |
| Login social | Sim ("entrar com Google") | Não (usa a conta corporativa) |
| Produto Microsoft | **Entra External ID (CIAM)** | **Entra ID (workforce)** |
| URL de login | `<tenant>.ciamlogin.com` | `login.microsoftonline.com` |

> **A intuição que importa:** pense num estádio. O **cliente** compra o ingresso na **bilheteria pública** (qualquer um chega, se cadastra, compra) — isso é o **External ID**. O **funcionário** entra pela **portaria de serviço** com o **crachá** que a empresa já deu a ele — isso é o **workforce (Entra ID)**. São duas portas, dois públicos, dois produtos. Misturar as duas é deixar o público comprar ingresso pela portaria de serviço, ou dar crachá de funcionário pra cada torcedor.

### 1.2 A solução: dois tenants, um gateway

O Living Lab modela exatamente esse desenho canônico:

```
[Cliente — torcedor]                          [Admin — operador]
  cadastro self-service                          conta corporativa
        │                                              │
        ▼                                              ▼
[Entra External ID (CIAM)]                   [Entra ID (workforce)]
  <tenant>.ciamlogin.com                       login.microsoftonline.com
        │  JWT (oid, iss, aud)                        │  JWT (oid, roles=[Admin])
        └──────────────┬───────────────────────────┘
                       ▼
        [Gateway YARP — guardião único do JWT]
          valida AMBOS os issuers por discovery
          extrai oid → injeta X-Entra-OID downstream
                       │
                       ▼
        [Function F1 — INALTERADA] → entra_oid em SQL
```

O ponto pedagógico central: o gateway que você construiu na F2 **não precisa ser reescrito** para aceitar um segundo mundo de identidade. Ele valida JWT **por discovery** (busca a chave pública do emissor automaticamente). Aceitar um novo emissor é **configuração**, não código novo. É o que chamamos de **issuer-agnóstico** (seção 5).

---

## 2. Nota de terminologia: Connect ≠ Entra ID ≠ External ID

> **Esta seção é a mais importante da leitura.** O erro nº1 do lab é confundir esses três produtos — todos começam com "Entra", todos são da Microsoft, e dois deles têm URLs de login parecidas mas **diferentes**. Decore esta tabela.

| Produto | O que é | Para quem | URL de login | Usamos no lab? |
|---|---|---|---|---|
| **Microsoft Entra Connect** | Ponte de **sincronização** de um Active Directory **on-premises** para a nuvem | Empresas com AD local | *(não é um login — é sync)* | ❌ **Irrelevante aqui.** Só citamos para desambiguar. |
| **Microsoft Entra ID** (ex-"Azure AD") | Identidade **workforce / B2B** — o "crachá de funcionário" | Funcionários, parceiros internos | **`login.microsoftonline.com`** | ✅ Para o **admin** (Bloco 3) |
| **Microsoft Entra External ID** | Identidade **CIAM / B2C** — cadastro de **cliente** | Clientes finais, externos | **`<tenant>.ciamlogin.com`** | ✅ Para o **cliente** (Bloco 2) |

Três armadilhas que esta tabela mata:

1. **"Entra Connect é o login do cliente"** → ❌ Não. Connect é sincronização de AD on-prem; não tem nada a ver com login de cliente. O nome parecido confunde.
2. **"Cliente entra pelo `login.microsoftonline.com`"** → ❌ Não. Esse é o workforce (funcionário). O cliente entra pelo **`ciamlogin.com`**. Apontar a authority do MSAL para o domínio errado é o bug clássico (gera `AADSTS50011`, "redirect URI mismatch" / authority inválida).
3. **"External ID e Azure AD B2C são a mesma coisa"** → Quase. O External ID é o **sucessor** do Azure AD B2C. O B2C é **legado e está em depreciação** (seção 4). No lab usamos **só** o External ID.

> **Por que `ciamlogin.com` e não `microsoftonline.com`?** O External ID é um tenant **separado** do workforce, com seu próprio endpoint de autenticação. A Microsoft escolheu o domínio `ciamlogin.com` (CIAM = **C**ustomer **I**dentity and **A**ccess **M**anagement) justamente para deixar o "mundo do cliente" visualmente distinto do "mundo do funcionário". Quando você vir `ciamlogin.com` numa URL, pense **"cliente"**; quando vir `login.microsoftonline.com`, pense **"funcionário"**.

---

## 3. OIDC + PKCE: como um SPA loga sem guardar segredo

O frontend de ingressos é um **SPA** (Single-Page Application) — código JavaScript que roda **no navegador do usuário**. Isso cria um problema de segurança clássico: **um SPA não pode guardar um segredo**. Qualquer "client secret" embutido no JavaScript é visível para qualquer um que abra o DevTools. Então como o SPA prova que é ele mesmo ao trocar o código de autorização por um token?

A resposta é **OIDC (OpenID Connect)** com o fluxo **Authorization Code + PKCE**.

### 3.1 Os conceitos em uma frase cada

- **OAuth 2.0** — o protocolo de **autorização** (dar acesso a um recurso sem entregar a senha). É o "como o app ganha um token".
- **OIDC (OpenID Connect)** — uma camada de **autenticação** **em cima** do OAuth 2.0 (provar **quem você é**, não só dar acesso). Adiciona o **ID token** e claims padronizados como `oid`, `iss`, `aud`.
- **Authorization Code Flow** — o fluxo onde o app recebe primeiro um **código** de curta duração e depois o **troca** por tokens. Mais seguro que entregar o token direto no navegador.
- **PKCE** (Proof Key for Code Exchange, lê-se "pixy") — a peça que torna o Authorization Code Flow seguro **sem client secret**. O SPA gera um segredo **temporário e descartável** (o `code_verifier`) por login; envia só um hash dele (o `code_challenge`) ao pedir o código; e revela o `code_verifier` original só na hora de trocar o código por token. Quem interceptar o código no meio **não consegue** trocá-lo por token, porque não tem o `code_verifier`.

### 3.2 O fluxo, passo a passo (o que acontece no Bloco 2)

```
1. Usuário clica "Entrar" no SPA
2. SPA gera code_verifier (segredo temporário) + code_challenge (hash dele)
3. SPA redireciona o navegador para o External ID (ciamlogin.com),
   levando o code_challenge
4. Usuário se autentica (Google, ou email + OTP) na tela do External ID
5. External ID devolve um CÓDIGO de autorização ao SPA (não o token ainda)
6. SPA troca o código por tokens, agora revelando o code_verifier original
7. External ID confere: o hash do code_verifier bate com o code_challenge? → emite os tokens
8. SPA recebe o access token (JWT) e o envia ao gateway como Authorization: Bearer <token>
```

> **A boa notícia:** você **não implementa** isso à mão. A biblioteca **MSAL** (`@azure/msal-browser`) faz todo o PKCE por baixo dos panos. No código (`authV2.ts`) você só configura a **authority** e o **clientId**; o MSAL cuida do `code_verifier`, do `code_challenge`, do redirect e da troca. O que **muda** das Quartas vs a F3 é **uma linha**: a authority deixa de apontar para o workforce e passa a apontar para o CIAM (seção 5).

---

## 4. External ID vs Azure AD B2C legado (NÃO use o B2C)

A Microsoft já teve **dois** produtos de CIAM. É fácil esbarrar no errado ao buscar no portal ou no Google — por isso este aviso explícito.

| | **Microsoft Entra External ID** | **Azure AD B2C** (legado) |
|---|---|---|
| Status | ✅ **Atual** — o produto vivo de CIAM | ⚠️ **Depreciado** — em fim de vida |
| URL | `<tenant>.ciamlogin.com` | `<tenant>.b2clogin.com` |
| Recomendação Microsoft | É para onde a Microsoft direciona novos projetos | Fim de venda anunciado; sem novos recursos |
| Usamos no lab? | ✅ **Sim, exclusivamente** | ❌ **Nunca provisionado** (AC-17) |

> **Regra do lab (AC-17):** **nenhum recurso Azure AD B2C é criado em momento algum.** No portal, ao criar o tenant externo, confirme que você está no **External ID** (`ciamlogin.com`), não no B2C (`b2clogin.com`). O PORTAL-GUIDE sinaliza o caminho correto a cada passo. Ensinar um produto em depreciação seria antipedagógico — você sairia daqui sabendo operar algo que a Microsoft está aposentando.

Como diferenciar na prática: se a URL de login termina em **`b2clogin.com`**, é o **legado** — pare e revise. Se termina em **`ciamlogin.com`**, é o **External ID** — correto.

---

## 5. O gateway como guardião issuer-agnóstico (o que muda vs F2/F3)

### 5.1 O delta é minúsculo (e essa é a lição)

Aqui está o coração técnico das Quartas. Apesar de adicionarmos **um produto inteiro de identidade** (External ID), o código que muda é **ridiculamente pequeno** — porque tanto o MSAL quanto o `AddJwtBearer` do ASP.NET Core são **issuer-agnósticos**: eles validam tokens **por discovery** (buscam a configuração e as chaves públicas do emissor a partir de uma URL `.well-known`).

```
[authV2.ts]  authority: <tenant>.ciamlogin.com    ← MUDA (era login.microsoftonline.com)
[Program.cs] AddJwtBearer authority: ciamlogin     ← MUDA (era microsoftonline)
[Program.cs] AddJwtBearer("Admin"): workforce      ← NOVO (Bloco 3 — segundo emissor)

[PurchaseEntryFunction]                            ← NÃO muda (issuer-agnóstico)
[purchases.entra_oid]                              ← NÃO muda (coluna já existe)
[oid → X-Entra-OID → entra_oid pipeline]           ← NÃO muda
```

> **A prova de que o gateway é issuer-agnóstico:** ele valida o token do CIAM com a **mesma mecânica** que validava o token do workforce. Só muda a **string da authority** que aponta o discovery. Em código, o gateway extrai o claim `oid`, faz strip do header `X-Entra-OID` de entrada (anti-spoofing) e injeta o `oid` validado downstream — **exatamente** como na F3. A coluna `entra_oid` em SQL **não sabe nem se importa** de qual tenant veio o GUID.

### 5.2 O encontro Gateway × Identidade (o clímax do Bloco 2)

O `AddJwtBearer` apontando o discovery para o CIAM é onde os dois temas das Quartas se encontram:

> "O gateway é o **guardião**: ele valida o JWT que o **External ID emitiu** antes de deixar a requisição passar para a Function."

O discovery do CIAM (que o `AddJwtBearer` busca automaticamente) é:

```
https://<tenant>.ciamlogin.com/<tenantId>/v2.0/.well-known/openid-configuration
```

Desse documento o gateway extrai o `jwks_uri` (de onde busca as chaves públicas RS256 para validar a assinatura) e o `issuer` (que ele valida contra o `iss` do token). É tudo automático — você só fornece a authority.

### 5.3 Dois eixos de `entra_oid` (não confunda)

O `oid` do cliente aterrissa em **dois lugares diferentes** no banco, com papéis distintos. Guarde esta distinção — ela explica por que há uma migration nova no Bloco 4:

| Coluna | Eixo | Significa | Origem | Schema |
|---|---|---|---|---|
| **`purchases.entra_oid`** | **Compra** (transacional) | "o `oid` de quem fez ESTA compra v2" | pipeline `oid → X-Entra-OID` (a cada compra) | **já existe** (`phase-03.sql`, F3) — índice filtrado **NÃO-unique** |
| **`users.entra_oid`** | **Cadastro** (durável) | "o `oid` CIAM vinculado a ESTE usuário v1" | a **migração** hands-on (Bloco 4) | **DDL nova** (`phase-04-ciam-link.sql`) — índice **UNIQUE** filtrado |

> **Por que dois eixos?** A coluna `purchases.entra_oid` só é preenchida **quando uma compra v2 acontece** — é transacional. Um usuário v1 que **migra o cadastro mas ainda não comprou no v2** não teria onde seu `oid` aterrissar de forma durável. A coluna `users.entra_oid` resolve: é o **vínculo de cadastro**, único por usuário (chave de match = `users.email`, que é `UQ_users_email`). É ela que prova a coexistência no clímax do Bloco 4 (seção 7). Detalhe completo em [migration-v1-ciam-design.md](../../architecture/migration-v1-ciam-design.md).

---

## 6. Comparação v1 (bcrypt homegrown) vs v2 (CIAM gerenciado)

Esta é a tabela que o lab inteiro quer cravar na sua cabeça. À esquerda, a identidade **que você gerencia** (v1, intocada). À direita, a identidade **que a Microsoft gerencia** (v2 CIAM).

| Aspecto | **v1 (homegrown — intocado)** | **v2 (CIAM — Quartas)** |
|---|---|---|
| Storage de senha | `bcrypt` (10 rounds) em **`users.password`** ¹ | **Não armazenado** — a Microsoft gerencia a credencial |
| Token | JWT **HS256** local (Express, `jsonwebtoken`) | JWT **RS256** do Entra External ID |
| Issuer (`iss`) | `fifa2026-api` (local) | `<tenant>.ciamlogin.com/<tenantId>/v2.0` |
| Validação do token | Middleware Express manual | `AddJwtBearer` no gateway YARP (por discovery) |
| Login social (Google) | ❌ Não | ✅ Sim (via CIAM) |
| Cadastro self-service | ❌ Não (admin cria) | ✅ Sim (user flow do CIAM) |
| MFA / brute-force / reset de senha | **Você** implementa e mantém | **A Microsoft** cuida |
| Produto Microsoft | — (custom) | Microsoft Entra External ID (sucessor do B2C) |
| Vínculo de cadastro (durável) | `users.id` (int) | **`users.entra_oid`** (GUID, via `phase-04-ciam-link.sql`) |
| Vínculo por compra (transacional) | `purchases.user_id` → `users.id` | **`purchases.entra_oid`** (oid da compra v2, zero-DDL) |

> ¹ **Atenção à coluna real:** o hash bcrypt vive em **`users.password`** (NÃO `users.password_hash`). Confirmado em [`schema.sql`](../../../fifa2026-api/database/schema.sql) (linha 20: `password NVARCHAR(255) NOT NULL`). Esta é uma divergência que o design da migração reportou honestamente — toda query do lab usa `users.password`.

### A lição central: a senha bcrypt NÃO migra (e isso é de propósito)

Quando você migrar um usuário v1 para o CIAM (Bloco 4), a senha bcrypt **não vai junto**. O External ID **não aceita** importar um hash `bcryptjs` como credencial. O usuário migrado **estabelece uma credencial nova** no CIAM (Google ou email+OTP), e o `users.password` bcrypt **permanece intacto** no caminho v1.

Isso pode parecer uma limitação a contornar — mas é **a própria lição**:

> "No mundo gerenciado, a Microsoft cuida da credencial; você só guarda o `oid`. O bcrypt continua aqui ao lado, intacto, para você comparar. Veja: com identidade gerenciada, você nem tem mais um hash para guardar."

Resultado: **o mesmo humano passa a ter duas credenciais independentes** — bcrypt no v1, identidade gerenciada no CIAM. As duas coexistem. Esse é o ápice didático (seção 7).

---

## 7. A migração aditiva e a prova de coexistência

A migração das Quartas é **aditiva**, não destrutiva. Ela **vincula** o usuário v1 ao CIAM — não apaga nada. O bcrypt fica; o `id` fica; **adiciona-se** o `entra_oid`.

O mecanismo (resolvido pelo @data-engineer — Opção C híbrida):

1. **Sign-up self-service no CIAM** com o **mesmo email** do `users` v1 (reusa o que você aprendeu no Bloco 2).
2. **Captura do `oid`** emitido pelo CIAM (via app ou via Portal).
3. **`UPDATE users SET entra_oid = @oid WHERE email = @email AND entra_oid IS NULL`** — idempotente (o `WHERE entra_oid IS NULL` + índice UNIQUE filtrado garantem que rodar 2x não duplica nada).

E a **prova** — o clímax do lab — é uma query que mostra, **na mesma linha de `users`**, as duas identidades coexistindo:

```sql
SELECT u.id, u.email,
       CASE WHEN u.password LIKE '$2%' THEN 'bcrypt-presente' ELSE 'sem-bcrypt' END AS credencial_v1,
       u.entra_oid AS oid_ciam_v2,
       CASE WHEN u.password IS NOT NULL AND u.entra_oid IS NOT NULL
            THEN 'COEXISTE (v1 bcrypt + v2 CIAM)'
            WHEN u.entra_oid IS NULL THEN 'so v1 (nao migrou)'
            ELSE 'estado inesperado' END AS status_migracao
FROM dbo.users u
WHERE u.email = @email;
```

Resultado esperado para o usuário migrado: `status_migracao = 'COEXISTE (v1 bcrypt + v2 CIAM)'`. **Uma linha, duas identidades, dois paradigmas — homegrown e gerenciado — vivos lado a lado.** É a prova visual de que modernizar não exige destruir.

---

## 8. O que vamos construir (arquitetura delta sobre F1+F2)

```
[Browser SPA — Vite/React]
   │ CIAM login via MSAL.js (ciamlogin.com — Bloco 2)
   │ Admin login via MSAL.js (login.microsoftonline.com — Bloco 3)
   │ POST /purchase + Authorization: Bearer <token-CIAM>
   ▼
[Container App: gateway-<iniciais>  (ASP.NET Core + YARP .NET 8)]
   ├─ AddCors / AddRateLimiter (429) / AddOutputCache (30s)  ← Bloco 1 (já existe, F2)
   ├─ AddJwtBearer (CIAM) → discovery ciamlogin.com          ← Bloco 2 (muda de F3)
   ├─ AddJwtBearer ("Admin") → discovery microsoftonline.com ← Bloco 3 (novo emissor)
   ├─ Extrai oid → injeta X-Entra-OID (anti-spoofing)        ← herdado da F3
   └─ MapReverseProxy → Function App F1
            │
            ▼
     [Function PurchaseEntryFunction — INALTERADA]
            ├─ Lê header X-Entra-OID
            └─ INSERT purchases com entra_oid = <oid-CIAM>  (resto do fluxo F1)

[phase-04-ciam-link.sql]  → users.entra_oid (eixo cadastro, preenchido na migração do Bloco 4)
```

> **O que NÃO tocamos:** a **Function F1** e seu pipeline (Service Bus → Consumer → SQL) ficam **intactos**. O fluxo v1 (`api.ts`, bcrypt, `users.password`) fica **intocado** — é o lado de comparação. A coluna `purchases.entra_oid` **não muda** (já existe da F3). O único schema novo é a coluna `users.entra_oid` (aditiva, idempotente, criada **pré-aula** pelo instrutor).

### Os 4 blocos do dia

| Bloco | Tema | O que você sai sabendo |
|---|---|---|
| **1 — Gateway YARP policies** | Borda segura como código | Rate-limit → 429, cache 30s, CORS, JWT — tudo em C# legível (revisão da F2) |
| **2 — Cliente CIAM** | B2C real, identidade gerenciada | External ID é o CIAM correto; OIDC/PKCE; o gateway valida o JWT que o CIAM emitiu; `oid → X-Entra-OID → entra_oid` intacto |
| **3 — Admin workforce + App Roles** | Dois mundos de identidade | Cliente no CIAM, funcionário no workforce; gateway issuer-agnóstico valida ambos; App Role `Admin` |
| **4 — Migração v1→CIAM** | Modernização sem destruição | Migração aditiva: bcrypt v1 e `entra_oid` CIAM coexistem na mesma linha de `users` |

> **Ponto de pausa natural:** ao **fim do Bloco 2** (cliente CIAM validado no gateway, `entra_oid` gravado no SQL) o lab tem um clímax fechado. Turmas que precisarem dividir o lab em dois encontros encerram aqui.

---

## 9. Glossário rápido

| Termo | Significado curtíssimo |
|---|---|
| **CIAM** | Customer Identity and Access Management — identidade de **cliente** (B2C). O produto Microsoft é o External ID. |
| **Microsoft Entra External ID** | O produto CIAM atual da Microsoft. Login em `<tenant>.ciamlogin.com`. Sucessor do Azure AD B2C. |
| **Microsoft Entra ID** (workforce) | Identidade de **funcionário** (B2B). Login em `login.microsoftonline.com`. Ex-"Azure AD". |
| **Microsoft Entra Connect** | Sync de AD on-premises → nuvem. **Não** é login de cliente. Citado só para desambiguar. |
| **Azure AD B2C** | CIAM **legado** (`b2clogin.com`), em depreciação. **Não usado** no lab. |
| **OIDC** | OpenID Connect — camada de **autenticação** sobre o OAuth 2.0 (prova quem você é; emite `oid`/`iss`/`aud`). |
| **PKCE** | Proof Key for Code Exchange — torna o Authorization Code Flow seguro **sem client secret** (essencial p/ SPA). |
| **MSAL** | Microsoft Authentication Library (`@azure/msal-browser`) — faz OIDC/PKCE por você no SPA. |
| **authority** | A URL do emissor que o MSAL/gateway usa para descobrir endpoints e chaves. CIAM: `<tenant>.ciamlogin.com`. |
| **user flow** | Fluxo de cadastro/login self-service configurado no External ID (pré-provisionado pelo instrutor). |
| **IdP social** | Identity Provider externo (ex.: Google) plugado no user flow do CIAM. |
| **OTP** | One-Time Passcode — código enviado por email; fallback de login quando não se usa Google. |
| **discovery / `.well-known`** | Documento `openid-configuration` que expõe issuer, endpoints e `jwks_uri` do emissor. |
| **claim `oid`** | Object ID — GUID **imutável** e estável do usuário **dentro de um tenant**. A chave de identidade do v2. |
| **claim `iss`** | Issuer — quem emitiu o token. CIAM termina em `/v2.0`. O gateway valida contra ele. |
| **claim `aud`** | Audience — para quem o token é destinado. Em v2.0 é o **client ID** da app. |
| **claim `roles`** | App Roles atribuídas ao usuário (no admin: contém `Admin`). |
| **App Registration (SPA)** | Registro da aplicação cliente no tenant (Authorization Code + PKCE, sem secret no browser). |
| **App Role** | Papel de autorização definido numa App Registration (no lab: uma única role `Admin`). |
| **issuer-agnóstico** | O gateway valida qualquer emissor por discovery — aceitar um novo issuer é config, não código. |
| **`X-Entra-OID`** | Header que o gateway injeta downstream com o `oid` validado (a Function lê e grava `entra_oid`). |
| **bcrypt** | Hash de senha do v1 (`bcryptjs`, 10 rounds, em `users.password`). **Não** exportável para o CIAM. |

---

## 10. Checklist antes de entrar na aula

- [ ] Sei diferenciar **Entra Connect** (sync AD), **Entra ID** (workforce, `microsoftonline.com`) e **Entra External ID** (CIAM, `ciamlogin.com`) — seção 2 ← conceito-chave
- [ ] Entendo por que o **cliente** entra pelo CIAM e o **admin** pelo workforce (desenho canônico B2C) — seção 1
- [ ] Sei o que é **OIDC + PKCE** e por que um SPA não pode guardar segredo — seção 3
- [ ] Sei que **Azure AD B2C é legado** e que usamos **só** o External ID (`ciamlogin.com`, não `b2clogin.com`) — seção 4
- [ ] Entendo que o gateway é **issuer-agnóstico** — só muda a **string da authority** para aceitar um novo emissor — seção 5
- [ ] Conheço a **diferença entre `purchases.entra_oid` (compra) e `users.entra_oid` (cadastro)** — seção 5.3
- [ ] Sei que a **senha bcrypt NÃO migra** e que isso é a lição (credencial nova no CIAM, bcrypt intacto) — seção 6
- [ ] Tenho minha **Function F1** e meu **gateway F2** funcionando (a identidade entra na frente deles) e login em portal.azure.com / entra.microsoft.com

Nos vemos na aula. Próximo artefato que você vai usar: [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md), nos 4 blocos de hands-on.
