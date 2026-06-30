# SPEAKER NOTES — Quartas de Final: Identidade dois-mundos + Migração v1→CIAM

> **Notas do facilitador** · 4 blocos de trabalho · sessão única estendida (~7,5–9,5h) · Workshop "Living Lab Azure-Native"
> **Use junto com:** [`README.md`](./README.md) (pré-leitura), [`slides.md`](./slides.md) (conceitos), [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Blocos 2/3/4), código em `src/Fifa2026.V2.Gateway/Program.cs` e `Lovable/.../authV2.ts`.
> **Story:** [2.11](../../stories/2.11.story.md) · **Decisões:** [ADE-007 v1.1](../../architecture/ade-007-identity-external-id.md) · [migração](../../architecture/migration-v1-ciam-design.md)

---

## Visão geral do dia (cole no flip chart)

| # | Bloco | Tempo | Modo | Marco do aluno |
|---|---|---|---|---|
| 0 | Conceitos: dois mundos, **slide de desambiguação** (Connect/Entra ID/External ID), OIDC/PKCE, B2C canônico | 40min | Expositivo + Q&A | Sabe diferenciar os 3 produtos "Entra" e por que cliente≠admin |
| 1 | **Gateway YARP policies:** confirmar/demonstrar rate-limit 429, cache 30s, CORS, JWT | 60–75min | Demo + hands-on | Revisão da F2; pipeline de borda no ar |
| 2 | **Cliente CIAM:** App Reg SPA + authority `ciamlogin.com` + sign-up/Google + `entra_oid` no SQL | 90–120min | Hands-on | Cliente CIAM real validado pelo gateway |
| ☕ | **PONTO DE PAUSA NATURAL** (turmas que dividem encerram aqui) | 15min | — | — |
| 3 | **Admin workforce + App Role `Admin`:** App Reg workforce + role + login separado | 75–90min | Hands-on | Dois mundos coexistindo; gateway aceita 2º emissor |
| 4 | **Migração v1→CIAM:** sign-up mesmo email + UPDATE + prova de coexistência | 60–90min | Hands-on | `COEXISTE (v1 bcrypt + v2 CIAM)` na mesma linha |
| 5 | Retro + Q&A: v1 vs v2, "o que a migração fez com o bcrypt?", carry-over F4 | 30min | Conversa |

**Mindset do facilitador:** a turma vem da F1 (fluxo assíncrono) e da F2 (gateway YARP com JWT placeholder anônimo). O ouro didático das Quartas é mostrar (1) que **B2C real tem dois mundos de identidade** — cliente no CIAM, funcionário no workforce — e (2) que o gateway valida **ambos com a mesma mecânica** (issuer-agnóstico: só muda a string da authority). O fio condutor emocional é a **migração**: modernizar **sem destruir** — bcrypt v1 e `oid` CIAM coexistindo na mesma linha.

**A frase âncora do dia:** "O cliente entra pelo External ID; o funcionário pelo workforce; o gateway valida os dois — só muda a string da authority." Repita.

**Pré-checagem (antes de começar):** confirme em voz alta "todo mundo tem a Function F1 e o gateway F2 no ar?" e "o instrutor já provisionou o tenant CIAM trial + user flow + Google IdP, e aplicou a `phase-04-ciam-link.sql`?" Sem o pré-provisionamento, o lab não cabe no tempo.

> **Nota de tempo:** este é um **lab longo** (sessão única estendida, decisão consciente do owner). Sinalize isso para a turma logo no início e administre os buffers. O **ponto de pausa ao fim do Bloco 2** é a quebra natural se for preciso dois encontros.

---

## BLOCO 0 — Conceitos (40min · slides + Q&A)

**Objetivo:** ao fim, o aluno diferencia **Entra Connect / Entra ID / Entra External ID**, entende o desenho canônico B2C (cliente≠admin), sabe o básico de OIDC/PKCE e sabe que o B2C legado **não** é usado.

### O SLIDE DE DESAMBIGUAÇÃO É OBRIGATÓRIO (AC-14)

> **Não pule este slide.** É a mitigação nº1 da carga cognitiva de dois mundos. Projete-o, leia em voz alta, e **volte a ele** sempre que a turma confundir os produtos (vai acontecer).

| Produto | É | Login | Usamos? |
|---|---|---|---|
| **Entra Connect** | Sync de AD on-prem → nuvem | *(não é login)* | ❌ só desambiguar |
| **Entra ID** (workforce) | Crachá de funcionário (B2B) | `login.microsoftonline.com` | ✅ admin (Bloco 3) |
| **Entra External ID** | Cadastro de cliente (CIAM/B2C) | `<tenant>.ciamlogin.com` | ✅ cliente (Bloco 2) |

**O roteiro de fala do slide:**
> "Três produtos, todos começam com 'Entra', dois têm login parecido. **Connect** é sync de Active Directory on-premises — esquece, não é login, só tô citando pra você não confundir. **Entra ID** é o crachá do funcionário — `login.microsoftonline.com`. **External ID** é o cadastro do cliente — `ciamlogin.com`. Decora: vê `ciamlogin`, pensa **cliente**; vê `microsoftonline`, pensa **funcionário**."

### Pontos a enfatizar
- **A analogia do estádio:** cliente compra na bilheteria pública (External ID); funcionário entra pela portaria de serviço com o crachá da empresa (workforce). Duas portas, dois públicos, dois produtos. Use o dia inteiro.
- **OIDC + PKCE:** um SPA **não pode guardar segredo** (qualquer um abre o DevTools). PKCE resolve: segredo temporário e descartável por login. **O MSAL faz isso por você** — você só configura authority + clientId.
- **External ID ≠ Azure AD B2C legado:** o B2C (`b2clogin.com`) está em depreciação. Usamos **só** o External ID (`ciamlogin.com`). Ensinar produto morto é antipedagógico.
- **Issuer-agnóstico (planta a semente do Bloco 2):** o gateway valida JWT por discovery. Aceitar um novo emissor é **config, não código**. "Vão ver: pra aceitar o cliente CIAM, a gente muda **uma string**."

### Perguntas pra turma (escolher 1-2)
- "Quem já integrou 'Login com Google' ou usou Auth0/Cognito/Firebase Auth? Vamos mapear os termos." (cria pontes — IdP social, user flow, claims).
- "Por que um SPA não pode ter um client secret?" (roda no browser; segredo é visível → motiva o PKCE).
- "Cliente e admin são 'usuário'. Por que tratá-los com produtos diferentes?" (públicos, escala, cadastro self-service vs crachá → motiva os dois mundos).

### Armadilhas (a evitar como instrutor)
- ⚠️ Não detalhe o protocolo OAuth/OIDC a fundo (token endpoint, grant types exóticos) — confunde. Foque na intuição "código → troca segura com PKCE".
- ⚠️ Não venda o External ID como "melhor que homegrown". É "o produto correto **para cliente**"; homegrown ainda é a comparação didática viva (v1).
- ⚠️ Não misture os tenants na cabeça da turma cedo demais — uma coisa de cada vez: Bloco 2 é só CIAM; o workforce admin só aparece no Bloco 3.

### Transição → Bloco 1
"Conceito na cabeça. Antes de mexer em identidade, vamos confirmar que o gateway da F2 — com rate-limit, cache e CORS — está no ar. A identidade entra **na frente** dele."

---

## BLOCO 1 — Gateway YARP policies (60–75min · demo + hands-on)

**Objetivo:** revisar/demonstrar que rate-limit (429), output cache (30s, `X-Cache`), CORS e o `AddJwtBearer` já estão ativos no gateway (story 2.2/2.3 Done). É a fundação de borda sobre a qual a identidade entra.

> Código: `src/Fifa2026.V2.Gateway/Program.cs`. As policies já existem — este bloco **confirma e demonstra**, não escreve do zero.

### Pontos a enfatizar
- **A ordem do pipeline é lei** (mostre no `Program.cs`): `UseCors → UseRateLimiter → XCacheMiddleware → UseOutputCache → UseAuthentication → UseAuthorization → MapReverseProxy`. (Mesma lição da F2.)
- **Rate limit (AC-2):** `AddRateLimiter` fixed window, 5/min por IP, 6ª → **429**. Demonstre o loop de 6 chamadas.
- **Output cache (AC-3):** `AddOutputCache` 30s no GET + header `X-Cache: HIT/MISS`.
- **CORS (AC-4):** `AddCors` restrito a `Gateway:FrontendOrigin`.
- **O `AddJwtBearer` já existe** (da F3): valida `iss`/`aud`/assinatura/expiração por discovery; **fail-closed** (sem `"common"`); anti-spoofing `X-Entra-OID`. **Nas Quartas, só muda a authority** (Bloco 2). Plante: "esse handler já valida workforce; vão ver que aceitar o CIAM é trocar uma string".

### Demonstração (rate limit — o "uau" da borda)
```bash
for i in $(seq 1 6); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST $GATEWAY/purchase \
    -H "Content-Type: application/json" \
    -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
done
# Esperado: 202 202 202 202 202 429
```

### Perguntas pra turma
- "Por que o rate limiter vem ANTES do proxy?" (barra abuso antes de gastar o backend).
- "O `AddJwtBearer` já está aqui desde a F3. O que vai mudar nas Quartas pra ele aceitar o cliente CIAM?" (a string da authority/discovery — planta o Bloco 2).

### Se faltar tempo (-15min)
- Demonstre só o 429 (o mais impactante); descreva cache/CORS verbalmente. As policies não são o foco novo das Quartas — a identidade é.

### Transição → Bloco 2
"Borda confirmada. Agora o tema central: o **cliente CIAM**. Vamos criar a App Reg no External ID, apontar a authority do MSAL pro `ciamlogin.com`, e ver o gateway validar o JWT que o CIAM emitiu."

---

## BLOCO 2 — Cliente Entra External ID / CIAM (90–120min · hands-on)

**Objetivo:** o aluno cria a App Reg SPA no CIAM, pluga `authority=ciamlogin.com` no MSAL, faz sign-up + login (Google/OTP) no browser, e vê `purchases.entra_oid` (origem CIAM) gravado em SQL — validado pelo gateway. **Este é o clímax técnico do lab.**

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Steps 2.1–2.4). Código: `authV2.ts` (authority CIAM), `Program.cs` (discovery CIAM).

### Pontos a enfatizar
- **A authority é a ÚNICA mudança de código de identidade** (issuer-agnóstico): `authV2.ts` `authority` de `login.microsoftonline.com/<tenant>` → **`<tenant>.ciamlogin.com`**. No gateway, o `AddJwtBearer` aponta o discovery pro CIAM. **"Só muda a string."**
- **O formato exato da authority CIAM** (AC-19): `https://<tenant-subdomain>.ciamlogin.com/`. Diga 3 vezes que é **`ciamlogin.com`, NÃO `microsoftonline.com`** — é o erro nº1 (`AADSTS50011`).
- **`knownAuthorities`:** authority non-AAD (`ciamlogin.com`) pode exigir `knownAuthorities` no msalConfig, senão o MSAL recusa a authority como não confiável. (Detalhe a validar com @dev — ver SPEAKER nota de validação no fim.)
- **O discovery do CIAM** (AC-19): `https://<tenant>.ciamlogin.com/<tenantId>/v2.0/.well-known/openid-configuration`. Dele o gateway pega `jwks_uri` (chaves RS256) e `issuer`. Tudo automático.
- **O encontro Gateway × Identidade:** o `AddJwtBearer` apontando o discovery CIAM é onde os dois temas se encontram. **"O gateway é o guardião: valida o JWT que o External ID emitiu antes de deixar passar."**
- **`oid → X-Entra-OID → entra_oid` NÃO muda** (issuer-agnóstico): o gateway extrai o `oid`, faz strip do header de entrada (anti-spoofing), injeta downstream; a Function grava `purchases.entra_oid` igual à F3. A coluna não sabe de qual tenant veio o GUID.
- **Dois eixos de `entra_oid`:** este bloco grava no eixo **compra** (`purchases.entra_oid`, transacional, já existia). O eixo **cadastro** (`users.entra_oid`) só aparece no Bloco 4. Não confunda a turma — só mencione que existe.

### Perguntas de checagem (faça EXPLICITAMENTE — AC-18)
- **"Qual é a URL de discovery do CIAM?"** → `https://<tenant>.ciamlogin.com/<tenantId>/v2.0/.well-known/openid-configuration`. (Se a turma não souber, volte ao slide.)
- **"Por que o gateway é issuer-agnóstico?"** → porque valida JWT por **discovery** (busca chaves/issuer do emissor automaticamente); aceitar um novo emissor é trocar a string da authority, não reescrever código.
- "Se eu apontar a authority do MSAL pro `microsoftonline.com` por engano, o que acontece?" → `AADSTS50011`; o cliente não loga. É o erro clássico.
- "Onde o `oid` do CIAM aterrissa neste bloco?" → em `purchases.entra_oid` (eixo compra, transacional).

### Demonstração (login CIAM ponta-a-ponta — o "uau" do lab)
1. SPA → "Entrar (v2)" → tela do `ciamlogin.com` → sign-up (Google ou email+OTP).
2. Compra → `Authorization: Bearer <token-CIAM>` → gateway valida → `X-Entra-OID` → Function → SQL.
3. Mostre o token em jwt.ms: `oid`, `iss` (termina em `ciamlogin.com/.../v2.0`), `aud` (= client ID). E o SQL com `purchases.entra_oid` preenchido.

### Armadilhas (acompanhar a turma)
- ⚠️ **Authority com `microsoftonline.com`** → `AADSTS50011`. Pegue no Step 2.2.
- ⚠️ **`Jwt__CiamTenantId` ausente** no gateway → 401 "Invalid issuer". Confirme o App Setting.
- ⚠️ **App Reg criada no tenant errado** (workforce em vez de CIAM) → login nunca funciona. Confirme `*.ciamlogin.com` no topo.
- ⚠️ **Google falha** → fallback email+OTP (sempre funciona, zero dependência).

### Transição → PAUSA
"Pronto: o cliente CIAM real, validado pelo gateway, com `entra_oid` no SQL. **Esse é o clímax técnico do dia.** Vamos respirar — e quem precisar dividir o lab em dois encontros, este é o ponto de corte."

---

## ☕ PONTO DE PAUSA NATURAL (15min)

> **Sinalização obrigatória (AC-18 / ADE-007):** ao fim do Bloco 2, o lab tem um **clímax fechado** — cliente CIAM validado no gateway, `entra_oid` gravado. Avise a turma: "se a gente dividir em dois encontros, **encerra aqui**. O que vem depois (admin + migração) é uma camada por cima de um cliente CIAM que já funciona."
>
> Se for sessão única: "Café. Voltamos pro **segundo mundo** de identidade — o admin no workforce — e depois a migração que prova a coexistência."

---

## BLOCO 3 — Admin workforce + App Role `Admin` (75–90min · hands-on)

**Objetivo:** o aluno constrói (hands-on) o login de admin no **workforce** com **uma** App Role `Admin`, e o gateway aceita o **segundo** emissor — provando que é issuer-agnóstico.

> **PROJETE O SLIDE DE DESAMBIGUAÇÃO DE NOVO** aqui (abertura do Bloco 3). A turma está saindo de `ciamlogin.com` (cliente) e entrando em `login.microsoftonline.com` (funcionário). É o momento de maior risco de confusão de tenants.

### Pontos a enfatizar
- **Dois mundos, dois tenants:** admin é **workforce** (`*.onmicrosoft.com`, `login.microsoftonline.com`); cliente é **CIAM** (`*.ciamlogin.com`). Tenants diferentes, App Regs diferentes.
- **Uma única App Role `Admin`** (decisão do owner): `displayName=Admin`, `value=Admin`, member type Users/Groups. Simplicidade didática — só uma role.
- **O claim `roles`** (AC-19): array de strings com os App Roles atribuídos ao usuário. O admin terá `roles: ["Admin"]`. Atribuir a role via **Enterprise applications → Users and groups** (não basta criar; tem que **atribuir**).
- **Gateway aceita o 2º emissor:** segundo `AddJwtBearer("Admin")` apontando o discovery do workforce. **Mesma mecânica do CIAM, só muda a authority.** Esta é a prova viva do issuer-agnóstico (ADE-004).
- **Fail-closed também aqui:** App Reg admin single-tenant (nunca `common`/multi-tenant), herdado da F3.

### Perguntas de checagem (AC-18)
- "Em que tenant criamos a App Reg admin?" → **workforce** (`login.microsoftonline.com`), não CIAM.
- "Qual claim o gateway lê para saber que o usuário é admin?" → `roles` (contém `Admin`).
- "Por que o gateway aceita o token do workforce **e** o do CIAM sem reescrever código?" → issuer-agnóstico: dois `AddJwtBearer`, cada um com sua authority; validação por discovery.

### Armadilhas
- ⚠️ **App Reg admin criada no CIAM** por engano → confunde os mundos. Admin é workforce.
- ⚠️ **Role criada mas não atribuída** → `roles` não aparece no token. Atribuir via Enterprise applications.
- ⚠️ **401 no admin login** → gateway sem `Jwt__AdminTenantId`/2º handler. Confirme os dois `AddJwtBearer`.

### Transição → Bloco 4
"Dois mundos no ar: cliente CIAM e admin workforce, ambos validados pelo gateway. Agora o ápice didático — vamos **migrar** um usuário v1 pro CIAM e provar que os dois paradigmas coexistem na mesma linha do banco."

---

## BLOCO 4 — Migração `users` v1 → CIAM (60–90min · hands-on)

**Objetivo:** migração aditiva v1→CIAM (sign-up mesmo email + UPDATE idempotente) e prova de coexistência na mesma linha de `users`. **O ápice emocional do lab.**

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Steps 4.1–4.5). Design: [migration-v1-ciam-design.md](../../architecture/migration-v1-ciam-design.md).

### Pontos a enfatizar
- **A migração é ADITIVA, não destrutiva:** vincula, não apaga. bcrypt v1 + `oid` CIAM coexistem.
- **A SPEAKER NOTE central — "a senha bcrypt não viaja":**
  > "A senha bcrypt do v1 **não** vai para o CIAM — e isso é de propósito. O External ID não importa hash bcrypt. No mundo gerenciado, a Microsoft cuida da credencial; você só guarda o `oid`. O bcrypt continua aqui ao lado, intacto, para você comparar. Veja: com identidade gerenciada, você nem tem mais um hash para guardar."
- **Mecanismo Opção C:** sign-up self-service no CIAM (reusa o Bloco 2) com o **mesmo email** do v1 + `UPDATE users SET entra_oid WHERE email AND entra_oid IS NULL`. Idempotente por construção.
- **`users.entra_oid` vs `purchases.entra_oid`:** este é o eixo **cadastro** (durável, único por usuário, índice UNIQUE filtrado). Diferente do eixo **compra** do Bloco 2 (transacional, NÃO-unique). A migração de **cadastro** precisa de um vínculo que sobreviva mesmo sem compra v2.
- **A query da prova é o clímax:** `status_migracao = COEXISTE (v1 bcrypt + v2 CIAM)`. Uma linha, duas identidades.

### Perguntas de checagem (AC-18)
- **"O que a migração faz com o bcrypt?"** → **nada** — deixa intacto. O bcrypt não migra (External ID não aceita); o usuário cria credencial nova no CIAM. bcrypt v1 e `oid` CIAM coexistem. **Esta é a lição.**
- "Por que o UPDATE tem `WHERE entra_oid IS NULL`?" → idempotência: rodar 2x não duplica nem reescreve.
- "Por que o vínculo de cadastro vai em `users.entra_oid` e não em `purchases.entra_oid`?" → porque `purchases` é transacional (só preenche com compra v2); um usuário que migra mas não compra precisa de vínculo durável em `users`.

### Demonstração (a coexistência — o "uau" final)
Rode a query do Step 4.5 e mostre a linha: `bcrypt-presente` + `<guid> CIAM` + `COEXISTE`. "Mesmo humano. Duas identidades. Dois paradigmas — homegrown e gerenciado — vivos lado a lado. Modernizar não exige destruir."

### Armadilhas
- ⚠️ **`status_migracao = 'so v1'`** → UPDATE não rodou ou email divergente. Conferir alvos (Step 4.1).
- ⚠️ **Sign-up com email diferente do v1** → o UPDATE por email não casa. Insista no **mesmo** email.
- ⚠️ **Trial CIAM expirado** entre turmas (30 dias) → recriar via VS Code Extension.

### Transição → Bloco 5
"Coexistência provada. Vamos fechar conversando sobre o que vocês acabaram de viver — homegrown vs gerenciado — e o que isso planta para a F4."

---

## BLOCO 5 — Retro & Q&A (30min · conversa)

### Roteiro da conversa
1. **A comparação v1 vs v2** (retome a tabela do README): bcrypt homegrown (você gerencia hash/reset/MFA) vs CIAM gerenciado (Microsoft cuida). **Frase de fechamento:** "Vocês não trocaram um pelo outro — vocês fizeram os dois **coexistirem**. Isso é modernização real: incremental, sem big-bang."
2. **Revisitar o DoD** (todos marcados? ver PORTAL-GUIDE Validação final).
3. **A prova issuer-agnóstica:** o gateway validou **dois** emissores (CIAM + workforce) sem reescrever código — só authority. "Esse é o poder de validar por discovery."

### Perguntas pra turma (reflexão)
- "Em que cenário real você usaria CIAM em vez de homegrown? E quando o homegrown ainda faz sentido?" (escala/social/MFA gerenciada vs controle total/simplicidade).
- "O que foi mais surpreendente: que a authority é a única mudança, ou que o bcrypt não migra?" (ambos são lições centrais).
- "Como a identidade que vocês construíram hoje vai ser usada na F4 (orquestração)?" (o `oid`/`X-Entra-OID` propagado leste-oeste para n8n/McpServer — ADE-006).

### Armadilhas (de fechamento)
- ⚠️ Não deixe ninguém sair achando "homegrown é ruim". É a comparação didática viva; em muitos casos simples ainda serve.
- ⚠️ Reforce que **B2C legado** não foi usado — e por quê (depreciado). Para não saírem buscando o produto errado depois.

### Carry-over para F4 (plante a curiosidade)
"Hoje vocês plantaram a identidade na borda: o gateway valida o JWT (cliente CIAM ou admin workforce) e propaga o `oid` como `X-Entra-OID`. Na **F4**, esse `oid` vai andar **leste-oeste** — para o n8n e o McpServer da orquestração. A identidade que nasceu aqui na borda vai dizer 'quem' disparou cada passo do fluxo interno."

---

## Apêndice — Mapa de troubleshooting (consulta rápida em sala)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| **`AADSTS50011`** no login do cliente | authority MSAL com `microsoftonline.com` em vez de `ciamlogin.com` | Authority = `<tenant>.ciamlogin.com` (erro nº1 das Quartas) |
| MSAL recusa authority "não confiável" | authority non-AAD sem `knownAuthorities` | `knownAuthorities: ['<tenant>.ciamlogin.com']` no msalConfig |
| **401 "Invalid issuer"** (cliente) no gateway | `Jwt__CiamTenantId` errado / `ValidIssuer` aponta workforce | Conferir App Setting CIAM + authority `ciamlogin.com` |
| **401 no admin login** | gateway sem 2º handler / `Jwt__AdminTenantId` | Confirmar os dois `AddJwtBearer` (CIAM + Admin) |
| `roles` ausente no token admin | role não atribuída ao usuário | Enterprise applications → Users and groups → atribuir `Admin` |
| `oid` ausente no token CIAM | scope/user flow | `oid` é claim padrão; fallback URI `objectidentifier` |
| Token CIAM expirado → 401 | `ClockSkew=Zero` (sem tolerância) | Comportamento esperado/desejado; renovar token (acquireTokenSilent) |
| Usuário não migra / `so v1` | UPDATE não rodou / email divergente | Re-executar UPDATE idempotente; mesmo email do v1 |
| Google IdP falha | redirect URI do Google não inclui callback CIAM | Instrutor pré-configura; **fallback email+OTP** |
| Trial CIAM expirado entre turmas | 30 dias encerraram | Recriar via VS Code Extension / tenant com subscription (free 50K MAU) |

---

## Lembretes finais para o facilitador
- **O slide de desambiguação** (Connect/Entra ID/External ID) é obrigatório no Bloco 0 e **repetido** no Bloco 3. Volte a ele sempre que a turma confundir os produtos.
- **"Só muda a string da authority"** é o fio condutor técnico — repita no Bloco 2 (cliente CIAM) e no Bloco 3 (admin workforce).
- **O ponto de pausa é ao fim do Bloco 2** — sinalize-o claramente; é a quebra natural se a turma dividir o lab.
- **"A senha bcrypt não viaja — e isso é a lição"** é a speaker note do Bloco 4. Não trate como limitação; é o coração da comparação homegrown vs gerenciado.
- **`ciamlogin.com` ≠ `microsoftonline.com` ≠ `b2clogin.com`** — três domínios, três significados. O `b2clogin.com` (legado) **nunca** aparece no lab.
- Tom: prático, honesto sobre trade-offs, sem hype. A turma é técnica e respeita transparência — igual à F1/F2.
