# ADE-004 — Gateway via YARP em Código .NET (substitui APIM Developer)

> **Tipo:** Architecture Decision Entry (component substitution)
> **Status:** ✅ Accepted
> **Date:** 2026-06-03
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 F2 (`phase-02-apim` → a renomear) e toda fase que dependa do gateway (F3 valida JWT no gateway)
> **Supersedes:** Decisão de stack APIM do blueprint seção 3 (linha "APIM Developer"); revisa ADE-000 Invariante 6 (APIM como único recurso compartilhado)
> **Related:** ADE-000 (microsserviço paralelo), ADE-003 (baseline PaaS + Azure SQL DB), ADE-005 (identidade — validação de JWT no gateway)

---

## Context

Esta ADE foi gerada em sessão de re-escopo do EPIC-002 com o owner (Guilherme Prux Campos) em 2026-06-03.

O blueprint original (seção 3 e fase F2) adotava **Azure API Management (APIM) Developer tier** como o gateway do fluxo v2 — 1 instância compartilhada para toda a turma, com products/subscriptions por aluno (cenário A do cost model, ~US$50-80). A story 2.2 (`phase-02-apim`, Draft) materializa essa decisão: product, subscription key, API backend, e uma policy library XML (`rate-limit-by-key`, `cache`, `validate-jwt`, `cors`, `set-header`, `rewrite-uri`).

Na sessão de re-escopo, o owner identificou que o APIM Developer introduz **atrito desproporcional ao valor didático** num workshop de 6h por fase:

- **Provisioning lento:** uma instância APIM Developer leva ~30-45min para provisionar — incompatível com o ritmo de uma aula ao vivo e com o padrão "Portal passo-a-passo, aluno replica".
- **Custo:** ~US$50-80 pro-rata, o maior item isolado do cost model do evento (SC-4).
- **Complexidade desnecessária:** products/subscriptions/keys, portal de developer e test-console são superfície de produto gerenciado que o aluno não consegue reproduzir/inspecionar em código — o "como funciona por dentro" fica opaco.

A decisão (fechada com o owner — esta ADE apenas a formaliza) é **remover o APIM Developer e implementar o gateway como código C# usando YARP (Yet Another Reverse Proxy)**, hospedado em recurso serverless/container que o aluno já domina das fases anteriores. Os **objetivos pedagógicos de gateway permanecem intactos** (rate-limit, header/path transform, JWT validation, CORS) — agora ensinados em código, reforçando o fio condutor "microsserviço .NET".

## Decision

Adotamos o pattern **"Gateway-as-Code com YARP"** com 5 invariantes:

### Invariante 1: O gateway do fluxo v2 é YARP em código .NET, não APIM

O entrypoint do fluxo v2 deixa de ser uma instância APIM gerenciada e passa a ser uma aplicação **ASP.NET Core + YARP** (`Yarp.ReverseProxy`), de código aberto e custo zero, versionada no repo junto com os microsserviços. A configuração de rotas/clusters/transforms vive em `appsettings.json` (declarativa) e/ou em código (`AddReverseProxy().LoadFromConfig(...)` + transforms programáticos).

### Invariante 2: Hosting do gateway — Container App dedicado (recomendado) ou Function .NET

O YARP é hospedado em **Azure Container Apps (Consumption)** como recurso recomendado:

- Container Apps já é stack do epic (F4 sobe n8n lá) — reaproveita conhecimento e não adiciona um tipo de recurso novo.
- ASP.NET Core + YARP é o host natural de um reverse proxy de longa duração (middleware pipeline, streaming, conexões keep-alive) — encaixa no modelo de container melhor do que numa Function.
- Scale-to-zero do Consumption mantém o custo ~US$0 em ociosidade.

Alternativa aceitável (documentada): **Azure Functions .NET isolated com ASP.NET Core integration** (custom handler / `app.MapReverseProxy()` no host isolated), caso a turma prefira manter tudo em Functions. Trade-off: YARP em Function tem fricção com cold start e com o modelo de proxy de longa duração — por isso Container App é o default. A escolha final de hosting é ponto de validação no design da story 2.2 re-drafted.

### Invariante 3: As capacidades de gateway são reimplementadas em YARP — sem perda pedagógica

Cada policy que o APIM entregaria tem equivalente em código YARP/ASP.NET Core. A tabela abaixo é o contrato de paridade (nenhum objetivo didático de F2 se perde):

| Capacidade (APIM policy) | Equivalente YARP / ASP.NET Core | Onde |
|---|---|---|
| `rate-limit-by-key` | `Microsoft.AspNetCore.RateLimiting` (`AddRateLimiter` + partição por chave/usuário) | middleware antes do proxy |
| `cache-lookup` / `cache-store` | `OutputCache` (`AddOutputCache` + `[OutputCache]` / policy) ou `ResponseCaching` | middleware no pipeline |
| `set-header` (ex.: `X-Correlation-ID`) | YARP `Transforms` → `RequestHeader` / `AddRequestTransform` | cluster/route transform |
| `rewrite-uri` / path strip | YARP `PathPattern` / `PathRemovePrefix` transform | route config |
| `cors` | `AddCors` + `UseCors` (origin restrito ao front) | middleware |
| `validate-jwt` | `AddAuthentication().AddJwtBearer(...)` + `[Authorize]` / `RequireAuthorization()` validando o issuer Entra (ADE-005) | auth middleware antes do proxy |

> **Nota didática (ganho real):** ensinar rate limiting, output cache, header transform e validação de JWT **em C#** mostra "o que um gateway faz por dentro" — algo que o APIM, como caixa-preta gerenciada, esconde. Isso reforça o fio condutor "microsserviço .NET" do epic em vez de quebrá-lo com um produto PaaS de configuração proprietária.

### Invariante 4: A validação de JWT no gateway é o ponto de integração com a identidade (ADE-005)

Quando F3 (ADE-005) habilitar Entra ID, a validação de token passa a acontecer **no YARP**, via `AddJwtBearer` apontando para o discovery do issuer Entra (`.well-known/openid-configuration`). O gateway valida `iss`/`aud`/assinatura, extrai o claim `oid` e o propaga downstream para a Function (header `X-Entra-OID` ou via token repassado). Em F2, a autenticação fica desabilitada (rotas anônimas), exatamente como o placeholder `<choose>` desabilitado fazia no APIM — a paridade de faseamento é preservada. Detalhe completo do fluxo de claims em ADE-005.

### Invariante 5: Não há mais recurso Azure compartilhado entre alunos

Com a remoção do APIM, **deixa de existir o "único recurso compartilhado" da ADE-000 Invariante 6**. O gateway YARP é provisionado **por aluno** (cada aluno sobe seu Container App de gateway no próprio RG), igual aos demais recursos do epic. Isso **revisa a ADE-000 Invariante 6**, cuja última frase ("ÚNICO recurso compartilhado: APIM") fica obsoleta — agora todos os recursos do fluxo v2 são per-aluno, sem exceção. Consequência positiva de isolamento: falha do gateway de um aluno não derruba o workshop inteiro (risco que a ADE-000 Consequência negativa "APIM compartilhado derruba o workshop" levantava — agora eliminado).

---

## Rationale

### Por que YARP (vs manter APIM Developer)?

- **Custo zero vs ~US$50-80:** YARP é open-source, roda no Container App Consumption (scale-to-zero ~US$0). Derruba o maior item isolado do cost model do evento.
- **Deploy instantâneo vs 30-45min de provisioning:** um Container App sobe em segundos/poucos minutos via `az containerapp` ou Portal; uma instância APIM Developer leva 30-45min — inviável para demo ao vivo.
- **Transparência didática:** o aluno **lê e escreve** o código do gateway (rate-limit, cache, transform, JWT) em C#. APIM esconde isso atrás de policies XML proprietárias e de um portal gerenciado. YARP ensina o conceito de gateway, não a operação de um produto específico.
- **Alinhamento com o fio condutor do epic:** o epic é "microsserviço .NET". Um gateway que também é .NET reforça a narrativa; APIM a interrompe com um produto PaaS de paradigma diferente.
- **Elimina superfície de atrito:** products, subscriptions, keys, developer portal — toda a cerimônia do APIM some. O aluno chama o gateway por URL direta com (em F3) um Bearer token Entra.

### Por que Container App (vs Function) para hospedar o YARP?

- Reverse proxy é workload de **longa duração** (pipeline de middleware, streaming, keep-alive). Container/ASP.NET Core é seu habitat natural; Function Consumption sofre com cold start no caminho crítico do gateway.
- Container Apps **já está no stack** (F4). Não introduz um tipo de recurso novo na cabeça do aluno.
- Mantém custo ~US$0 com scale-to-zero.

### Por que não se perde nada pedagogicamente?

- A tabela de paridade (Invariante 3) cobre 1:1 cada policy que F2 ensinaria. O **conceito** de cada capacidade de gateway é idêntico; muda só o **meio** (código C# em vez de XML APIM) — e o meio em código é mais transparente.
- O único "recurso de produto" que se perde é o **test-console e o developer portal do APIM**. Isso é substituído por `curl`/Postman + logs do Container App + App Insights — ferramentas que o aluno já usa desde F1.

---

## Consequences

### Positivas

- ✅ Custo compartilhado do evento cai de **~US$95 para ~US$0** (o gateway some do cost model como item compartilhado; vira recurso per-aluno de custo ~US$0 no Consumption). SC-4 ("custo compartilhado ≤ US$95") fica trivialmente satisfeito — na prática não há mais custo compartilhado de gateway.
- ✅ Deploy do gateway em segundos — compatível com aula ao vivo.
- ✅ Isolamento total entre alunos — falha de um gateway não afeta os demais (elimina o risco "APIM compartilhado derruba o workshop" da ADE-000).
- ✅ Gateway versionado no repo, deployável via mesmo CI/CD por fase — coerente com a estratégia de branching cumulativo.
- ✅ Reforça o fio condutor "microsserviço .NET"; gateway deixa de ser caixa-preta e vira código auditável.

### Negativas / Trade-offs aceitos

- ⚠️ **Perde-se o produto gerenciado APIM:** sem developer portal, sem test-console gráfico, sem analytics nativos do APIM. Mitigado: para um workshop educacional esses recursos não são objetivo de aprendizado; logs + App Insights cobrem observabilidade.
- ⚠️ **Mais código a manter:** o gateway agora é código do repo (rate-limiter, cache, transforms, auth). Mitigado: é código simples e didático; serve de material de aula em si.
- ⚠️ **Aluno não vê "APIM na prática":** quem queria especificamente APIM no currículo não o terá. Mitigado: documentar nota didática "em produção corporativa, o equivalente gerenciado é o APIM — aqui ensinamos o conceito em código para transparência" (vira slide/SPEAKER-NOTES de F2).
- ⚠️ **Branch `phase-02-apim` tem nome agora enganoso.** Mitigado: renomear para `phase-02-gateway` (ação para @sm/@devops no re-draft; ver Impact).

---

## Alternatives Considered (rejeitadas)

### Alt 1: Manter APIM Developer (decisão original do blueprint)

- **Rejected porque:** provisioning de 30-45min inviabiliza a demo ao vivo; custo ~US$50-80 é o maior item do evento; superfície de produto opaca contraria a transparência didática; paradigma PaaS quebra o fio condutor "microsserviço .NET". Foi a decisão original e está sendo explicitamente substituída por esta ADE.

### Alt 2: APIM Consumption tier (em vez de Developer)

- **Rejected porque:** o APIM Consumption reduz custo (pay-per-call) e provisiona mais rápido que o Developer, mas **não suporta a superfície completa de policies** que o Developer oferece (não tem cache embutido, developer portal, nem alguns recursos de policy) — justamente o que o blueprint queria do Developer. Além disso continua sendo um produto gerenciado proprietário (mesma objeção de transparência da Alt 1) e ainda assim adiciona um recurso/custo que o YARP zera. YARP entrega rate-limit + cache + transform + JWT em código, com custo zero e total transparência — superior em todos os eixos relevantes ao workshop. APIM Consumption só venceria se "saber operar o produto APIM" fosse objetivo de aprendizado, o que o owner descartou.

### Alt 3: YARP hospedado em Azure Functions (em vez de Container App)

- **Rejected como default (mantido como alternativa aceitável)** porque: reverse proxy é workload de longa duração e sofre com cold start no caminho crítico quando em Function Consumption; Container App é o host natural. Mantida como opção se a turma preferir uniformidade total em Functions — decisão de design da story 2.2.

### Alt 4: Sem gateway (front chama Function direto)

- **Rejected porque:** elimina por completo os objetivos pedagógicos de gateway (rate-limit, transform, validação centralizada de JWT, CORS centralizado) que são o coração de F2. O gateway é o tema da fase, não um detalhe de infra.

---

## Validation

Esta substituição é considerada **validada** quando:

- [ ] Gateway YARP (ASP.NET Core) sobe em Container App por aluno e roteia `POST /purchase` / `GET /purchase/{correlationId}` para a Function F1.
- [ ] Rate limiting em código retorna 429 na chamada além do limite (paridade com `rate-limit-by-key`).
- [ ] Output cache responde 2ª chamada idêntica de `GET` mais rápido, com header de cache hit (paridade com `cache-store`).
- [ ] CORS restrito ao domínio do front; `X-Correlation-ID` propagado downstream (paridade com `cors` + `set-header`).
- [ ] (F3) `AddJwtBearer` valida token Entra e propaga `oid` downstream (integração com ADE-005).
- [ ] Cost report final confirma US$0 de custo compartilhado de gateway.
- [ ] Nenhuma instância APIM provisionada no evento.

## Impact on EPIC-002

### Stories que precisam de re-draft

| Story | Impacto | Ação (executor) |
|---|---|---|
| **2.2 (F2)** | **Re-draft completo.** Atualmente é "APIM Developer + policies XML" (Status Draft). Precisa virar "Gateway YARP em código .NET": trocar AC-2/AC-3/AC-4 (product/subscription/API APIM) por "subir Container App de gateway YARP"; reescrever AC-5..AC-9 (policies XML) para os equivalentes em código da Invariante 3; trocar AC-12 (`apim/policies/standard-policies.xml`) por o projeto YARP versionado; ajustar branch para `phase-02-gateway`; atualizar Executor Assignment (gateway é código .NET de @dev, deploy de Container App por @devops). Como está em Draft, re-draft é livre — **@sm**. |
| **2.3 (F3)** | Impacto parcial: a ativação de `validate-jwt` deixa de ser "policy XML do APIM" e passa a ser `AddJwtBearer` no YARP (ver ADE-005, que tem o impacto principal em 2.3). AC-6 e Task 6 de 2.3 devem referenciar o gateway YARP, não o APIM — **@sm** (em conjunto com o re-draft motivado por ADE-005). |

> **NÃO re-drafto as stories** (autoridade de @sm). Esta ADE apenas aponta o impacto.

### Artefatos a atualizar (apontados para os owners)

- **Blueprint seção 3 (stack table):** substituir a linha "APIM Developer / ~US$50/mês" por "Gateway YARP (ASP.NET Core) em Container App / ~US$0" — **@pm**.
- **Blueprint seção 4 F2 + seção 6/cost model:** reescrever F2 ("Gateway Profissional") para gateway-as-code; atualizar cost model seção 10 (custo compartilhado some o APIM) — **@pm**.
- **EPIC-002 (`docs/epics/EPIC-002-living-lab-workshop.md`):** stack list (linha 25), tabela de stories S2 (título e branch), SC-4 (custo compartilhado), riscos (o risco "APIM compartilhado" deixa de existir) — **@pm**.
- **ADE-000 Invariante 6:** anotar que a exceção "APIM compartilhado" foi removida por esta ADE-004 (todos os recursos viram per-aluno). Como ADE-000 é imutável durante o epic, esta ADE-004 registra a revisão por superseção parcial — **@architect** (registrado aqui).

---

**Authority:** Aria (Architect) — designado por @aiox-master para decisões de seleção de tecnologia e integração.
**Review cycle:** Imutável durante EPIC-002. Mudanças → nova ADE que a supersede.
