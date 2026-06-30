# EPIC-002 — Living Lab Workshop Azure-Native

> **Owner:** Morgan (PM) · **Status:** 📝 **DRAFT** · **Created:** 2026-05-24 · **Predecessor:** EPIC-001 (Active)
> **Target event:** Workshop educacional Azure-Native durante a Copa do Mundo FIFA 2026
> **Estimated total duration:** ~40h (workshop time, dividido em 6 fases cumulativas)
> **Source blueprint:** [docs/workshops/2026-blueprint-living-lab-azure.md](../workshops/2026-blueprint-living-lab-azure.md)
> **Source handoff:** [.aiox/handoffs/handoff-2026-05-24-analyst-to-pm-living-lab.yaml](../../.aiox/handoffs/handoff-2026-05-24-analyst-to-pm-living-lab.yaml)

---

## Motivação

A aplicação **FIFA 2026 Tickets** já provou seu valor didático na "Copa do Mundo Azure" da TFTEC (EPIC-000 ✅ done, EPIC-001 ativa). O próximo salto é transformá-la em **laboratório vivo** de um workshop de **40 horas** durante a Copa real, onde cada fase do evento evolui materialmente a aplicação introduzindo uma capacidade Azure-native sob a forma de **microsserviço .NET**, sem tocar no backend Node original.

A diferenciação vs EPIC-001:
- **EPIC-001:** ensina **modernização** (VM → PaaS)
- **EPIC-002:** ensina **arquitetura cloud-native** (PaaS → microsserviços, mensageria, gateway, identity, AI, observabilidade)

Os alunos não criam um "hello world" descartável. Evoluem um produto real em 6 fases cumulativas, terminando com app rodando em suas próprias subscriptions Azure.

## Escopo (in scope)

- **6 fases cumulativas** (F1–F6) cobrindo o ciclo completo de arquitetura Azure-native moderna
- **Backend Node + frontend Vite + SQL Server permanecem intocados** — novos componentes coexistem em fluxo v2 paralelo
- **Stack Azure-Native fechado:** Service Bus Standard · Gateway YARP (.NET) em Container App por aluno (substitui APIM Developer — ADE-004) · Functions Consumption (.NET 8 isolated) · Container Apps · **Identidade dois-mundos: cliente no Microsoft Entra External ID / CIAM (`ciamlogin.com`, user flow, Google + OTP) + admin no workforce (App Roles); gateway YARP valida o JWT do CIAM (ADE-007 — supersede ADE-005)** · MCP server · Gemini 2.0 Flash · App Insights · SignalR free tier
- **6 artefatos por fase** (obrigatórios): README aluno · PORTAL-GUIDE · SPEAKER-NOTES · slides · vídeo intro · branch executável com CI/CD
- **Provisioning sempre via Portal Azure passo-a-passo** (Bicep vira apêndice opcional)
- **CI/CD por fase** via GitHub Actions com branches cumulativas (`phase-01-…` → `phase-02-…` → … → `main`)

## Fora de escopo (out of scope)

Reservado para epic posterior ou stories independentes:

- Reescrever backend Node em .NET (anti-padrão didático — quebra a "vacinas sagradas")
- Mobile app (Android/iOS) — fora da audiência
- Internacionalização (i18n) — produto continua em PT-BR
- Pagamento real (Stripe, PayPal) — fluxo permanece mock
- Multi-currency dinâmica — preços continuam em USD
- Análise pós-evento de métricas de uso real durante a Copa (epic separado)

## Restrições firmes (vacas sagradas)

- ✅ Manter stack atual: Vite+React+Node+SQL Server (apenas **estende** com microsserviços .NET)
- ✅ Manter Azure como cloud única
- ✅ Manter propósito didático visível em cada fase
- ✅ Evento gratuito — guard rails de custo obrigatórios

## Pré-condições do epic (pré-flight)

- ⛔ **EPIC-001 S4 concluído — Azure SQL Database ativa (não SQL em VM)** antes de iniciar F1. **Restrição física, não preferencial** (ADE-003 Inv 2): Azure Functions em Consumption plan não estão em VNet e não alcançam SQL em VM com IP privado. Sem Azure SQL DB, F1 não consegue gravar no banco. Confirmar no pré-flight checklist do aluno.

## Success criteria

| # | Critério | Verificação |
|---|---|---|
| SC-1 | Aluno completa as 6 fases dentro das 40h alocadas | Cronômetro por bloco; checkpoint ao final de cada fase |
| SC-2 | App permanece funcional em cada fase (estados intermediários válidos) | Smoke test fluxo v1 + fluxo v2 ao final de cada fase |
| SC-3 | Aluno termina F6 com Flow Visualizer mostrando compra atravessando os 6 serviços | Demo individual ao final do workshop |
| SC-4 | Custo total compartilhado do evento ~US$0 (gateway YARP per-aluno em Container App, sem APIM — ADE-004; identidade do cliente via Entra External ID/CIAM em **trial sem subscription/cartão** + admin no workforce do aluno — ADE-007; só sobra domínio opcional ~US$12/ano) | Azure Cost Management ao final do evento |
| SC-5 | Custo por aluno ≤ US$15 nos 40h | Budget Alert + script teardown |
| SC-6 | NPS > 8/10 entre alunos | Pesquisa pós-evento |

## Stories

| # | ID | Título | Branch destino | Estimativa | Executor primário | Quality gate |
|---|---|---|---|---|---|---|
| S1 | 2.1 | F1 — Service Bus + Functions | `phase-01-servicebus-functions` | 6h | @dev | @architect |
| S2 | 2.2 | F2 — Gateway YARP + policies em código | `phase-02-gateway` | 6h | @dev | @architect |
| S3 | 2.3 | F3 (Quartas) — Identidade dois-mundos: cliente CIAM + admin workforce + migração v1→CIAM (1 story única) | `phase-03-identity` | **~7,5–9,5h** (lab "longo" — sessão única, ver Risco #7) | @dev | @architect |
| S4 | 2.4 | F4 — n8n self-hosted em Container Apps | `phase-04-orchestration` | 6h | @devops | @architect |
| S5 | 2.5 | F5 — MCP server + chatbot + Gemini 2.0 Flash | `phase-05-ai-mcp` | 8h | @dev | @architect |
| S6 | 2.6 | F6 — Flow Visualizer (correlation ID animado) | `phase-06-flow-visualizer` | 8h | @dev | @architect |
| S7 | 2.7 | Materiais didáticos (transversal): READMEs + PORTAL-GUIDEs + SPEAKER-NOTES + slides + vídeos | paralelo a S1–S6 | 40h spread | @analyst | @pm |

**Total estimado:** 40h workshop + ~40h de produção de materiais (S7) executável em paralelo.

### Padrão de artefatos por story de fase (S1–S6)

Cada story de fase entrega **6 artefatos** obrigatórios:

| Artefato | Audiência | Quando usar |
|---|---|---|
| `README.md` | Aluno | Leitura prévia (semana anterior) |
| `PORTAL-GUIDE.md` | Aluno | Durante hands-on (segue ao vivo) |
| `SPEAKER-NOTES.md` | Facilitador | Antes (preparo) + durante (referência) |
| `slides.pdf` / Reveal.js | Apresentação | Durante a aula |
| `intro-video.mp4` (~5min) | Aluno | Antes da aula (assíncrono) |
| Branch + workflow CI/CD | Aluno | Hands-on + pós-aula |

> **Padrão pedagógico:** provisioning de recursos Azure **sempre via Portal passo-a-passo** (demo guiada, instrutor projeta, aluno replica). Bicep/IaC = apêndice opcional.

## Dependências entre stories

```
S1 (F1: Service Bus + Functions)
   └─> S2 (F2: Gateway YARP em código)
        └─> S3 (F3: cliente CIAM + admin workforce + migração v1→CIAM)
             └─> S4 (F4: n8n em Container Apps)
                  └─> S5 (F5: MCP + Chatbot + Gemini)
                       └─> S6 (F6: Flow Visualizer)
                            └─> merge → main (pós-workshop)

S7 (Materiais didáticos) ─── paralela a S1–S6 (cada fase entrega seus 5 docs)
```

**Princípio:** branches são linhas-do-tempo cumulativas, não features paralelas. Aluno faz fork de cada branch e segue a sequência.

## Artefatos pré-existentes que cada story alavanca

| Story | Artefatos que reusa |
|---|---|
| S1 (F1) | `fifa2026-api/src/` (referência de schema), `purchases` table (extensão com `source` + `correlation_id`), `.github/workflows/deploy-backend.yml` (pattern de CI/CD) |
| S2 (F2) | Function App de S1; projeto YARP versionado no repo (criado em S2, reusado/estendido em S3+ para validação de JWT) |
| S3 (F3) | `fifa2026-api/src/middleware/auth.js` (referência do JWT atual para comparação didática); gateway YARP de S2 (recebe `AddJwtBearer`) |
| S4 (F4) | Container Apps pattern (a ser criado em S4 e reusado se necessário); Service Bus de S1 (n8n consome) |
| S5 (F5) | Function pattern de S1 (MCP server é Function); SQL como fonte de dados das tools |
| S6 (F6) | App Insights (já presente desde S1); SignalR é novo; React + framer-motion já em `Lovable/.../package.json` |
| S7 | `docs/qa/APPLICATION-OVERVIEW.md` (contexto do produto); EPIC-001 PORTAL-VM-style como referência de formato |

## Riscos e mitigações

| # | Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|---|
| 1 | Custo Azure ultrapassa budget do evento | Baixa | Alto | Gateway YARP custo ~US$0 (sem APIM compartilhado — ADE-004) + Budget Alert + script teardown.ps1 ao final |
| 2 | Atrito de setup do Entra External ID / CIAM em F3 (reaberto pela ADE-007) | Média | Médio | **MITIGADO** (não eliminado): trial CIAM **sem subscription/cartão** + **instrutor pré-provisiona** tenant/user flow/Google IdP fora do relógio da aula; **email/OTP** como fallback de zero dependência. Resíduo: trial expira em 30d (recriar por turma via script/VS Code) e o social Google exige app no Google (instrutor pré-configura). A ADE-005 havia eliminado este risco indo p/ workforce; a ADE-007 reabre-o ao adotar o produto CIAM correto p/ cliente — premissa de atrito revista, mas não nula. |
| 3 | n8n exposto sem auth na free config | Média | Alto | Basic auth obrigatório no PORTAL-GUIDE de S4 |
| 4 | MCP é tecnologia recente — quebra de spec durante evento | Média | Médio | Pinning de versão do SDK MCP em todas as fases; `@architect` valida no design de S5 |
| 5 | Drift entre branches (hotfix em F1 após F2 criada) | Baixa | Médio | Congelar `main` pré-workshop; cherry-pick scriptado em bootstrap de cada fase |
| 6 | ~~Mapping de IDs Entra (GUID) ↔ IDs locais (int)~~ — **RESOLVIDO/FECHADO** (ADE-007 herda ADE-005, supersede ADE-001) | — | — | Decisão fechada: o claim `oid` (GUID estável) é a chave; coluna aditiva idempotente `entra_oid` em `purchases`/`users`. Sem tabela de mapping. Com a ADE-007 o `oid` passa a ser emitido pelo **tenant CIAM** — só muda a origem do GUID, não o schema. *Nota: a ADE-007 §"Artefatos a atualizar" pediu revisitar "Risco #6" sob a pressão de tempo da sessão única; nesta epic o risco de tempo é o **#7** (revisado abaixo) — este #6 permanece o risco de mapping, fechado.* |
| 7 | 40h causam fadiga (formato compacto) **+ F3 (Quartas) estoura o padrão ~6h/fase: ~7,5–9,5h em sessão única** | Alta | Médio→Alto | (a) Calendário geral: 4 finais-de-semana × 10h **OU** 5 dias úteis × 8h — `@pm` define. (b) **F3/Quartas — RISCO DE CRONOGRAMA REGISTRADO HONESTAMENTE:** Gateway YARP + cliente CIAM + admin workforce + migração v1→CIAM hands-on somam **~7,5–9,5h** (ADE-007 §Dimensionamento). O owner decidiu **sessão única completa** (2026-06-25), aceitando conscientemente o estouro para preservar todas as decisões hands-on e a continuidade narrativa — a recomendação da Aria de dividir A/B **não foi adotada**. Mitigação: o roteiro de F3 deve sinalizar a duração estendida (lab "longo") e prever **ponto de pausa natural ao fim do bloco cliente CIAM** caso a turma precise quebrar em 2 encontros na prática; warmup e pré-provisionamento agressivo do tenant CIAM (instrutor) para recuperar tempo. Plano B de contenção (se necessário em aula): rebaixar a migração de hands-on p/ demo guiada (~−1–1,5h) — mas isso reverte decisão do owner, então é último recurso. |
| 8 | Gemini cai durante aula F5 | Baixa | Alto | Fallback Groq + cache local pré-configurado; documentado no SPEAKER-NOTES de S5 |
| 9 | Cold start de Functions trava demo ao vivo | Média | Baixo | Warmup automático 5min antes de cada bloco hands-on |
| 10 | Alunos sem subscription Azure ativa | Média | Médio | Checklist pré-evento + bootcamp opcional de setup 2h antes |

## Compatibility Requirements

- [x] Existing APIs (Node/Express v1) remain unchanged — fluxo v2 é paralelo
- [x] Database schema changes are backward compatible — apenas 2 colunas novas idempotentes (`source`, `correlation_id`)
- [x] UI changes follow existing patterns — apenas botão "Comprar v2" e rota `/flow` adicionadas, usando shadcn/ui existente
- [x] Performance impact é zero no fluxo v1 (não há mudança) e medível no fluxo v2 (App Insights)

## Decisões fechadas (10/10) — herdadas do handoff de @analyst

| # | Decisão | Resolução |
|---|---|---|
| 1 | Hospedagem n8n | Azure Container Apps (Consumption) com basic auth |
| 2 | LLM padrão do chatbot | Gemini 2.0 Flash + MCP para portabilidade |
| 3 | Quantas fases | 6 (F1-F6) + merge final em main |
| 4 | Escopo da identidade v2 | **Re-escopo (ADE-007, supersede ADE-005):** cliente no **Entra External ID / CIAM** (`ciamlogin.com`, user flow, Google + OTP) + admin no **workforce** (App Roles, construído hands-on) + **migração `users` v1→CIAM** hands-on; gateway YARP valida o JWT do CIAM; v1 mantém bcrypt+JWT para comparação. *Evolução: a ADE-005 [06-03] removeu o External ID por atrito; a ADE-007 [06-25] o reintroduz p/ o cliente — premissa de atrito revista (trial sem cartão + pré-provisionamento).* |
| 5 | Tools do MCP | `consultar_disponibilidade`, `verificar_ingresso`, `consultar_bracket` |
| 6 | Flow Visualizer real-time | SignalR free tier + fallback polling 2s |
| 7 | Gateway + custo | **Re-escopo (ADE-004):** sem APIM. Gateway YARP em código (.NET) hospedado em Container App **por aluno** (Consumption, scale-to-zero ~US$0); custo compartilhado do gateway eliminado |
| 8 | Pré-requisitos do aluno | C# básico + Git + Azure free trial US$200 ativo |
| 9 | Audiência | Devs polyglot com background cloud (não exige .NET prévio) |
| 10 | Materiais por fase | 6 artefatos: README + PORTAL-GUIDE + SPEAKER-NOTES + slides + vídeo + branch |

## Decisões resolvidas pós-criação do epic (re-escopo 2026-06-03, revisado 2026-06-25)

| Decisão | Resolução | Ref |
|---|---|---|
| Mapping IDs Entra (GUID) ↔ IDs locais (int) | **RESOLVIDA** — claim `oid` é a chave; coluna aditiva `entra_oid` (sem tabela de mapping). Com ADE-007, o `oid` passa a vir do tenant CIAM — só muda a origem do GUID | ADE-007 herda ADE-005 (supersede ADE-001) |
| Gateway (APIM Developer vs código) | **RESOLVIDA** — YARP em código (.NET), por aluno, custo ~US$0 | ADE-004 |
| Identidade do CLIENTE (External ID vs tenant workforce) | **REVISADA 2026-06-25** — volta para **Entra External ID / CIAM** (cliente) + **workforce no admin** + migração v1→CIAM hands-on; premissa de atrito da ADE-005 revista (trial sem cartão + pré-provisionamento) | ADE-007 (supersede ADE-005) |
| Formato da F3/Quartas (dividir A/B vs sessão única) | **RESOLVIDA 2026-06-25 (owner)** — **sessão única completa** (não A/B), migração mantida hands-on; estouro ~7,5–9,5h aceito conscientemente → **1 story única** (ver Risco #7) | ADE-007 §Dimensionamento |

## Decisões pendentes (carry-forward para fases específicas)

| Decisão | Quando resolver | Responsável |
|---|---|---|
| Formato de calendário (4 finais-de-semana × 10h OU 5 dias × 8h) | Pré-evento | `@pm` |
| Custom domain para o gateway (opcional) | Pré-evento | `@devops` |
| Pinning de versão MCP SDK | S5 (F5) design | `@architect` |

## Stakeholders

- **Owner do evento:** Guilherme Prux Campos (guilherme.campos@tftec.com.br)
- **Audiência:** alunos da TFTEC (pós-graduação) + devs convidados — polyglot com background cloud
- **Squad:** @pm (Morgan) → @sm (River) → @po (Pax) → @dev (Dex) + @analyst (Atlas) para materiais + @architect (Aria) para decisões arquiteturais + @devops (Gage) para CI/CD
- **Co-design conduzido por:** @analyst (Atlas) em 2026-05-24

## Próximos passos (PM → SM)

1. ✅ Epic criado em `docs/epics/EPIC-002-living-lab-workshop.md`
2. ➡️ **@sm (River)** drafta as 7 stories em `docs/stories/2.{1..7}.story.md` usando `story-tmpl.yaml`
3. Cada story deve ter:
   - **Status:** Draft
   - **Story (As a / I want / So that):** redigida do ponto de vista do **aluno do workshop**
   - **Acceptance Criteria:** seguindo os 9 sub-itens do molde F1 do blueprint (Objetivos pedagógicos, Arquitetura, Endpoints, Schema delta, CI/CD esqueleto, Roteiro de aula, DoD aluno, Troubleshooting, Tempo por sub-tópico)
   - **Tasks/Subtasks:** passo-a-passo executável incluindo geração dos 6 artefatos
   - **Dev Notes:** referências ao blueprint + handoff + artefatos pré-existentes
   - **Validation:** smoke test específico do fluxo v2 daquela fase
4. **@sm** começa por **S2.1 (F1)** que já está detalhada nos 9 sub-itens no blueprint — é o "molde" para as demais
5. **@po (Pax)** valida cada story no checklist de 10 pontos antes de virar Ready
6. **@architect (Aria)** consultado em S2.5 (pinning MCP SDK) durante o draft — decisões de gateway (ADE-004) e identidade/mapping (ADE-007, supersede ADE-005) já fechadas
7. **@devops (Gage)** orquestra CI/CD por branch e deploy do Container App de gateway YARP por aluno (sem APIM compartilhado — ADE-004); identidade do cliente usa tenant CIAM em **trial** (pré-provisionado pelo instrutor, não compartilhado pago) + workforce do aluno p/ admin (ADE-007)
8. **@dev (Dex)** implementa o código de demonstração de cada fase quando story estiver Ready
9. **@analyst (Atlas)** colabora em S2.7 (materiais didáticos transversais) em paralelo

## Definition of Done (Epic-level)

- [ ] Todas as 7 stories Done (S1–S7) com QA gate PASS
- [ ] App rodando em `main` consolidado com fluxo v2 completo
- [ ] 6 branches preservadas (`phase-01-…` → `phase-06-…`) para futuros workshops
- [ ] 36 artefatos didáticos entregues (6 fases × 6 artefatos cada)
- [ ] Cost report final ~US$0 compartilhado (sem APIM — ADE-004; identidade do cliente via CIAM em trial sem cartão + admin no workforce — ADE-007) + ≤ US$15 por aluno
- [ ] Script `teardown.ps1` testado e disponível
- [ ] Pesquisa NPS pós-evento coletada

---

> **Source blueprint:** [docs/workshops/2026-blueprint-living-lab-azure.md](../workshops/2026-blueprint-living-lab-azure.md) — todo detalhe técnico de F1 (9 sub-itens) e esqueletos de F2–F6 estão lá.
>
> **Story Manager Handoff (para @sm):**
>
> *"Drafta as 7 stories desta epic. Cada story de fase (S2.1–S2.6) usa o molde de 9 sub-itens documentado em F1 do blueprint. S2.7 é transversal (materiais didáticos). Mantém compatibilidade com fluxo v1 (Node/Express + bcrypt+JWT permanecem intocados). Para cada story, verifica que ACs incluem a geração dos 6 artefatos obrigatórios (README + PORTAL-GUIDE + SPEAKER-NOTES + slides + vídeo + branch). Provisioning Azure é sempre Portal-first (Bicep como apêndice). Inicia por S2.1 (F1: Service Bus + Functions) que já está pré-detalhada no blueprint."*
