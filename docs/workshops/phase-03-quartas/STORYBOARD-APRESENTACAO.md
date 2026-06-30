# Storyboard da Apresentação — Quartas de Final (F2)

> **Para o Claude for PowerPoint:** gere/ajuste a apresentação **no mesmo layout do template aberto** (o das Oitavas). **11 slides, máximo 12.** Mantenha o estilo visual: capa cheia, slide de "Stack da fase", slides de tecnologia no formato *O que é? · Como funciona (com diagrama) · Principais recursos (4 itens) · ▸ Nesta etapa*, slides de conceito-chave, arquitetura e encerramento.
> **Rodapé fixo em todos os slides:** `COPA DO MUNDO AZURE 2026` · `F2 · IDENTIDADE DOIS MUNDOS`
> **Paleta/tipografia:** as mesmas do template das Oitavas. Cor de acento sugerida para "dois mundos": **verde-azulado** (cliente/CIAM) + **azul** (admin/workforce).
> **Não** incluir blocos de código longos (isso fica no runbook); o foco é **explicar as tecnologias e os conceitos-chave**.

---

## Slide 1 — CAPA
- **Etiqueta:** QUARTAS DE FINAL · COPA DO MUNDO AZURE
- **Título:** Identidade em **Dois Mundos**
- **Subtítulo:** Gateway YARP + Microsoft Entra External ID (CIAM) + admin workforce
- **Linha de apoio:** O gateway vira o **guardião**: valida o login do cliente e do admin — e migra os usuários antigos sem apagar nada.

---

## Slide 2 — STACK DA FASE · "As tecnologias que vamos usar"
Lista (1 linha cada, com ícone):
- **Gateway YARP (.NET 8)** — proxy reverso que protege a borda: rate-limit, cache, CORS e validação de token.
- **Microsoft Entra External ID (CIAM)** — identidade do **cliente**: cadastro self-service com Google ou e-mail/OTP.
- **Microsoft Entra ID (workforce)** — identidade do **admin**: conta corporativa com App Role `Admin`.
- **Azure Container Apps** — onde o gateway roda (imagem no ACR + Container App público).
- **Azure SQL** — migração **aditiva** (`users.entra_oid`): bcrypt antigo e identidade nova coexistindo.

---

## Slide 3 — CONCEITO-CHAVE · "Dois mundos de identidade" (slide de desambiguação)
- **Título:** Dois públicos, dois produtos, duas URLs de login
- **Tabela:**
  | Produto | É | Login | Usamos? |
  |---|---|---|---|
  | **Entra Connect** | sync de AD on-prem → nuvem | *(não é login)* | ❌ só desambiguar |
  | **Entra ID** (workforce) | crachá do funcionário (B2B) | `login.microsoftonline.com` | ✅ admin |
  | **Entra External ID** | cadastro do cliente (CIAM/B2C) | `<tenant>.ciamlogin.com` | ✅ cliente |
- **Analogia (destaque):** o cliente compra na **bilheteria pública** (External ID); o funcionário entra pela **portaria de serviço** com o **crachá** (workforce).
- **Aviso:** `b2clogin.com` = Azure AD B2C **legado** → **não usamos**.

---

## Slide 4 — TECNOLOGIA 1 DE 4 · Gateway YARP
- **O que é?** Um **reverse proxy** em ASP.NET Core (.NET 8) que fica **na frente** das Functions. O cliente fala só com ele; ele aplica as regras de borda e encaminha.
- **Como funciona (diagrama):** `Browser` → **`Gateway YARP`** `[CORS · Rate-limit · Cache · JWT dual-issuer]` → `Function F1` (downstream com header `X-Entra-OID`).
- **Principais recursos (4):**
  - **Reverse proxy** — roteia `/purchase` para a Function; a URL real nunca aparece pro cliente.
  - **Rate limiting** — janela fixa 5/min; a 6ª chamada vira **HTTP 429**.
  - **Output cache** — 30s nos GET; 2ª resposta com `X-Cache: HIT` (<50ms).
  - **Validação JWT dual-issuer** — valida CIAM **e** workforce por *discovery*; extrai o `oid` e injeta `X-Entra-OID`.
- **▸ Nesta etapa:** subir o gateway no Container App e ver as políticas atuando (429, cache, e o 401 sem token).

---

## Slide 5 — TECNOLOGIA 2 DE 4 · Microsoft Entra External ID (CIAM)
- **O que é?** O serviço **CIAM** da Microsoft (sucessor do Azure AD B2C) para a identidade do **cliente final**: ele faz **cadastro self-service**.
- **Como funciona (diagrama):** `SPA` → **`<tenant>.ciamlogin.com`** (login Google **ou** e-mail/OTP) → `Authorization Code + PKCE` → `token JWT (claim oid)` → `Gateway`.
- **Principais recursos (4):**
  - **Cadastro self-service** — o cliente cria a própria conta (via *user flow*).
  - **Login social + OTP** — Google plugado como IdP; e-mail + código (OTP) como fallback.
  - **OIDC + PKCE** — o SPA loga **sem guardar segredo** (o MSAL faz o PKCE por baixo).
  - **Claim `oid`** — GUID estável do usuário no tenant; é a **chave de identidade** do v2.
- **▸ Nesta etapa:** criar o tenant CIAM + App Registration SPA, apontar a authority pro `ciamlogin.com` e logar no browser.

---

## Slide 6 — TECNOLOGIA 3 DE 4 · Microsoft Entra ID (workforce / admin)
- **O que é?** A identidade **corporativa** (B2B) — o "crachá do funcionário". É por aqui que o **admin** entra, separado do cliente.
- **Como funciona (diagrama):** `Admin` → **`login.microsoftonline.com`** → `token com roles:["Admin"]` → `Gateway` (policy **`AdminOnly`**).
- **Principais recursos (4):**
  - **App Registration workforce** — single-tenant (fail-closed, nunca `common`).
  - **App Role `Admin`** — uma única role; aparece no claim `roles`.
  - **`AdminOnly` → 403** — um cliente CIAM **válido** numa rota admin recebe **403** (autenticado, mas sem a role) — não 401.
  - **Mesmo gateway, 2º emissor** — aceitar o workforce é **configuração**, não código novo.
- **▸ Nesta etapa:** criar a App Registration admin + App Role `Admin` e logar separado do cliente.

---

## Slide 7 — TECNOLOGIA 4 DE 4 · Azure Container Apps
- **O que é?** Hospedagem **serverless de containers**. É onde a imagem do gateway roda e ganha uma URL pública.
- **Como funciona (diagrama):** `Dockerfile` → `az acr build` → **`Azure Container Registry`** → **`Container App`** (porta 8080, ingress público).
- **Principais recursos (4):**
  - **ACR** — o "depósito" da imagem Docker do gateway.
  - **Container App** — roda a imagem, escala e expõe a URL pública.
  - **App Settings** — `Jwt__CiamTenantId`, `Jwt__AdminTenantId`… (configuração, nada hardcoded).
  - **Fail-closed** — sem o tenant configurado, o gateway **não sobe** (segurança por padrão).
- **▸ Nesta etapa:** publicar a imagem no ACR e criar o Container App do gateway.

---

## Slide 8 — CONCEITO-CHAVE · Gateway issuer-agnóstico
- **Título:** "Só muda a string da authority"
- **Ideia central:** o gateway valida **qualquer** emissor por **discovery** (busca a chave pública do emissor numa URL `.well-known`). Aceitar um novo mundo de identidade é **configuração**, não reescrita de código.
- **Visual (lado a lado):**
  - `AddJwtBearer("Ciam")` → discovery `ciamlogin.com` → valida o token do **cliente**
  - `AddJwtBearer("Admin")` → discovery `login.microsoftonline.com` → valida o token do **admin**
  - `PolicyScheme "Selector"` → roteia pelo **issuer** do token (cliente vs admin)
- **Frase-âncora:** *"Cliente e admin são emissores diferentes — validados pela mesma mecânica."*

---

## Slide 9 — CONCEITO-CHAVE · Migração aditiva v1 → CIAM
- **Título:** Modernizar **sem destruir**
- **Ideia central:** a migração **vincula**, não apaga. O usuário antigo ganha um `oid` do CIAM **ao lado** da senha bcrypt — na mesma linha de `users`.
- **Visual (mesma linha de `users`):** `password` = `bcrypt-presente` ✅ · `entra_oid` = `<guid CIAM>` ✅ → **`COEXISTE (v1 bcrypt + v2 CIAM)`**
- **A lição (destaque):** *"A senha bcrypt **não viaja** pro CIAM — e isso é de propósito. No mundo gerenciado, a Microsoft cuida da credencial; você só guarda o `oid`."*
- **▸ Nesta etapa:** sign-up no CIAM com o mesmo e-mail + `UPDATE users.entra_oid` (idempotente) + provar a coexistência em SQL.

---

## Slide 10 — ARQUITETURA · Identidade dois mundos
- **Título:** A foto completa da F2
- **Diagrama (reusar o `quartas-f2-identidade-dois-mundos.drawio`):**
  - **Mundo 1 (cliente):** Entra External ID (`ciamlogin.com`) → token
  - **Mundo 2 (admin):** Entra ID workforce (`login.microsoftonline.com`) → token
  - Ambos → **Browser SPA** → ① `POST /purchase` (Bearer) → **Gateway YARP** (CORS · rate-limit · cache · **dual-issuer JWT · Selector** · `X-Entra-OID`) → ② proxy → **Function F1 (inalterada)** → ③ `purchases.entra_oid`
  - **SQL — dois eixos:** `purchases.entra_oid` (compra, zero-DDL) · `users.entra_oid` (cadastro, migration aditiva)
- **Legenda:** *o pipeline `oid → X-Entra-OID → entra_oid` é idêntico ao da F1 — o gateway só passou a validar dois mundos.*

---

## Slide 11 — ENCERRAMENTO DA FASE
- **Título:** Você concluiu as Quartas de Final
- **Bullets (o que construiu):**
  - **Gateway YARP** no ar, com rate-limit, cache, CORS e validação de token.
  - **Cliente CIAM** real (cadastro self-service + Google/OTP) validado pelo gateway.
  - **Admin workforce** com App Role `Admin`, separado do cliente — dois mundos coexistindo.
  - **Migração aditiva** provando bcrypt v1 + `oid` CIAM na mesma linha (`COEXISTE`).
- **PRÓXIMA FASE:** **Final** — orquestração (n8n), chatbot com IA (MCP + Gemini) e a jornada agêntica visível ao vivo (Flow Visualizer).

---

### Notas de geração (para o Claude for PowerPoint)
- Marcar nos slides 4–7 o selo **"TECNOLOGIA X DE 4"** (canto), como o template das Oitavas faz com "TECNOLOGIA X DE 3".
- Diagramas: caixas + setas simples, no estilo "COMO FUNCIONA" das Oitavas (não usar screenshots).
- Os slides 3, 8 e 9 são os **conceitos-chave** — dar destaque visual (cor de acento, frase grande).
- Speaker notes detalhadas (a teoria de cada bloco) estão em `SPEAKER-NOTES.md` — não precisam ir nos slides.
</content>
