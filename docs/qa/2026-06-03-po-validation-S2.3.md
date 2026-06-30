# PO Validation Report — Story 2.3 (F3: App Registration workforce + MSAL.js + Easy Auth)

**Validator:** Pax (Product Owner)
**Date:** 2026-06-03
**Story file:** `docs/stories/2.3.story.md`
**Parent epic:** `docs/epics/EPIC-002-living-lab-workshop.md`
**Source decisions:** `docs/architecture/ade-005-identity-easy-auth.md` (+ ADE-004, ADE-003, ADE-000)
**Task executed:** `validate-next-story.md`
**Context:** Re-draft 2026-06-03 (External ID → App Registration tenant workforce + MSAL.js; validate-jwt APIM → `AddJwtBearer` no YARP). ADE-005 supersede a ADE-001 (mapping resolvido). As decisões são lei — esta validação afere **qualidade e coerência**, não rediscute arquitetura.
**Verdict:** ✅ **GO — 8/10 (Confidence: HIGH)**

---

## 1. Checklist 10 pontos (story-lifecycle.md)

| # | Critério | Verdict | Observação |
|---|---|---|---|
| 1 | Título claro e objetivo | ✅ PASS | "F3: Identidade Moderna com App Registration (tenant workforce) + MSAL.js + Easy Auth" — reflete o re-escopo |
| 2 | Descrição completa | ✅ PASS | As a/I want/so that do aluno; motivação concreta (OIDC moderno vs auth local v1) |
| 3 | AC testáveis | ✅ PASS | 14 ACs verificáveis (401 em token expirado/issuer/aud; `entra_oid` preenchido em SQL; fluxo MSAL no browser) |
| 4 | Escopo bem definido | ✅ PASS | IN: App Reg + MSAL.js + AddJwtBearer + schema delta + Function update; OUT: v1 intocado (explícito) |
| 5 | Dependências mapeadas | ✅ PASS | Depends on 2.1 + 2.2 Done; ADE-005/004/003/000; gateway YARP de F2; auth.js v1 para comparação |
| 6 | Complexidade estimada | ✅ PASS | Complexity HIGH (CodeRabbit) + 6h timeboxed |
| 7 | Valor de negócio | ✅ PASS | Story "para que..." + base de F5/F6 (oid downstream); custo US$0 (SC-4) |
| 8 | Riscos documentados | ✅ PASS | AC-12 segurança (cenários 401) + AC-14 anti-hallucination + foco CodeRabbit (oid não vazado em log) |
| 9 | Critérios de Done claros | ✅ PASS | 14 ACs + 10 tasks + Testing (security scenarios + regressão v1) |
| 10 | Alinhamento com PRD/Epic | ✅ PASS | Título, branch `phase-03-identity`, stack batem com EPIC-002 (S3 linha 69 + decisão #4) e ADE-005 |

**Score: 10/10 (checklist) → GO**

---

## 2. Validações estruturais (validate-next-story.md)

### 2.1 Template Completeness
- ✅ Todas as seções presentes; zero placeholders de template não-resolvidos.
- Nota: `<tenant-id>` / `<client-id>` / `<iniciais>` são placeholders de runtime por aluno (valores reais via App Settings `EntraTenantId`/`EntraClientId`) — legítimos, não pendências de template.

### 2.2 Estrutura equivalente ao molde 2.1 (9 sub-itens + 6 artefatos)
| Sub-item do molde | Presente em 2.3? |
|---|---|
| Objetivos pedagógicos | ✅ Comparação v1 vs v2 + OIDC/PKCE/claims (Dev Notes) |
| Arquitetura técnica delta | ✅ Diagrama ASCII (MSAL → gateway → Function) |
| Endpoints/rotas | ✅ Rotas v2 com `RequireAuthorization()` (AC-6) |
| Schema delta | ✅ `phase-03.sql` idempotente (AC-8, SQL completo em Dev Notes) |
| CI/CD esqueleto | ✅ `deploy-phase-03.yml` (Task 1.2) |
| Roteiro de aula | ⚠️ **Ausente** — ver Should-Fix #1 |
| DoD aluno | ✅ AC-11 smoke E2E + AC-12 segurança |
| Troubleshooting | ⚠️ **Tabela dedicada ausente** (cenários de falha cobertos só em AC-12/Testing) — ver Should-Fix #2 |
| Tempo por sub-tópico | ⚠️ **Ausente** (sem tabela de minutagem 6h) — ver Should-Fix #1 |

- ✅ **6 artefatos didáticos:** AC-13 lista README, PORTAL-GUIDE, SPEAKER-NOTES, slides, intro-video-script, branch+workflow.

### 2.3 Executor Assignment
- executor: `@dev` ✅ · quality_gate: `@architect` ✅ · executor ≠ quality_gate ✅
- quality_gate_tools: `[code-review, security-validation, auth-flow-validation]` (non-empty) ✅
- Type-to-executor consistency: Security/Architecture + código → @dev com @architect ✅

### 2.4 File Structure
- Paths claros: `src/Fifa2026.V2.Gateway/Program.cs`, `fifa2026-api/database/migrations/phase-03.sql`, frontend Vite/React, `docs/workshops/phase-03/`, test projects
- Sequência lógica: branch → App Reg → schema → AddJwtBearer → MSAL front → Function update → Easy Auth doc → smoke → artefatos → anti-hallucination ✅

### 2.5 AC Satisfaction Coverage
| AC | Task(s) |
|---|---|
| AC-1 (branch) | Task 1 |
| AC-2 (App Reg SPA) | Task 2.1 |
| AC-3 (social login) | Task 2.3 |
| AC-4 (App Roles admin) | Task 2.4 |
| AC-5 (MSAL.js caminho b) | Task 5 |
| AC-6 (AddJwtBearer no YARP) | Task 4.1–4.2 |
| AC-7 (propaga X-Entra-OID) | Task 4.3 |
| AC-8 (schema delta entra_oid) | Task 3 |
| AC-9 (Function lê X-Entra-OID) | Task 6 |
| AC-10 (comparação v1/v2) | Task 9.1 |
| AC-11 (smoke E2E) | Task 8 |
| AC-12 (segurança 401) | Task 4.4 + 8.2 |
| AC-13 (6 artefatos) | Task 9 |
| AC-14 (anti-hallucination) | Task 10 |

Coverage: 100%

### 2.6 Fidelidade à ADE-005 (caminho MSAL b + Invariantes)
| ADE-005 | Story 2.3 | Fiel? |
|---|---|---|
| Inv 1 (tenant workforce, sem External ID) | AC-2 ("Sem tenant External ID — usa o tenant workforce") | ✅ |
| Inv 2 (caminho b = MSAL.js + PKCE recomendado; Easy Auth complementar) | AC-5 ("caminho b — RECOMENDADO") + Task 7 (Easy Auth opcional/documentado) | ✅ |
| Inv 3 (`oid` como chave; `entra_oid` aditivo; sem tabela de mapping) | AC-8 + schema delta idempotente; AC-8 nota "ADE-001 resolvida — NÃO criar `ade-001`" | ✅ |
| Inv 4 (YARP valida JWT, propaga `oid` downstream) | AC-6 + AC-7 + AC-9 (Function confia no header, não revalida) | ✅ |
| Inv 5 (contrato de setup App Reg) | Dev Notes tabela "Requisitos de configuração de identidade" | ✅ |
| Consequências (nota didática "External ID = CIAM em B2C real") | AC-10 + Dev Notes nota didática | ✅ |

- ✅ **Integração com ADE-004 Inv 4:** AC-6 ativa o placeholder JWT deixado em F2 (AC-9 da 2.2) — coerência cumulativa F2→F3 explícita.
- ✅ **ADE-001 corretamente aposentada:** Task 2 (criar ADE-001) removida; arquivo `ade-001-entra-id-mapping.md` confirmado **inexistente** (não deve ser criado). Sem invenção de artefato.

### 2.7 AVALIAÇÃO DO PONTO #1 — AUTO-DECISION `entra_oid` em `purchases` vs `users`

O @sm registrou `[AUTO-DECISION]` (AC-8 + Dev Notes) escolhendo coluna em **`purchases`** (paralelismo v1/v2 na mesma tabela, ADE-000 Inv 1), notando que se @architect preferir `users` o ajuste é trivial e não invalida a story.

**Avaliação (critério: "sem ambiguidades bloqueantes para o @dev"):**

- ADE-005 Invariante 3 explicitamente lista **ambas** as tabelas como válidas: *"A tabela `purchases` (e/ou `users`) recebe o `oid`... A coluna em `users` é a opção limpa se o vínculo for por usuário."* A ADE deixa a escolha aberta **por design** — não é uma lacuna da story, é latitude concedida pela própria decisão arquitetural (lei).
- A AUTO-DECISION é **bem fundamentada** (rationale + referência a ADE-000 Inv 1) e **determinística para o @dev**: a story prescreve um alvo concreto (`purchases`) com SQL idempotente completo. O @dev **não fica bloqueado nem em ambiguidade** — há um caminho único e executável.
- O impacto de uma eventual mudança para `users` é **localizado e baixo** (uma coluna + índice + qual tabela o INSERT da Function alimenta), reversível sem re-draft.
- Verificação técnica: `purchases.user_id INT` existe no schema (FK para `users`), então ambas as opções são fisicamente viáveis; `purchases` mantém a comparação v1/v2 lado-a-lado na mesma linha — coerente com o objetivo didático.

**Veredito sobre #1:** A AUTO-DECISION é **ACEITÁVEL para a transição Draft → Ready**. Não bloqueia, pois (a) a ADE autoriza ambas, (b) a story dá ao @dev um alvo único e executável, (c) o impacto de reversão é trivial. **NÃO é necessário fechar com @architect antes de Ready.** Recomendação: registrar como **confirmação leve no quality gate de arquitetura** (@architect já é o quality_gate desta story) — se @architect preferir `users`, ajusta no design sem reabrir a story. Tratado como nota informativa para @architect, não como gate bloqueante.

### 2.8 Testing
- Test approach claro: integration (`WebApplicationFactory` + mock JWT com chave conhecida), security (4 cenários 401/200 + X-Entra-OID), smoke (login MSAL real), regressão v1 (endpoint v1 sem token continua OK)
- Frameworks reais: xUnit, `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt` ✅

### 2.9 Security (foco central desta story)
- ✅ Cenários de rejeição explícitos: token expirado → 401; issuer errado → 401; aud errado → 401 (AC-12 + Task 4.4)
- ✅ `entra_oid` como `UNIQUEIDENTIFIER NULL` (não NOT NULL — alunos antigos sem oid não quebram) — foco CodeRabbit
- ✅ `oid` não vazado em logs — foco CodeRabbit primário
- ✅ PKCE sem client_secret no browser (SPA) — alinhado a ADE-005 Inv 2
- ✅ Gateway como guardião único; Function confia no header propagado e NÃO revalida (intencional, documentado — ADE-005 Inv 4)

### 2.10 CodeRabbit Integration (config `enabled: true`)
- ✅ Section completa: Story Type (Security+Architecture, HIGH), Agents (@dev/@analyst primary, @architect/@devops supporting), Quality Gates (Pre-Commit/Pre-PR/Pre-Deployment), Self-Healing (@dev light, 2 iter), Focus Areas (JWT validation, oid não em log)
- Verdict: PASS

### 2.11 Anti-Hallucination
- ✅ AC-14 + Task 10 validam claims `oid`/`iss`/`aud`, API `AddJwtBearer` e `@azure/msal-browser` contra docs Microsoft oficiais
- ✅ Claims verificados no código existente: `purchases` table existe; `purchases.user_id INT` (identidade v1 int) confirmado; v1 JWT via `jsonwebtoken` confirmado em `auth.js`; bcrypt confirmado em `fifa2026-api/src/routes/`. A tabela comparativa v1 vs v2 (Dev Notes) é factualmente correta.
- ✅ `src/Fifa2026.V2.Gateway/` referenciado como dependência forward de 2.2 (correto, não invenção). `ade-001` corretamente inexistente.
- Verdict: PASS

### 2.12 Dev Agent Implementation Readiness
- Self-contained: Dev Notes traz arquitetura, contrato de setup de identidade, schema SQL completo, comparação v1/v2, integração ADE-004
- Actionability: alta — @dev competente em ASP.NET Core + MSAL.js + Entra executa sem docs externos
- Verdict: HIGH readiness

---

## 3. Findings

### Critical Issues (Must Fix — Story Blocked)
Nenhum. ✅

### Should-Fix Issues (Important Quality Improvements — NÃO bloqueiam o GO)
1. **Roteiro de aula 6h ausente.** O molde 2.1 (e a 2.2 re-drafted) têm uma tabela "Roteiro de aula (6h = 360min)" com blocos e minutagem; a 2.3 não tem (apesar de SC-1 do epic ser "completar dentro das 40h" e o sub-item "Tempo por sub-tópico" ser parte do molde de 9 sub-itens). Recomendo @sm adicionar a tabela de roteiro (paridade com 2.1/2.2). Não bloqueia porque o conteúdo técnico está completo e o roteiro pode entrar via SPEAKER-NOTES (Task 9.3), mas a paridade estrutural com o molde ficaria mais limpa.
2. **Tabela de Troubleshooting dedicada ausente.** A 2.1 e a 2.2 têm seção "Troubleshooting esperado" (sintoma/causa/mitigação). A 2.3 cobre falhas só via AC-12 + Testing. Para identidade (alta taxa de erros de config: redirect URI, aud, CORS no token endpoint, relógio/expiração) uma tabela de troubleshooting agregaria valor didático. Recomendo @sm adicionar (paridade com molde).

### Nice-to-Have Improvements (Opcional)
1. **AC-8 "`purchases` e/ou `users`":** o texto do AC ainda carrega o "e/ou" antes da AUTO-DECISION resolver. Está resolvido logo abaixo na própria AC pela AUTO-DECISION (alvo = `purchases`), então é cosmético — @dev tem alvo único.
2. **Easy Auth (Task 7) marcado "opcional":** bem delimitado como complemento (ADE-005 Inv 2). Sem ação.

### Anti-Hallucination Findings
- ✅ Nenhuma claim inventada. Comparação v1/v2 verificada contra schema e código reais. `ade-001` corretamente não criada.

### CodeRabbit Integration Findings
- ✅ Section completa e consistente com o tipo Security+Architecture.

### Code Intelligence Check
- ➖ N/A (auto-skip per Step 8.1).

---

## 4. Final Assessment

| Métrica | Valor |
|---|---|
| **Verdict** | ✅ **GO** |
| **Implementation Readiness Score** | **8/10** (-2 pelos 2 Should-Fix de paridade estrutural: roteiro 6h + troubleshooting) |
| **Confidence Level** | **HIGH** |
| **AUTO-DECISION (`entra_oid` em `purchases`)** | **ACEITA** — não bloqueia Ready; confirmação leve no QG de @architect |
| **Status transition** | **Draft → Ready** (aplicado em `docs/stories/2.3.story.md`) |
| **Change Log updated** | ✅ (versão 0.3.0) |

> Score 8/10 (≥7) ⇒ GO. Os 2 Should-Fix são melhorias de **paridade estrutural** com o molde, não lacunas que impeçam a implementação — o conteúdo técnico está completo, testável e fiel à ADE-005. @sm pode incorporá-los antes ou durante a aula sem reabrir a validação. Se o owner preferir paridade estrutural estrita antes do dev start, basta @sm adicionar as 2 tabelas (incremento pequeno).

---

## 5. Next Steps
1. ✅ Status field atualizado: Draft → Ready
2. ✅ Change Log entry adicionada (0.3.0)
3. ✅ Relatório salvo aqui
4. **@architect (quality gate):** confirmação leve do alvo da coluna (`purchases` vs `users`) durante o QG de arquitetura — não-bloqueante
5. **@sm (opcional, recomendado):** adicionar tabela de roteiro 6h + tabela de troubleshooting (paridade com molde 2.1/2.2)
6. Story pronta para `@dev *develop 2.3` (após 2.1 + 2.2 Done — cadeia cumulativa)
7. @analyst pode iniciar artefatos de F3 (Task 9) em paralelo

---

**Reference:** `story-lifecycle.md` Phase 2 — Draft → Ready é responsabilidade @po; cumprida. Flip de status registrado neste GO.
