# MEMO de Recomendação — Identidade do Cliente Final (B2C) e Quartas de Final (F2: Gateway YARP + Identidade)

> **Autor:** Atlas (@analyst)
> **Data:** 2026-06-25
> **Para:** Owner (Guilherme Prux Campos) + @architect (formalizar nova ADE) + @pm (atualizar blueprint/epic)
> **Projeto:** FIFA 2026 Tickets — Workshop "Copa do Mundo Azure"
> **Escopo:** Validar a identidade do cliente final (consumer-facing/B2C) e projetar as Quartas de Final (F2 = Gateway + Identidade)
> **Status:** Recomendação para decisão do owner — propõe **nova ADE que supersede a ADE-005**
> **Confiança geral:** ALTA nos fatos de produto (citados Microsoft Learn); MÉDIA-ALTA na estimativa de tempo de aula (depende de pré-provisionamento)

---

## 0. TL;DR — Recomendação em 5 bullets

1. **O caminho atual (workforce + App Registration + MSAL) está semanticamente ERRADO para o cliente final.** O app de ingressos é **B2C consumer-facing**; o tenant workforce (`login.microsoftonline.com`) modela **funcionários/B2B**. A própria ADE-005 admitiu o trade-off ("workforce ≠ CIAM; o real B2C é o Entra External ID"). O produto correto para clientes é o **Microsoft Entra External ID (CIAM)**.

2. **Reabrir e ADOTAR o Entra External ID nas Quartas é VIÁVEL — a premissa de atrito da ADE-005 mudou.** A ADE-005 (2026-06-03) adiou o CIAM porque "exige tenant CIAM separado + user flows". Hoje a Microsoft oferece **trial tenant External ID SEM subscription e SEM cartão (30 dias, até 10K objetos)** + **extensão VS Code** + **"get started guide"** que cria tenant + user flow + app de exemplo "em poucos minutos". O atrito que justificou o adiamento caiu materialmente.

3. **O encadeamento Gateway → Function → SQL NÃO muda** — é issuer-agnóstico. Só mudam **authority/issuer** (`<tenant>.ciamlogin.com` em vez de `login.microsoftonline.com`) e o **`aud`** que o YARP valida. `oid → X-Entra-OID → entra_oid` permanece idêntico. **Risco técnico de migração: BAIXO.**

4. **No banco: `entra_oid` (do CIAM) COEXISTE com `users` v1 (bcrypt), não substitui.** O valor pedagógico do workshop é a **comparação lado-a-lado v1 (homegrown) vs v2 (CIAM gerenciado)** na mesma tabela `purchases`. Coluna `entra_oid` aditiva e idempotente (ADE-000 Invariante 2) — mantém os dois caminhos vivos. Substituir destruiria o contraste didático.

5. **Estrutura das Quartas (F2):** Gateway YARP (proxy + rate-limit + cache + CORS + validação JWT) como **plano de dados**, e External ID (tenant CIAM + 1 user flow sign-up/sign-in + 1 social IdP + App Reg SPA) como **plano de identidade**, com o YARP validando o JWT emitido pelo CIAM. **Mitigação de tempo:** instrutor PRÉ-PROVISIONA o trial tenant + user flow; aluno só cria a App Reg e pluga o `authority` no MSAL. Cabe em 6h.

---

## 1. Esclarecimento de terminologia (vai para o material da aula)

Existe confusão recorrente entre três produtos "Entra" que resolvem problemas **diferentes**. O material da aula precisa desambiguar isso explicitamente, porque o aluno vê "Entra" em todos e assume que são intercambiáveis — não são.

| Produto | O que é | Direção de identidade | Quando usar | Authority / domínio |
|---|---|---|---|---|
| **Microsoft Entra Connect** | **Ferramenta de sincronização** (agente instalado on-prem). Sincroniza usuários/grupos do **Active Directory on-premises** para o Entra ID na nuvem (cenário **híbrido**). Não é um provedor de identidade — é uma **ponte**. | AD on-prem → nuvem (sync) | Empresa tem AD local e quer estender essas identidades para a nuvem (híbrido). **Irrelevante para este workshop** (não há AD on-prem). | n/a (agente de sync) |
| **Microsoft Entra ID** (ex-Azure AD) | IdP de **força de trabalho** (workforce): funcionários, contratados, parceiros de negócio (B2B). É o tenant que vem com a subscription Azure. | Funcionário/B2B → apps corporativos | Login de **funcionário** (admin, operador, back-office). Apps internos. | `login.microsoftonline.com/<tenant>` |
| **Microsoft Entra External ID** (ex-Azure AD B2C, CIAM) | IdP de **clientes** (CIAM = Customer Identity & Access Management). Self-service sign-up, social login, branding, coleta de atributos. Tenant **separado** do workforce. | **Cliente/consumidor (B2C)** → app público | Login do **cliente final** de um app público (e-commerce, ingressos, SaaS). **É o produto correto para a loja de ingressos.** | `<tenant>.ciamlogin.com` |

**Regra mnemônica para a aula:**
- **Connect** = *ponte* (sincroniza AD local → nuvem).
- **Entra ID** = *crachá de funcionário* (quem trabalha na empresa).
- **External ID** = *cadastro de cliente* (quem compra de você).

**Aplicação ao app de ingressos:** o **comprador** (cliente final) → **External ID**. O **admin/operador** que gerencia o evento no back-office → **Entra ID workforce** (App Roles). Os dois coexistem: cliente entra por `ciamlogin.com`, funcionário por `login.microsoftonline.com`. Este é, aliás, o desenho **canônico** de produto B2C — e é exatamente o que torna o lab fiel à realidade.

> Fonte: [Microsoft Entra External ID Overview](https://learn.microsoft.com/en-us/entra/external-id/external-identities-overview), [External Tenant Overview (CIAM)](https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam)

---

## 2. Entra External ID (CIAM) — validação para B2C

Fatos confirmados via Microsoft Learn (2026-06-25):

### 2.1 Modelo de tenant
- O External ID usa um **tenant "external" (CIAM) separado** do tenant workforce. Cria-se em **Entra admin center → Manage tenants → Create → External**.
- **Trial tenant sem subscription:** ao criar pela primeira vez, há a opção de um **trial tenant que NÃO exige subscription Azure** (30 dias, suporta até **10.000 objetos**) — suficiente de sobra para um lab. *(Este é o achado que muda o cálculo da ADE-005.)*
- Fonte: [Create an External Tenant](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-create-external-tenant-portal), [Quickstart trial setup](https://learn.microsoft.com/en-us/entra/external-id/customers/quickstart-trial-setup)

### 2.2 User flows
- **User flow** = a jornada de **sign-up/sign-in self-service**: define os passos de cadastro, os métodos de login (email+senha, OTP por email, contas sociais) e os **atributos coletados** do usuário. Configurável com **branding** (logo, cores).
- Criado em **External Identities → User flows → New user flow**; depois associa-se a aplicação ao user flow.
- Fonte: [Create a User Flow (customers)](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-user-flow-sign-up-sign-in-customers), [Self-service sign-up overview](https://learn.microsoft.com/en-us/entra/external-id/self-service-sign-up-overview)

### 2.3 Social IdPs
- Suporta **Google, Facebook e Apple** como provedores sociais no user flow (Apple saiu de preview recentemente). Configura-se criando um app no provedor (client id + secret) e plugando no tenant External ID.
- Fonte: [Identity providers for external tenants](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers), [Apple IdP support](https://devblogs.microsoft.com/identity/openid-connect-social-identity-provider-apple/)

### 2.4 Authority / issuer
- Apps em tenants External ID **sempre** usam authority no formato **`<tenant-name>.ciamlogin.com`** — diferente do workforce (`login.microsoftonline.com`). Esta é a **única mudança material** no código de identidade vs o caminho atual.
- Fonte: [Entra External ID SignUpSignIn authority URL](https://learn.microsoft.com/en-us/answers/questions/2285781/entra-external-id-signupsignin-authority-url), [Get started guide features](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-guide-explained)

### 2.5 Free tier e custo
- **Free tier: primeiros 50.000 MAU** (Monthly Active Users) no tier Basic, **sem custo**. *(Confirmado — a estimativa "50K MAU" do blueprint/ADE-005 está correta e atual.)*
- Acima de 50K MAU: cobrança por MAU (modelo Basic MAU). **Add-ons premium** (ex.: cenários avançados) têm cobrança própria e **não têm free tier**.
- Para um workshop, o uso fica **muito abaixo** dos 50K MAU → **custo US$0** (e o trial nem exige subscription).
- Fonte: [External ID Pricing (Microsoft Learn)](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing), [Microsoft Entra External ID Pricing (Azure)](https://azure.microsoft.com/en-us/pricing/details/microsoft-entra-external-id/)

### 2.6 Limites relevantes
- Trial tenant: **até 10K objetos**, **30 dias** de validade (depois precisa converter para subscription ou recriar). Para o lab, recriar a cada turma é trivial.
- O free tier de 50K MAU aplica-se ao tenant "de produção" (com subscription); o trial é para experimentação rápida sem cartão.

---

## 3. Comparação de alternativas para o workshop

Critérios: **atrito de setup** (tempo/cerimônia), **fidelidade ao "B2C real"** (semântica correta para cliente final), **custo**, **tempo de aula** (cabe em 6h?).

| # | Alternativa | Atrito de setup | Fidelidade B2C | Custo | Tempo de aula | Veredito |
|---|---|---|---|---|---|---|
| **(a)** | **Entra External ID / CIAM** ⭐ | MÉDIO → **BAIXO com mitigação** (trial sem subscription + extensão VSCode + get-started guide; user flow pré-provisionado pelo instrutor) | **ALTA** — é literalmente o produto B2C da Microsoft (ex-Azure AD B2C) | **US$0** (free 50K MAU; trial sem cartão) | **Cabe em 6h** se instrutor pré-provisiona tenant+user flow | **RECOMENDADO** |
| **(b)** | **Workforce tenant atual (ADE-005)** | BAIXO (tenant já existe na subscription) | **BAIXA** — workforce ≠ CIAM; modela funcionário, não cliente. Semanticamente errado para a loja | US$0 | Cabe | **Rejeitar como identidade do CLIENTE** (manter só para o ADMIN) |
| **(c)** | **App Service Easy Auth** | BAIXO (zero código de auth) | BAIXA-MÉDIA — usa o IdP por baixo (workforce ou CIAM); por si só não é "B2C". Acopla auth ao hosting (App Service) e ao header `X-MS-CLIENT-PRINCIPAL` | US$0 | Cabe | **Rejeitar como principal** — quebra "o gateway valida o JWT" (ADE-004); manter como nota/alternativa zero-código |
| **(d)** | **Azure AD B2C legado** | MÉDIO | ALTA (era o CIAM) mas **EM DEPRECAÇÃO** | US$0 mas em fim de vida comercial | Cabe | **Rejeitar** — fim de venda para novos clientes em **2025-05-01**; B2C P2 descontinuado **2026-03-15**; Microsoft direciona explicitamente para External ID. Ensinar produto morto é antipedagógico |
| **(e)** | **3rd-party (Auth0 / Cognito)** | MÉDIO-ALTO (conta externa, fora do Azure) | ALTA (são CIAM de verdade) | Free tier limitado, mas **fora do Azure** | Cabe, mas foge do tema | **Rejeitar** — o workshop é "Copa do Mundo **Azure**"; sair do Azure quebra a narrativa e adiciona vendor externo |

### Nota sobre (d) — Azure AD B2C legado (status de depreciação)
Confirmado: **efetivo 2025-05-01, Azure AD B2C deixou de ser vendido para novos clientes**; novos tenants só com B2C P1; **B2C P2 descontinuado em 2026-03-15** para todos; suporte aos existentes até pelo menos 2030, mas a Microsoft **move ativamente para o Entra External ID**. Conclusão: **não ensinar B2C legado** — External ID é o sucessor oficial.
> Fonte: [New Azure AD B2C customers after May 2025](https://learn.microsoft.com/en-us/answers/questions/2150272/new-azure-ad-b2c-customers-after-may-2025), [Microsoft to End Sale of Azure AD B2B/B2C on May 1, 2025](https://envisionit.com/resources/articles/microsoft-to-end-sale-of-azure-ad-b2bb2c-on-may-1-2025-shifting-to-entra-id-external-identities)

---

## 4. Encaixe no lab de 6h

**Pergunta:** dá para provisionar tenant CIAM + 1 user flow + App Reg no tempo da aula?

**Resposta: SIM, com pré-provisionamento pelo instrutor.** A premissa de atrito da ADE-005 era válida em 2026-06-03 para um fluxo onde o **aluno** criava tudo do zero. A mitigação correta é dividir o trabalho:

### O que o INSTRUTOR pré-provisiona (antes da aula, fora do relógio de 6h)
1. **Trial tenant External ID** (sem subscription, sem cartão — 30 dias). Recriável por turma.
2. **1 user flow** sign-up/sign-in (email+senha + 1 social IdP, ex. Google) com branding mínimo do evento.
3. (Opcional) **Script CLI / extensão VSCode** versionado no repo para recriar tenant+user flow de forma reproduzível entre turmas.

### O que o ALUNO faz na aula (dentro das 6h)
1. **App Registration tipo SPA** no tenant External ID (redirect URI localhost + URL prod). ~5 min no Portal.
2. Plugar `authority = <tenant>.ciamlogin.com` + `clientId` no **MSAL.js** do front (mesmíssimo código já esboçado; só muda a string de authority). ~5 min.
3. **Demonstrar o fluxo no browser:** sign-up self-service → login social → recebe access token.
4. Ver o **YARP validar o JWT** (issuer CIAM) e propagar `oid → X-Entra-OID`.
5. Ver o `entra_oid` gravado no SQL **ao lado** do registro v1.

### Mitigações do atrito apontado pela ADE-005
| Atrito ADE-005 | Mitigação 2026 |
|---|---|
| "Tenant CIAM separado, pré-criado e gerido pelo instrutor" | Hoje é **trial sem subscription/cartão** + **extensão VSCode** que cria em minutos. Instrutor pré-provisiona; aluno não vê esse custo. |
| "User flows consomem tempo de aula" | Instrutor pré-cria o user flow; aluno apenas **associa a App Reg** ao flow (1 passo) e demonstra. O get-started guide da Microsoft faz isso "em poucos minutos". |
| "Passos de Portal extensos" | **Roteiro de Portal enxuto** (só App Reg + plug no MSAL) documentado no PORTAL-GUIDE de F2. CLI/VSCode para o que é pré-aula. |

**Recomendação de abordagem:** **híbrida** — instrutor pré-provisiona a infraestrutura CIAM (tenant + user flow + social IdP) via script/VSCode versionado; aluno foca no que é pedagógico (App Reg SPA + MSAL authority + ver o token fluir e ser validado no YARP). Isto preserva o objetivo de aula sem o atrito que matou a ideia em junho.

> Fonte: [Quickstart trial setup (no subscription)](https://learn.microsoft.com/en-us/entra/external-id/customers/quickstart-trial-setup), [Fast Entra External ID setup with VSCode](https://www.azuredive.net/2024/07/fast-entra-external-id-setup-with-vscode/), [MSAL React sample (External ID)](https://learn.microsoft.com/en-us/samples/azure-samples/ms-identity-ciam-javascript-tutorial/ms-identity-ciam-javascript-tutorial-1-sign-in-react/)

---

## 5. Loja do cliente — `oid` do CIAM substitui ou coexiste com `users` v1?

**Recomendação: COEXISTÊNCIA (migração aditiva), NÃO substituição.**

### Por quê coexistir
1. **O contraste v1 vs v2 É o produto pedagógico.** O workshop ensina a diferença entre **identidade homegrown** (bcrypt + JWT local na tabela `users`, com toda a dívida: você gerencia hash, reset de senha, MFA, brute-force) e **identidade gerenciada CIAM** (Microsoft cuida de tudo). Apagar o v1 apaga a lição.
2. **Aditividade preserva ADE-000 Invariante 2** (schema delta idempotente). A coluna `entra_oid` (já existente, criada na F3 atual) **não muda** — só passa a ser preenchida com o `oid` do **CIAM** em vez do workforce. Issuer-agnóstico: a coluna não sabe nem se importa de qual tenant veio o GUID.
3. **Dados existentes (`users` + compras ligadas) permanecem íntegros.** O app já tem `users` int + `purchases` ligadas a ele. O v2/CIAM grava `entra_oid` ao lado, na mesma `purchases` (ADE-000 Invariante 1, comparação lado-a-lado). Zero migração destrutiva, zero risco aos dados v1.

### Como fica o modelo
- **v1 (homegrown):** `users.id` (int) + bcrypt; `purchases.user_id` → `users.id`. **Intacto.**
- **v2 (CIAM):** `purchases.entra_oid` (UNIQUEIDENTIFIER, já existe) recebe o `oid` do **External ID**. Mesma coluna da F3 atual — só muda a origem do GUID (CIAM em vez de workforce).
- **Sem tabela de mapping** (ADE-001 já foi aposentada pela ADE-005; `oid` continua sendo a chave direta — isso não muda com CIAM).

### Caminho de evolução (pós-workshop, fora de escopo do lab)
Em produção real, a estratégia seria **migrar usuários `users` v1 → External ID** (import via Graph + link `entra_oid`) e **descontinuar o bcrypt**. Vale como **speaker note de "próximos passos"**, não como passo do lab. O lab mostra os dois vivos; produção consolidaria no CIAM.

---

## 6. Estrutura das Quartas (F2 = Gateway YARP + Identidade External ID)

**Tese de F2:** o **Gateway YARP** é o *plano de dados* (todo tráfego passa por ele) e o **External ID** é o *plano de identidade* (quem é o cliente). A síntese pedagógica é: **"o gateway é o guardião — ele valida o JWT que o CIAM emitiu antes de deixar passar"**. Os dois temas se encontram exatamente no `AddJwtBearer` do YARP apontando para o discovery do CIAM.

### Roteiro do lab (proposta)

| Bloco | Tema | Atividade | Recurso NOVO em destaque |
|---|---|---|---|
| **0. Setup (pré-aula, instrutor)** | Infra CIAM | Trial tenant + user flow + social IdP (script/VSCode versionado) | (bastidor — não consome relógio) |
| **1. Gateway como plano de dados** | YARP proxy | Subir o YARP na frente das Functions; rotear v1/v2 | Reverse proxy, route/cluster YARP |
| **2. Resiliência no edge** | Rate-limit + cache + CORS | Configurar rate-limit, response cache e CORS no gateway | Políticas de edge centralizadas (1 lugar, não N serviços) |
| **3. Identidade do cliente (CIAM)** | External ID | Aluno cria App Reg SPA; pluga `authority=ciamlogin.com` no MSAL; demonstra sign-up self-service + login social no browser | **Entra External ID, user flow, social IdP, `ciamlogin.com`** |
| **4. O encontro: gateway valida o JWT** | YARP + JWT | `AddJwtBearer` no YARP apontando para discovery do **issuer CIAM**; valida `iss`/`aud`/assinatura; extrai `oid` | **Validação de JWT de CIAM no edge** (issuer-agnóstico vs F3 workforce) |
| **5. Propagação e persistência** | `oid → entra_oid` | YARP propaga `X-Entra-OID`; Function grava `entra_oid` ao lado do v1 | Identidade de cliente persistida lado-a-lado com a homegrown |
| **6. Comparação final** | v1 vs v2 | Tabela: bcrypt homegrown vs CIAM gerenciado (quem cuida de quê) | Contraste explícito de TCO/risco de identidade |

### Features / recursos NOVOS a destacar (para o material)
- **Microsoft Entra External ID** (produto CIAM — o sucessor oficial do Azure AD B2C).
- **Tenant External ID** (separado do workforce) e **trial sem subscription**.
- **User flow** self-service (sign-up/sign-in, atributos, branding).
- **Social IdP** (Google) num app B2C real.
- **Authority `ciamlogin.com`** (vs `login.microsoftonline.com` — a única mudança de string vs F3).
- **YARP `AddJwtBearer` validando token de CIAM** — prova de que o gateway é issuer-agnóstico.
- **Rate-limit / cache / CORS centralizados** no gateway (plano de dados).

### O que vira speaker notes (não vai para o passo-a-passo do aluno)
- A **distinção Connect vs Entra ID vs External ID** (seção 1 deste memo) — fala do instrutor ao abrir F2.
- "Por que não Azure AD B2C?" → porque está **em depreciação** (datas da seção 3).
- "Por que não 3rd-party?" → o workshop é **Azure-native**.
- O **caminho de produção** (migrar `users` v1 → CIAM e aposentar bcrypt) como "próximos passos".
- O fato de que o **admin** continua no **workforce** (App Roles) — desenho canônico B2C: cliente no CIAM, funcionário no workforce.
- "Por que o encadeamento não muda?" → issuer-agnóstico; só authority/`aud` mudam.

---

## 7. Recomendação final, riscos e proposta de nova ADE

### Recomendação final
**ADOTAR o Microsoft Entra External ID (CIAM) como identidade do cliente final nas Quartas (F2)**, com o **workforce mantido apenas para o admin** (App Roles). Pré-provisionar a infra CIAM (instrutor) para caber em 6h. Manter o **v1 bcrypt coexistindo** com o v2/CIAM para preservar o contraste pedagógico. **Superseder a ADE-005** com uma nova ADE.

### Riscos
| Risco | Severidade | Mitigação |
|---|---|---|
| Trial tenant expira em 30 dias | Baixa | Recriar por turma via script/VSCode; ou converter para tenant com subscription (free 50K MAU) |
| User flow consome tempo se feito ao vivo | Média | **Pré-provisionar** (instrutor); aluno só associa App Reg |
| Social IdP exige app no Google (client id/secret) | Média | Instrutor pré-configura o IdP no tenant; ou usar só email+OTP no caso mínimo |
| Aluno confunde os 3 "Entra" | Média | Slide de desambiguação (seção 1) **obrigatório** no início de F2 |
| Limite 10K objetos do trial | Baixa | Irrelevante para turma de lab (dezenas de usuários) |
| Authority errada (workforce vs ciamlogin) no MSAL | Baixa-Média | Checklist explícito: `*.ciamlogin.com`, não `login.microsoftonline.com` |

### Proposta de nova ADE (bullets para o @architect formalizar) — supersede ADE-005

- **Título:** ADE-00X — Identidade do cliente via **Microsoft Entra External ID (CIAM)**; workforce restrito ao admin (supersede ADE-005)
- **Status:** Proposed (aguarda @architect)
- **Decisão:** A identidade do **cliente final (v2)** passa a usar **Entra External ID** (tenant CIAM, authority `<tenant>.ciamlogin.com`, user flow self-service, social IdP). O **workforce** (`login.microsoftonline.com` + App Roles) fica **restrito ao admin/operador**. O v1 (bcrypt) **coexiste** para contraste didático.
- **Invariantes propostas:**
  1. Identidade do cliente = **External ID/CIAM** (não workforce). Authority = `*.ciamlogin.com`.
  2. **1 user flow** sign-up/sign-in + ≥1 social IdP; **pré-provisionado pelo instrutor** (script/VSCode versionado).
  3. `oid` do CIAM continua sendo a chave → `entra_oid` (coluna **inalterada** vs F3; só muda a origem do GUID). **Sem mapping** (ADE-001 segue aposentada).
  4. **YARP valida o JWT** (`AddJwtBearer`) contra o discovery do **issuer CIAM** e propaga `X-Entra-OID` (ADE-004 preservada — gateway issuer-agnóstico).
  5. **Coexistência v1/v2** na mesma `purchases` (ADE-000 Invariante 1) — sem migração destrutiva.
  6. **Admin** permanece no **workforce** (App Roles Admin/Operator/Viewer).
- **Trade-offs aceitos:**
  - (+) Fidelidade B2C real (produto CIAM correto); (+) sucessor oficial do B2C legado; (+) US$0; (+) encadeamento técnico intacto (issuer-agnóstico).
  - (−) Reintroduz tenant CIAM + user flow → **mitigado** por pré-provisionamento (trial sem subscription + VSCode, atrito muito menor que em jun/2026); (−) aluno precisa entender 2 tenants (cliente CIAM + admin workforce) → mitigado por slide de desambiguação.
- **Supersedes:** ADE-005 (que adiou o External ID por atrito — premissa **revista** com trial sem subscription + extensão VSCode).
- **Preserva:** ADE-000 (microsserviço paralelo, schema aditivo), ADE-003 (baseline PaaS), ADE-004 (YARP valida JWT).

---

## Pontos abertos que precisam de DECISÃO do owner

1. **Social IdP no lab:** habilitar **Google** (mais realista, mas exige app no Google Cloud Console pelo instrutor) **ou** ficar em **email + OTP** (zero dependência externa)? — *Recomendo Google pré-configurado pelo instrutor; OTP como fallback.*
2. **Trial vs tenant com subscription:** usar **trial 30 dias** (recriável, sem cartão) ou **tenant External ID na subscription HML** (free 50K MAU, persistente)? — *Recomendo trial para turmas pontuais; subscription se o lab for recorrente.*
3. **Admin no escopo de F2?** Manter o admin (workforce + App Roles) dentro de F2 ou empurrar para fase posterior? — *Recomendo mencionar como speaker note em F2 e implementar só se sobrar tempo.*
4. **Caminho de migração v1→CIAM:** entra como speaker note (recomendado) ou vira passo do lab? — *Recomendo speaker note; passo do lab estoura as 6h.*
5. **Reescrita de artefatos:** confirmar que @architect formaliza a nova ADE e @pm atualiza blueprint (seções 3/8/4-F3)/epic (stack, riscos) — a F3 atual (workforce) precisa ser **re-narrada** como "admin" e a F2 ganha o customer CIAM.

---

## Fontes (Microsoft Learn e oficiais)

- [Microsoft Entra External ID — Overview](https://learn.microsoft.com/en-us/entra/external-id/external-identities-overview)
- [External Tenant Overview (CIAM)](https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam)
- [Create an External Tenant (Portal)](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-create-external-tenant-portal)
- [Quickstart: Trial setup (no subscription)](https://learn.microsoft.com/en-us/entra/external-id/customers/quickstart-trial-setup)
- [Create a User Flow (customers)](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-user-flow-sign-up-sign-in-customers)
- [Self-service sign-up overview](https://learn.microsoft.com/en-us/entra/external-id/self-service-sign-up-overview)
- [Identity providers for external tenants (Google/Facebook/Apple)](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers)
- [Apple IdP support (Entra External ID)](https://devblogs.microsoft.com/identity/openid-connect-social-identity-provider-apple/)
- [Entra External ID — Authority URL (ciamlogin.com)](https://learn.microsoft.com/en-us/answers/questions/2285781/entra-external-id-signupsignin-authority-url)
- [Get started guide features](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-guide-explained)
- [External ID Pricing (Microsoft Learn)](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing)
- [Microsoft Entra External ID — Pricing (Azure)](https://azure.microsoft.com/en-us/pricing/details/microsoft-entra-external-id/)
- [New Azure AD B2C customers after May 2025 (depreciação)](https://learn.microsoft.com/en-us/answers/questions/2150272/new-azure-ad-b2c-customers-after-may-2025)
- [Microsoft to End Sale of Azure AD B2B/B2C on May 1, 2025](https://envisionit.com/resources/articles/microsoft-to-end-sale-of-azure-ad-b2bb2c-on-may-1-2025-shifting-to-entra-id-external-identities)
- [MSAL React sample — sign-in (External ID)](https://learn.microsoft.com/en-us/samples/azure-samples/ms-identity-ciam-javascript-tutorial/ms-identity-ciam-javascript-tutorial-1-sign-in-react/)
- [MSAL React sample — call protected API (External ID)](https://learn.microsoft.com/en-us/samples/azure-samples/ms-identity-ciam-javascript-tutorial/ms-identity-ciam-javascript-tutorial-1-call-api-react/)
- [Fast Entra External ID setup with VSCode](https://www.azuredive.net/2024/07/fast-entra-external-id-setup-with-vscode/)
