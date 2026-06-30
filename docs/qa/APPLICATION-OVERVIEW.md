# FIFA 2026 Tickets — Application Overview (QA Reference Document)

> **Owner:** @qa (Quinn) · **Created:** 2026-05-23 · **Status:** Canonical
> **Scope:** Documentação técnica completa da aplicação, consolidada a partir de leitura direta de código, infra, epics, stories e gates. Serve como referência única para onboarding, QA review, e auditoria.
> **Disclaimer:** Aplicação educacional fictícia. Sem vínculo com a FIFA ou entidades oficiais. Uso restrito ao evento TFTEC "Copa do Mundo Azure".

---

## Índice

1. [Visão de Produto](#1-visão-de-produto)
2. [Arquitetura de Alto Nível](#2-arquitetura-de-alto-nível)
3. [Estrutura do Repositório](#3-estrutura-do-repositório)
4. [Backend — fifa2026-api](#4-backend--fifa2026-api)
5. [Frontend — Lovable/World Cup Tickets Hub](#5-frontend--lovableworld-cup-tickets-hub)
6. [Banco de Dados](#6-banco-de-dados)
7. [Infraestrutura como Código (Bicep)](#7-infraestrutura-como-código-bicep)
8. [CI/CD (GitHub Actions)](#8-cicd-github-actions)
9. [Topologias de Deploy](#9-topologias-de-deploy)
10. [Fluxos End-to-End](#10-fluxos-end-to-end)
11. [Segurança & Configuração](#11-segurança--configuração)
12. [Epics, Stories e Gates](#12-epics-stories-e-gates)
13. [Riscos Conhecidos & Mitigações](#13-riscos-conhecidos--mitigações)
14. [Smoke Tests & Critérios de Aceitação](#14-smoke-tests--critérios-de-aceitação)
15. [Apêndice: Referências](#15-apêndice-referências)

---

## 1. Visão de Produto

### 1.1 O que é

Plataforma fictícia de **venda de ingressos da Copa do Mundo FIFA 2026** desenvolvida como **sistema-piloto educacional** para o evento TFTEC "Copa do Mundo Azure". A app entrega o ciclo completo de uma bilheteria 3-camadas: catálogo, compra, ingresso premium com QR code, painel administrativo e conteúdo de fixação (quiz, bracket, histórico).

### 1.2 Para quem

| Persona | Necessidade |
|---|---|
| **Aluno TFTEC** (pós-graduação) | Aprender Azure (PaaS e VMs) construindo do zero, com app real, em ~1h30 (PaaS) ou ~3h (VMs) |
| **Instrutor TFTEC** | Demonstrar trade-offs VM vs PaaS, isolamento de rede, CI/CD, migração de dados |
| **Stakeholder TFTEC** | Validar didática, custo, robustez do material |

### 1.3 Por que existe

- Ensina a arquitetura web **mais comum do mercado**: SPA estática + API REST + banco relacional.
- Mostra padrões reais: **reverse proxy** via `web.config` (sem CORS em prod), **isolamento back/DB**, **CI/CD com publish profile**, **migração de dados via `.bacpac`**.
- A **jornada de modernização** (VM → PaaS) é o produto pedagógico; a app é o veículo.

### 1.4 Domínio funcional

- **104 partidas** (12 grupos A-L × 6 + 32 de mata-mata).
- **16 estádios oficiais** FIFA 2026 (USA + México + Canadá).
- **48 seleções** classificadas (estado pós-Final Draw de 2025-12-05).
- **3 categorias de ingresso** por jogo: VIP, Cat1, Cat2 (preços reais FIFA aplicados via migration `real-fifa-prices.sql`).
- **Ingresso premium** com QR code (jspdf + qrcode.react) + página `/ticket/verify/:id` para validação.
- **Bracket de mata-mata interativo** com cascade automática de vencedores (`GET /api/bracket`).

---

## 2. Arquitetura de Alto Nível

### 2.1 Componentes

```
┌────────────────────────────────────────────────────────────────────┐
│                      🌎 Torcedor (browser)                         │
└──────────────────────────────┬─────────────────────────────────────┘
                               │ HTTPS
                               ▼
┌────────────────────────────────────────────────────────────────────┐
│  🌐 fifa2026-web  (App Service Windows · público)                  │
│  ─ Build estático Vite (dist/) + web.config rewrite                │
│  ─ /api/* → reverse proxy ──────────────────────────────────────┐  │
└────────────────────────────────────────────────────────────────┼──┘
                                                                 │
                                                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│  🔒 fifa2026-back  (App Service Windows · privado via ACL)         │
│  ─ Node 18 + Express 4 + iisnode                                   │
│  ─ Helmet · CORS · Morgan · Rate Limit · Express Validator         │
│  ─ JWT (HS256, 24h/7d) · bcryptjs (10 rounds)                      │
│  ─ Pool mssql ──────────────────────────────────────────────────┐  │
└────────────────────────────────────────────────────────────────┼──┘
                                                                 │ 1433
                                                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│  🟥 Azure SQL Database (FIFA2026Tickets)                           │
│  ─ 6 tabelas (users, teams, stadiums, matches, ticket_categories,  │
│    purchases) · FKs cascade · índices                              │
│  ─ Source-of-truth: FIFA2026Tickets.bacpac                         │
└────────────────────────────────────────────────────────────────────┘
```

### 2.2 Decisões arquiteturais-chave

| ADR informal | Decisão | Razão |
|---|---|---|
| Reverse proxy via IIS | `/api/*` reescrito no `web.config` do front | Cliente nunca vê o back; sem CORS em prod; backend privado |
| Backend privado | Access Restrictions allow só IPs outbound do front | Defesa em profundidade; minimiza superfície |
| JWT em localStorage | Frontend guarda `auth_token` em `localStorage` | Stateless; bearer em `Authorization` |
| Carrinho client-side | `CartContext` persiste só em `localStorage` | Sem complexidade de carrinho sincronizado; demo educacional |
| 1 App Service Plan, 2 Web Apps | Compartilham B1 (~$13/mês) | Custo mínimo para alunos |
| Bacpac como SoT | `FIFA2026Tickets.bacpac` na raiz; migrations só para evoluções pós-bacpac | Aluno consegue subir banco em 1 comando |

---

## 3. Estrutura do Repositório

```
fifa2026-tickets-dev/
├── fifa2026-api/                     # Backend Node.js/Express
│   ├── src/
│   │   ├── index.js                  # Pipeline Express
│   │   ├── config/database.js        # Pool mssql
│   │   ├── middleware/auth.js        # JWT auth + admin guard
│   │   └── routes/
│   │       ├── auth.js               # register/login/me
│   │       ├── matches.js            # CRUD + tickets/match
│   │       ├── teams.js              # GET teams + groups
│   │       ├── stadiums.js           # CRUD stadiums
│   │       ├── tickets.js            # purchase + my-tickets
│   │       ├── users.js              # profile + admin list
│   │       ├── admin.js              # sales + stats
│   │       ├── standings.js          # tabela de grupos
│   │       └── bracket.js            # mata-mata dinâmico
│   ├── database/
│   │   ├── schema.sql                # 6 tabelas canônicas
│   │   ├── seed-admin.sql            # admin@fifa2026.com (bcrypt admin123)
│   │   ├── migrations/               # 10 migrations 2026-05-07/08
│   │   └── legacy/                   # SQL antigo (referência)
│   ├── package.json                  # Node 18+, deps prod
│   └── web.config                    # iisnode handler
├── fifa2026-web/                     # Build deployado do frontend (artefato)
│   ├── index.html
│   ├── assets/                       # JS/CSS hashed
│   └── web.config                    # IIS rewrite (api proxy + SPA fallback)
├── Lovable/World Cup Tickets Hub/    # Source do frontend (Vite + React + TS)
│   ├── src/
│   │   ├── App.tsx                   # Routes + Providers (Query/Auth/Cart)
│   │   ├── contexts/{AuthContext,CartContext}.tsx
│   │   ├── lib/api.ts                # ApiClient (fetch wrapper)
│   │   ├── pages/                    # 20+ pages (público + admin)
│   │   ├── components/               # shadcn/ui + custom
│   │   └── data/                     # matches/stadiums/teams (fallback estático)
│   ├── public/web.config             # template com __BACKEND_URL__
│   ├── scripts/set-backend-url.mjs   # pós-build: troca placeholder
│   └── package.json                  # React 18, Vite 5, Tailwind 3
├── infra/
│   ├── main.bicep                    # Orquestração resource group
│   ├── modules/
│   │   ├── app-service-plan.bicep
│   │   ├── web-app-frontend.bicep
│   │   ├── web-app-backend.bicep
│   │   └── sql-database.bicep
│   ├── parameters/dev.bicepparam
│   ├── provision.ps1 / provision.sh
│   └── README.md
├── .github/workflows/
│   ├── deploy-frontend.yml
│   ├── deploy-backend.yml
│   └── README.md
├── docs/
│   ├── epics/{EPIC-000,EPIC-001}.md
│   ├── stories/{0.1..0.11, 1.1..1.5}.story.md
│   ├── qa/{po-validations + gates/}
│   ├── audits/2026-05-07-content-audit.md
│   ├── GUIA-EVENTO.md                # Cenário PaaS (~1h30)
│   ├── GUIA-EVENTO-VMS.md            # Cenário 3 VMs (~3h)
│   └── PACOTE-ALUNOS.md              # Geração de ZIPs pré-compilados
├── FIFA2026Tickets.bacpac            # Source-of-truth do banco
├── DEPLOY.md                         # Topologias 3-VM / Web App / Dev local
├── README.md
└── AGENTS.md                         # Atalhos AIOX
```

---

## 4. Backend — fifa2026-api

### 4.1 Dependências (`package.json`)

| Categoria | Pacote | Versão | Finalidade |
|---|---|---|---|
| Web | `express` | ^4.18.2 | Framework REST |
| Web | `cors` | ^2.8.5 | CORS allowlist por origem |
| Web | `helmet` | ^7.1.0 | Headers de segurança |
| Web | `morgan` | ^1.10.0 | HTTP logs (combined) |
| Web | `express-rate-limit` | ^8.5.1 | 100 req/15min global, 5 req/15min em `/auth/login` |
| Web | `express-validator` | ^7.0.1 | Validação body/query/params |
| Data | `mssql` | ^10.0.1 | Driver SQL Server (pool nativo) |
| Auth | `bcryptjs` | ^2.4.3 | Hash de senha (10 rounds) |
| Auth | `jsonwebtoken` | ^9.0.2 | JWT stateless |
| Config | `dotenv` | ^16.3.1 | `.env` em dev |
| Dev | `nodemon` | ^3.0.2 | Auto-restart |

**Scripts:** `npm start` (`node src/index.js`) · `npm run dev` (`nodemon src/index.js`).

### 4.2 Pipeline Express (`src/index.js`)

Ordem real de execução:
1. `app.disable('x-powered-by')`
2. `app.set('trust proxy', true)` — necessário para `X-Forwarded-For` sob iisnode/Azure
3. `helmet()`
4. `morgan('combined')`
5. `cors({ origin: callback, credentials: true })` — allowlist via `FRONTEND_URL` (CSV)
6. `express.json()`
7. `app.use('/api/auth/login', loginLimiter)` — 5 req/15min, `skipSuccessfulRequests: true`
8. `app.use('/api', generalLimiter)` — 100 req/15min
9. Rotas montadas em `/api/*`
10. Error handler global → `{ error: 'Erro interno do servidor' }` (500)

**Porta:** `process.env.PORT || 3001` (em iisnode mapeada para named pipe).

### 4.3 Healthchecks

| Endpoint | Auth | Resposta |
|---|---|---|
| `GET /api/health` | — | `{ status: 'ok', timestamp }` |
| `GET /api/health/db` | — | `{ status, database: 'connected', sample: {id, name}, config: {server, database, user} }` em sucesso |

### 4.4 Configuração do pool MSSQL (`src/config/database.js`)

```javascript
{
  server: process.env.DB_SERVER,
  port: process.env.DB_PORT || 1433,
  user: process.env.DB_USER,
  password: process.env.DB_PASSWORD,
  database: process.env.DB_NAME,
  options: {
    encrypt: true,                 // Força TLS
    trustServerCertificate: true,  // Aceita cert auto-assinado em dev
    enableArithAbort: true
  },
  pool: { max: 10, min: 0, idleTimeoutMillis: 30000 }
}
```

Exports: `{ getConnection, query, sql }`. Lazy connect; pool reusado entre requests.

### 4.5 Middlewares de autenticação (`src/middleware/auth.js`)

| Middleware | Comportamento |
|---|---|
| `authMiddleware` | Header `Authorization: Bearer <token>` obrigatório; `jwt.verify(token, JWT_SECRET)`; popula `req.user = { id, email, role }`; 401 em falha |
| `adminMiddleware` | Exige `req.user.role === 'admin'`; 403 caso contrário |

Tokens são gerados em `/auth/register` e `/auth/login` com payload `{ id, email, role }` e `expiresIn: JWT_EXPIRES_IN` (`24h` dev, `7d` prod).

### 4.6 Inventário de endpoints

#### `/api/auth` (`routes/auth.js`)

| Verbo | Path | Auth | Validação | Comportamento |
|---|---|---|---|---|
| POST | `/register` | — | name+email válido+password>=6 | INSERT user (role default `user`), retorna token |
| POST | `/login` | — | email+password | SELECT + `bcrypt.compare`; retorna user+token |
| GET | `/me` | ✅ | — | SELECT user por `req.user.id` |

#### `/api/matches` (`routes/matches.js`)

| Verbo | Path | Auth | Notas |
|---|---|---|---|
| GET | `/` | — | Filtros: `stage`, `stadium_id`, `team_id`; JOIN teams+stadiums |
| GET | `/:id` | — | Match individual |
| GET | `/:id/tickets` | — | Lista `ticket_categories` com `available_quantity > 0` |
| POST | `/` | ✅ admin | INSERT match |
| PUT | `/:id` | ✅ admin | UPDATE match (inclui `home_score`, `away_score`, `status`) |
| DELETE | `/:id` | ✅ admin | DELETE match |

#### `/api/teams` (`routes/teams.js`)

| Verbo | Path | Auth | Notas |
|---|---|---|---|
| GET | `/` | — | Filtros: `confederation`, `group_name`; ORDER BY `fifa_ranking` |
| GET | `/groups` | — | Agrupado por `group_name` com STRING_AGG dos times |
| GET | `/:id` | — | Time individual |
| GET | `/:id/matches` | — | Todos os jogos do time (home ou away) |

#### `/api/stadiums` (`routes/stadiums.js`)

| Verbo | Path | Auth |
|---|---|---|
| GET | `/` (filtro `country`) | — |
| GET | `/:id` | — |
| GET | `/:id/matches` | — |
| POST `/` · PUT `/:id` · DELETE `/:id` | | ✅ admin |

Campos: `name, city, country, capacity, image, description, inauguration_year, latitude (Decimal 10,8), longitude (Decimal 11,8)`.

#### `/api/tickets` (`routes/tickets.js`)

| Verbo | Path | Auth | Comportamento |
|---|---|---|---|
| POST | `/purchase` | ✅ | Body `{items: [{ticket_category_id, quantity}]}`. **Transação:** valida `available_quantity >= quantity` para cada item; `UPDATE ticket_categories SET available_quantity -= quantity`; `INSERT INTO purchases (status='completed')`. Rollback completo em qualquer falha. |
| GET | `/my-tickets` | ✅ | Histórico de compras do usuário |

#### `/api/users` (`routes/users.js`)

| Verbo | Path | Auth | Notas |
|---|---|---|---|
| GET | `/profile` | ✅ | `req.user` |
| PUT | `/profile` | ✅ | `{ name }` |
| PUT | `/password` | ✅ | `{ currentPassword, newPassword }` (>=6); bcrypt compare + rehash |
| GET | `/` | ✅ admin | Paginação `page/pageSize` (max 200) + `search` (LIKE name/email) + `role` |

#### `/api/admin` (`routes/admin.js`) — todas exigem admin

| Verbo | Path | Filtros |
|---|---|---|
| GET | `/sales` | `page, pageSize, status, search, start_date, end_date` (BETWEEN) |
| GET | `/sales/:id` | — |
| GET | `/stats` | KPIs: total_users, total_sales, total_revenue, total_tickets_sold, total_matches, total_stadiums |

#### `/api/standings` (`routes/standings.js`)

`GET /` calcula classificação por grupo a partir de `matches WHERE status='finished'` com placar completo. Pontuação **V=3, E=1, D=0**. Ordenação: pontos DESC → saldo DESC → gols pró DESC → nome ASC (locale pt-BR).

#### `/api/bracket` (`routes/bracket.js`)

`GET /` calcula bracket dinâmico:
- **R32 (matches 73–88):** top-2 dos 12 grupos + 8 melhores 3ºs (matching bipartite FIFA).
- **R16 (89–96)** · **QF (97–100)** · **SF (101–102)** · **3º lugar (103)** · **Final (104)**.
- Cascade: vencedores são **UPDATE em `matches.home_team_id/away_team_id`** onde estavam NULL.

### 4.7 Padrões de erro

| Status | Cenário |
|---|---|
| 200 / 201 | Sucesso |
| 400 | Validação falhou, quantidade insuficiente, campo obrigatório ausente |
| 401 | Token ausente/inválido/expirado, credenciais incorretas |
| 403 | Autenticado mas sem `role=admin` |
| 404 | Recurso inexistente |
| 500 | Erro não tratado (logado em `console.error`; resposta genérica) |

### 4.8 Hospedagem (`web.config`)

Handler `iisnode` redireciona todas as requests para `src/index.js`. `node_env=production`, `loggingEnabled=true` em `logs/`, `watchedFiles=*.js;node_modules\*;routes\*.js`.

---

## 5. Frontend — Lovable/World Cup Tickets Hub

### 5.1 Stack

| Categoria | Pacote | Versão |
|---|---|---|
| Build | Vite | 5.4.19 (`@vitejs/plugin-react-swc`) |
| UI | React | 18.3.1 |
| TS | TypeScript | 5.8.3 |
| Router | react-router-dom | 6.30.1 (lazy loading) |
| State | @tanstack/react-query | 5.83.0 (staleTime 5min, gcTime 30min) |
| Form | react-hook-form 7.61.1 + zod 3.25.76 | |
| UI Kit | shadcn/ui + Radix (50+ componentes) | |
| Styling | Tailwind 3.4.17 + tailwindcss-animate | |
| Theme | next-themes 0.3.0 (dark mode via classe) | |
| PDF/QR | jspdf 3.0.4 + html2canvas 1.4.1 + qrcode.react 4.2.0 | |
| Toast | sonner 1.7.4 | |
| Chart | recharts 2.15.4 | |

### 5.2 vite.config.ts

```typescript
server: {
  host: '::',           // IPv6 + IPv4
  port: 8080,
  proxy: {
    '/api': {
      target: process.env.VITE_DEV_BACKEND_URL || 'http://localhost:3001',
      changeOrigin: true,
      secure: false
    }
  }
},
resolve: { alias: { '@': './src' } },
plugins: [react(), componentTagger() /* dev only */]
```

### 5.3 Setup de providers (`App.tsx`)

```
QueryClientProvider (refetchOnWindowFocus: false)
└── TooltipProvider
    └── AuthProvider
        └── CartProvider
            └── BrowserRouter
                └── Routes (lazy + <PageLoader>)
```

### 5.4 AuthContext

- **Storage:** `localStorage['auth_token']` (Bearer) e `localStorage['copa2026_user']` (cache do user).
- **Bootstrap:** carrega cache, valida via `GET /auth/me`; se inválido → logout.
- **Métodos:** `login`, `register`, `logout`, `updateProfile`, `addOrder` (local).
- **Role-based:** `user.role === 'admin'` → libera AdminLayout.

### 5.5 CartContext

- State: `items: CartItem[]` (`match + sector + ticketCategoryId + quantity + unitPrice`).
- Dedup: mesma `match+ticketCategoryId` somam quantidade.
- Computed: `totalItems`, `totalPrice`.
- Persistência: `localStorage` (não sincroniza com backend).

### 5.6 API Client (`src/lib/api.ts`)

- Wrapper sobre `fetch`; headers padrão `Content-Type: application/json`; Bearer token se houver.
- Base URL: `VITE_API_URL` em prod; em dev usa proxy `/api`.
- Cobre todos os endpoints do backend.

### 5.7 Rotas

**Públicas (em `Layout`):**

| Path | Componente | Função |
|---|---|---|
| `/` | Index | Hero + jogos em destaque |
| `/matches` | Matches | Lista geral, filtros |
| `/matches/:id` | MatchDetail | Setores/preços, add-to-cart |
| `/stadiums` · `/stadiums/:id` | Stadiums · StadiumDetail | Catálogo de estádios |
| `/teams` · `/teams/:id` | Teams · TeamDetail | 48 seleções |
| `/groups` · `/standings` | Groups · Standings | Tabela classificatória |
| `/quiz` · `/qualified` · `/historia` · `/historia/:year` | Quiz + Historia | Conteúdo educacional |
| `/cart` · `/checkout` · `/payment-confirmation` | Cart · Checkout · PaymentConfirmation | Fluxo de compra |
| `/ticket/verify/:id` | TicketVerify | Validação por QR code |
| `/login` · `/register` · `/profile` | Auth | Sessão e perfil |
| `*` | NotFound | 404 |

**Admin (em `AdminLayout`, protegido):**

| Path | Componente |
|---|---|
| `/admin` | Dashboard (KPIs + recharts) |
| `/admin/matches` | CRUD matches |
| `/admin/stadiums` | CRUD stadiums |
| `/admin/users` | Lista de users (paginada) |
| `/admin/sales` | Relatório de vendas |

### 5.8 Design tokens (Tailwind)

CSS variables: `--primary`, `--secondary`, `--destructive`, `--success`, `--muted`, `--accent`, `--card`, `--gold[-light/-dark]`, `--stadium-green`, `--fifa-blue`, `--sidebar-*`. Fontes: `sans: Inter`, `display: Bebas Neue`. Dark mode via `.dark` class. Animations custom: `fade-in`, `fade-in-up`, `scale-in`, `pulse-border`, `scroll-left/right`.

### 5.9 web.config do frontend

```xml
<rule name="API Proxy" stopProcessing="true">
  <match url="^api/(.*)" />
  <action type="Rewrite" url="__BACKEND_URL__/api/{R:1}" />
</rule>
<rule name="React Routes" stopProcessing="true">
  <match url=".*" />
  <conditions>
    <add input="{REQUEST_FILENAME}" matchType="IsFile"      negate="true" />
    <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
  </conditions>
  <action type="Rewrite" url="/index.html" />
</rule>
```

`scripts/set-backend-url.mjs` roda no `build` e substitui `__BACKEND_URL__` pelo valor de `BACKEND_URL` (env). Permite o **mesmo build** servir qualquer aluno — basta editar uma linha do `web.config`.

---

## 6. Banco de Dados

### 6.1 Schema canônico (`fifa2026-api/database/schema.sql`)

| Tabela | Colunas Principais | FKs | Índices |
|---|---|---|---|
| **users** | id PK, name, email UQ, password, role (def `user`), phone, document, created_at, updated_at | — | `idx_users_email` |
| **teams** | id PK, name, code UQ, flag, group_name, confederation, fifa_ranking, created_at | — | — |
| **stadiums** | id PK, name, city, country, capacity, image, description, address, latitude (Dec 10,8), longitude (Dec 11,8), created_at | — | — |
| **matches** | id PK, home_team_id FK→teams, away_team_id FK→teams, stadium_id FK→stadiums, date, time, stage, group_name, home_score, away_score, status (def `scheduled`), created_at | 3 FKs | `idx_matches_date`, `idx_matches_stadium` |
| **ticket_categories** | id PK, match_id FK→matches CASCADE, category, price (Dec 10,2), total_quantity, available_quantity, description, created_at | 1 FK | `idx_ticket_categories_match` |
| **purchases** | id PK, user_id FK→users, ticket_category_id FK→ticket_categories, quantity, unit_price, total_price, status (def `pending`), payment_method, transaction_id, created_at, updated_at | 2 FKs | `idx_purchases_user` |

### 6.2 Admin seedado (`seed-admin.sql`)

- **Email:** `admin@fifa2026.com`
- **Senha:** `admin123` (hash bcrypt 10 rounds)
- **Role:** `admin`
- Idempotente: skip se já existe.

### 6.3 Migrations (aplicadas após o bacpac canônico)

| Data | Arquivo | Propósito |
|---|---|---|
| 2026-05-07 | `knockout-matches.sql` | Insere 32 placeholders R32→Final |
| 2026-05-07 | `update-16-stadiums.sql` | Atualiza 16 estádios |
| 2026-05-07 | `update-48-teams.sql` | Atualiza 48 seleções |
| 2026-05-08 | `fix-encoding-and-images.sql` | Corrige Unicode + URLs de imagens |
| 2026-05-08 | `group-stage-72.sql` | 72 jogos de grupos (A–L × 6) |
| 2026-05-08 | `knockout-stadiums-allocation.sql` | Aloca estádios no mata-mata |
| 2026-05-08 | `perf-indexes.sql` | Índices de performance |
| 2026-05-08 | `real-fifa-prices.sql` | Preços oficiais FIFA |
| 2026-05-08 | `seed-100k.sql` | 100k usuários + 500k compras simuladas (teste de escala admin) |
| 2026-05-08 | `stadium-coords-year.sql` | Latitude/longitude + ano de inauguração |

> **Atenção QA (do EPIC-001):** o `FIFA2026Tickets.bacpac` versionado é o estado de **2026-05-07** (pré-migrations 05-08). Para reproduzir o app live (104 jogos + preços FIFA + seed 100k), aplicar bacpac **+** migrations 05-08. O bacpac é regenerado **manualmente pelo owner** quando necessário (decisão registrada em `docs/qa/2026-05-15-po-validation-EPIC-001.md`).

### 6.4 Origem dos dados

Fonte de verdade declarada (README): **`FIFA2026Tickets.bacpac`** na raiz. Restauro via SqlPackage (`SqlPackage /Action:Import ...`) ou Azure CLI (`az sql db import`). Alternativa "schema só": rodar `schema.sql + seed-admin.sql`.

---

## 7. Infraestrutura como Código (Bicep)

### 7.1 `infra/main.bicep`

Scope: `resourceGroup`. Parâmetros principais:

| Parâmetro | Default | Notas |
|---|---|---|
| `namingPrefix` | `fifa2026` | Prefixo de todos os recursos |
| `location` | `eastus` (dev usa `eastus2`) | — |
| `appServicePlanSku` | `B1` | B1/B2/S1/S2/P1v3 |
| `sqlDatabaseSku` | `Basic` | Basic/S0/S1/S2 |
| `sqlAdminLogin` | `fifa2026admin` | — |
| `sqlAdminPassword` | secure | `SQL_ADMIN_PASSWORD` |
| `sqlDatabaseName` | `FIFA2026Tickets` | — |
| `jwtSecret` | secure | `JWT_SECRET` |
| `nodeVersion` | `~18` | — |

**Outputs:** `frontendUrl`, `backendUrl`, `sqlServerFqdn`, `frontendOutboundIps` (usados na configuração pós-deploy de Access Restrictions).

### 7.2 Módulos (`infra/modules/`)

| Módulo | Recurso | Destaques |
|---|---|---|
| `app-service-plan.bicep` | `Microsoft.Web/serverfarms` | Windows (`reserved: false`); SKU parametrizado |
| `web-app-frontend.bicep` | `Microsoft.Web/sites` | `httpsOnly: true`, `ftpsState: Disabled`, `minTlsVersion: 1.2`, `http20Enabled: true`, `defaultDocuments: [index.html]` |
| `web-app-backend.bicep` | `Microsoft.Web/sites` | App Settings (DB_*, JWT_*, FRONTEND_URL, WEBSITE_NODE_DEFAULT_VERSION); `ipSecurityRestrictionsDefaultAction: Allow` inicial (pós-deploy: switch para Deny + allowlist) |
| `sql-database.bicep` | `Microsoft.Sql/servers` + `databases` | `minimalTlsVersion: 1.2`, firewall rule `AllowAzureServices` (0.0.0.0–0.0.0.0) |

### 7.3 `parameters/dev.bicepparam`

Lê secrets de env: `readEnvironmentVariable('SQL_ADMIN_PASSWORD', 'TROQUE_OU_VIA_ENV')` e `readEnvironmentVariable('JWT_SECRET', 'TROQUE_OU_VIA_ENV')`.

### 7.4 Scripts (`provision.ps1` / `provision.sh`)

- Cria RG.
- Faz `az deployment group create` apontando para `main.bicep`.
- Importa bacpac (Storage temporário + `az sql db import`).
- Configura allowlist de Access Restrictions com IPs outbound do front.

---

## 8. CI/CD (GitHub Actions)

### 8.1 `.github/workflows/deploy-backend.yml`

| Field | Value |
|---|---|
| Triggers | `workflow_dispatch` (input `app_name`), `push main` em `fifa2026-api/**` |
| Node | 18 (cache `fifa2026-api/package-lock.json`) |
| Build | `npm ci --omit=dev`; remove `logs/`, `.env` |
| Deploy | `azure/webapps-deploy@v3` com secret `AZURE_BACKEND_PUBLISH_PROFILE` |

### 8.2 `.github/workflows/deploy-frontend.yml`

| Field | Value |
|---|---|
| Triggers | `workflow_dispatch` (inputs `backend_url`, `app_name`), `push main` em `Lovable/**` |
| Node | 20 |
| Env build | `BACKEND_URL` (input/var/default `https://fifa2026-back.azurewebsites.net`), `VITE_API_URL` |
| Build | `cd Lovable && npm ci && npm run build` (executa `set-backend-url.mjs`) |
| Verify | `grep -L "__BACKEND_URL__" dist/web.config` (placeholder substituído) |
| Deploy | `azure/webapps-deploy@v3` com secret `AZURE_FRONTEND_PUBLISH_PROFILE` |

---

## 9. Topologias de Deploy

### 9.1 Cenário A — 3 VMs (escopo do EPIC-001 + `GUIA-EVENTO-VMS.md`)

```
Internet → VM-Front (Windows Server, IP público, IIS+ARR :80/443)
            │ rewrite /api/*
            ▼
          VM-Back (privada, IIS+iisnode :3001)
            │ mssql :1433
            ▼
          VM-DB (privada, SQL Server 2022 Developer)
```

- **VNet única** `vnet-fifa2026` (3 subnets ou compartilhadas).
- **NSGs:** front 80/443 público + 3389 do seu IP; back 3001 da subnet front + 3389 do seu IP; db 1433 da subnet back.
- VM-Back **sem IP público**; acesso via Bastion / jump host.
- VMs B2s ~$30/mês cada se 24/7; `az vm deallocate` reduz a ~$5/mês por disco.

### 9.2 Cenário B — Azure Web Apps (produção atual, `GUIA-EVENTO.md`)

- App Service Plan Windows B1 (~$13/mês).
- 2 Web Apps (`fifa2026-web`, `fifa2026-back`).
- Azure SQL Basic (~$5/mês).
- **Backend privado** via Access Restrictions (allow outbound IPs do front; default Deny).
- HTTPS Only em ambos; TLS 1.2 mínimo.
- **Custo total: ~$18/mês.**

### 9.3 Cenário C — Dev local

- Backend `cd fifa2026-api && npm run dev` em `:3001`.
- Frontend `cd "Lovable/World Cup Tickets Hub" && npm run dev` em `:8080` com proxy `/api → :3001`.
- Banco: SQL local ou Azure SQL (configurável via `.env`).

### 9.4 Pacote pré-compilado para alunos (`PACOTE-ALUNOS.md`)

Professor gera 2 ZIPs no Blob `stotfteccopaazure/copa2026`:
- `fifa2026-api.zip` — código + `node_modules --omit=dev` (JS puro, portável Windows).
- `fifa2026-web.zip` — `dist/` + `web.config` com placeholder.

Aluno **não compila nada**; só:
1. Importa bacpac na VM-DB.
2. Edita `.env` na VM-Back.
3. Troca `__BACKEND_URL__` por uma linha de PowerShell na VM-Front.

---

## 10. Fluxos End-to-End

### 10.1 Autenticação

```
Browser → POST /auth/login {email, password}
         → backend SELECT users WHERE email + bcrypt.compare
         → jwt.sign({id, email, role}, JWT_SECRET, {expiresIn})
         ← {user, token}
Browser localStorage['auth_token'] = token
       localStorage['copa2026_user'] = user
Subsequente: Authorization: Bearer <token> em todas as chamadas autenticadas
```

### 10.2 Compra de ingresso

```
1. Browser GET /matches → lista
2. GET /matches/:id/tickets → categorias com available_quantity > 0
3. CartContext.addItem (client-side, deduplica por match+category)
4. /checkout → POST /tickets/purchase {items: [{ticket_category_id, quantity}]}
5. Backend transação:
   - Para cada item: SELECT available_quantity FROM ticket_categories
   - Se insuficiente → ROLLBACK + 400
   - UPDATE ticket_categories SET available_quantity -= quantity
   - INSERT INTO purchases (status='completed')
   - COMMIT
6. Resposta {message, total_amount, tickets: [...]}
7. Browser /payment-confirmation → gera PDF com QR code (jspdf + qrcode.react)
8. Validação posterior: /ticket/verify/:id (página pública por ID de purchase)
```

### 10.3 Bracket dinâmico

```
GET /api/bracket
  1. Calcula standings (V=3, E=1, D=0) por grupo via matches finished
  2. Top-2 de cada grupo → 24 slots
  3. Top-8 de terceiros lugares (best-of-12)
  4. Matching bipartite FIFA → preenche home_team_id/away_team_id de matches 73-88 (R32)
  5. Cascade: vencedores R32 → R16 → QF → SF → 3º/Final
  6. UPDATE matches SET home_team_id=..., away_team_id=... onde NULL
  ← {bracket: {round_of_32, round_of_16, quarter_final, semi_final, third_place, final}}
```

### 10.4 Admin: vendas paginadas

```
GET /api/admin/sales?page=1&pageSize=15&status=completed&search=joao&start_date=...&end_date=...
  → JOIN purchases × users × ticket_categories × matches × teams × stadiums
  → COUNT(*) + SELECT ... OFFSET (page-1)*pageSize ROWS FETCH NEXT pageSize
  ← {sales: [...], pagination: {page, pageSize, total, totalPages}}
```

---

## 11. Segurança & Configuração

### 11.1 Variáveis de ambiente

**Backend (`fifa2026-api/.env` em dev / App Settings em Azure):**

| Variável | Exemplo | Crítica |
|---|---|---|
| `PORT` | `3001` (ignorado em iisnode) | Não |
| `HOST` | `0.0.0.0` | Não |
| `DB_SERVER` | `fifa2026-sql.database.windows.net` | ✅ |
| `DB_PORT` | `1433` | — |
| `DB_USER` | `sqladmin` | ✅ |
| `DB_PASSWORD` | senha forte | **🔴 nunca versionar** |
| `DB_NAME` | `FIFA2026Tickets` | — |
| `DB_ENCRYPT` | `true` | — |
| `JWT_SECRET` | aleatória 32 bytes (`openssl rand -hex 32`) | **🔴 nunca versionar** |
| `JWT_EXPIRES_IN` | `24h` (dev) ou `7d` (prod) | — |
| `FRONTEND_URL` | `http://localhost:8080,https://fifa2026-web.azurewebsites.net` | — |
| `NODE_ENV` | `production` (controlado por iisnode em Azure) | — |

**Frontend (build-time):**
- `BACKEND_URL` — gravado no `web.config` (placeholder `__BACKEND_URL__`).
- `VITE_API_URL` — opcional; em dev usa proxy `/api`.

### 11.2 Defesas implementadas

| Camada | Mecanismo |
|---|---|
| Transport | HTTPS Only nos Web Apps; TLS 1.2 mínimo |
| Headers | `helmet()` (HSTS, X-Frame-Options, X-Content-Type, etc.); `X-Powered-By` removido |
| Auth | JWT (HS256, expira) + bcryptjs (10 rounds); admin guard separado |
| Rate Limit | 100 req/15min global; 5 req/15min em `/auth/login` (skipSuccessfulRequests) |
| Validation | `express-validator` em login/register/purchase |
| CORS | Allowlist por env `FRONTEND_URL`; credentials true |
| Network | Backend privado (Access Restrictions ou NSG); DB privado |
| SQL | Parameterizado via `sql.Int`, `sql.VarChar`, etc. (driver mssql) |
| FTP | `ftpsState: Disabled` |

### 11.3 Gaps conhecidos (declarados nos guias)

- Segredos em **App Settings** (não em Key Vault) — apontado como evolução em `GUIA-EVENTO.md`.
- Sem Managed Identity (Web App usa SQL login/password) — apontado como evolução.
- Sem Application Insights / Log Analytics — apontado como `out of scope` do EPIC-001.
- VM scenario: `.env` em arquivo na VM (frágil) — apontado em `GUIA-EVENTO-VMS.md §7`.
- bacpac canônico desatualizado vs prod (migrations 05-08 não embutidas) — regen manual pelo owner.

---

## 12. Epics, Stories e Gates

### 12.1 EPIC-000 — App Adjustment for TFTEC Event

- **Status:** ✅ **Done** (validação visual aprovada 2026-05-07).
- **Live:** https://fifa2026-web.azurewebsites.net
- **QA Gate:** `docs/qa/gates/2026-05-07-EPIC-000-qa-gate.md` — PASS.
- **Stories fechadas:**

| Story | Título | Status |
|---|---|---|
| 0.1 | Deploy inicial em Azure PaaS | ✅ Done |
| 0.2 | Remover refs Lovable | ✅ Done |
| 0.3 | Footer com disclaimer TFTEC | ✅ Done |
| 0.4 | Admin Dashboard com dados reais | ✅ Done |
| 0.5 | Polish TD-3 + TD-5 + TD-6 | ✅ Done (CONCERNS no gate) |
| 0.6 | 48 seleções classificadas | ✅ Done |
| 0.7 | Tabela interativa | ✅ Done |
| 0.8 | Bracket de mata-mata | ✅ Done |
| 0.9 | 16 estádios oficiais | ✅ Done |
| 0.10 | Consolidação lote 05-08 | ✅ Done (gate 10/10 PASS) |
| 0.11 | Polish adicional | ✅ Done |

### 12.2 EPIC-001 — VM-to-WebApp Modernization

- **Status:** 🟢 **Active** (ativado 2026-05-15 após go-no-go).
- **Pré-requisito atendido:** EPIC-000 done + 0.10 PASS.
- **Pré-dry-run owner-owned:** regeneração do bacpac (FORA do escopo de agente). Runbook em `docs/qa/2026-05-15-po-validation-EPIC-001.md`.
- **Stories:**

| Story | Título | Estimativa |
|---|---|---|
| 1.1 | Deploy inicial em 3 VMs | 45 min |
| 1.2 | Migrar Backend → Azure Web App | 30 min |
| 1.3 | Migrar Frontend → Azure Web App | 30 min |
| 1.4 | Migrar SQL Server (VM) → Azure SQL | 45 min |
| 1.5 | Guia de evento VM (workshop) | ✅ Done (PR #4) |

**Validações PO disponíveis:**
- `docs/qa/2026-05-07-po-validation-EPIC-000.md`
- `docs/qa/2026-05-15-po-validation-EPIC-001.md`
- `docs/qa/2026-05-20-po-validation-1.5.md`
- `docs/qa/2026-05-20-po-validation-EPIC-001-go.md` (9/10 GO)

**Gates:**
- `docs/qa/gates/2026-05-07-EPIC-000-qa-gate.md` — PASS
- `docs/qa/gates/2026-05-15-story-0.10-qa-gate.md` — PASS (10/10)
- `docs/qa/gates/1.5.gate.yaml` — gate 1.5

---

## 13. Riscos Conhecidos & Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Aluno trava em S1 (IIS/iisnode/ARR) | Alta | Bloqueia o evento | VM pré-configurada via ARM/golden image; pacote pré-compilado (`PACOTE-ALUNOS.md`) |
| Cota Azure do aluno insuficiente | Média | Aluno sem subscription | Pre-flight na Fase 0; subscription compartilhada de backup |
| Bacpac falha de importar (firewall) | Média | Trava S4 | Pré-passos: `AllowAllAzureServices` ANTES do import; Storage temp |
| Tempo estourar (~3h) | Alta | Stories incompletas | Cada story é independente; instrutor decide o quanto fazer |
| `__BACKEND_URL__` não substituído no build | Média | Front quebra `/api/*` | Workflow `deploy-frontend.yml` faz `grep -L` para validar |
| bacpac canônico defasado vs migrations 05-08 | Alta | Aluno tem app sem 104 jogos | Owner regenera bacpac antes do evento (runbook em PO validation) |
| Token JWT em `localStorage` | Baixa-Média | XSS poderia roubar token | `helmet` + Content-Type guard; demo educacional, não prod hardened |
| Senha do SQL em App Settings | Média | Vazamento se RBAC frouxo | Evolução prevista: Key Vault + Managed Identity (out of scope) |
| Carrinho client-side perde se localStorage limpa | Baixa | UX | Aceitável para escopo educacional |
| Concurrency em `available_quantity` | Média | Oversell se 2 compras simultâneas | Transação atual usa `SELECT + UPDATE`; risco de race se sem isolation level adequado — **monitorar em QA** |

---

## 14. Smoke Tests & Critérios de Aceitação

### 14.1 Smoke E2E (pós-deploy)

| # | Passo | Esperado |
|---|---|---|
| 1 | `GET https://fifa2026-web.azurewebsites.net/` | 200, HTML inicial |
| 2 | `GET https://fifa2026-web.azurewebsites.net/api/health` (reverse proxy) | 200 `{status: 'ok'}` |
| 3 | `GET https://fifa2026-back.azurewebsites.net/api/health` direto | 200 se ACL permitir o IP de origem; 403 caso contrário (desejável em prod) |
| 4 | `POST /api/auth/login` com `admin@fifa2026.com/admin123` | 200 com token; role `admin` |
| 5 | `GET /api/admin/stats` (Bearer admin) | 200 com KPIs |
| 6 | `GET /api/matches` | 200 com 104 jogos (ou número conforme dataset) |
| 7 | `POST /api/tickets/purchase` (Bearer user) com item válido | 201 + PDF gerável no front |
| 8 | `GET /api/bracket` | 200 com R32→Final |

### 14.2 Critérios por epic

**EPIC-000:**
- SC-1: App acessível publicamente em `https://fifa2026-web.azurewebsites.net` (smoke).
- SC-2: Backend não responde direto da Internet (Access Restriction validada).
- SC-3: Banco com dados reais (`/api/admin/stats`).
- SC-4: Ajustes de conteúdo aprovados pelo owner.

**EPIC-001:**
- SC-1: Aluno executa 100% das 4 stories em ~3h (cronometrar dry-run).
- SC-2: App funcional ao fim de cada story (smoke: login + listar jogos + comprar).
- SC-3: Aluno entende diferença entre os estados (pergunta de fixação).
- SC-4: Custo < $30 na subscription do aluno durante o evento.

### 14.3 Quality gates (do AGENTS.md)

Antes de marcar story como Done:
- `npm run lint`
- `npm run typecheck`
- `npm test`
- Atualizar checklist e File List da story.

---

## 15. Apêndice: Referências

### 15.1 Documentos primários

| Documento | Localização | Conteúdo |
|---|---|---|
| README | `README.md` | Quick start, stack, status |
| Deploy | `DEPLOY.md` | 3 topologias (VM/PaaS/dev) |
| Guia PaaS | `docs/GUIA-EVENTO.md` (486 linhas) | Workshop Azure Web Apps (~1h30) |
| Guia VMs | `docs/GUIA-EVENTO-VMS.md` (729 linhas) | Workshop 3 VMs (~3h) |
| Pacote alunos | `docs/PACOTE-ALUNOS.md` (166 linhas) | Geração de ZIPs pré-compilados |
| AGENTS | `AGENTS.md` | Atalhos AIOX (Codex CLI) |
| Constitution | `.aiox-core/constitution.md` | Princípios inegociáveis |
| Epics | `docs/epics/EPIC-{000,001}.md` | Objetivos e stories |
| Stories | `docs/stories/{0.1..0.11, 1.1..1.5}.story.md` | Detalhes implementação |
| Audit | `docs/audits/2026-05-07-content-audit.md` | Achados de conteúdo |

### 15.2 Live endpoints (privados ao evento)

| Recurso | URL |
|---|---|
| Frontend | `https://fifa2026-web.azurewebsites.net` |
| Backend | `https://fifa2026-back.azurewebsites.net` |
| SQL | `fifa2026-sql.database.windows.net` |
| Pacote backend | `https://stotfteccopaazure.blob.core.windows.net/copa2026/fifa2026-api.zip` |
| Pacote frontend | `https://stotfteccopaazure.blob.core.windows.net/copa2026/fifa2026-web.zip` |

### 15.3 Credenciais de demo

| Usuário | Senha | Role |
|---|---|---|
| `admin@fifa2026.com` | `admin123` | admin |

> **Aviso QA:** essas credenciais são fictícias e públicas no `seed-admin.sql`. **Trocar antes** de qualquer ambiente que extrapole o evento didático.

### 15.4 Convenções de versionamento e commits

- Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, etc.
- Referenciar story: `feat: implement bracket logic [Story 0.8]`.
- Commits atômicos.

---

**Documento mantido por:** @qa (Quinn) · **Próxima revisão:** após fechamento das stories 1.1–1.4 do EPIC-001 · **Origem:** leitura direta do código e docs em 2026-05-23.
