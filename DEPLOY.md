# FIFA 2026 Tickets — Topologia & Deploy

Aplicação dividida em **3 camadas**, com a mesma codebase rodando tanto em **VMs** quanto em **Azure Web App for Windows**. O modelo de rede **difere entre os dois cenários** — atenção:

| | Cenário A (VMs) | Cenário B (Azure Web App B1) |
|---|---|---|
| Frontend → Backend | **reverse proxy IIS/ARR** (`web.config` rewrite `/api/*` → IP privado do backend) | **chamada direta do browser** (`VITE_API_URL` absoluto embutido no bundle) |
| Backend acessível por | só pela VM-Front (NSG/IP) — **privado** | **público** na internet (ver nota abaixo) |
| CORS | não exercitado (proxy same-origin) | **exercitado** — backend libera só `FRONTEND_URL` |
| Banco | privado (NSG) | Azure SQL com firewall `AllowAllAzureServices` |

> ⚠️ **Por que o backend é público no Cenário B:** no App Service **B1**, o reverse proxy outbound do IIS/ARR **não funciona** — uma regra `web.config` que reescreve `/api/*` para `https://<backend>.azurewebsites.net` retorna **404** (testado). Por isso o frontend embute `VITE_API_URL` (URL absoluta do backend) no bundle JS e o **browser chama o backend diretamente**. Como as chamadas partem do IP do usuário final, **não é possível** travar o backend por allowlist de IPs do frontend (isso devolveria 403 ao usuário). A segurança fica por conta de **CORS** (`FRONTEND_URL`) + **JWT**. Para privacidade real de rede, use **Private Endpoint + VNet Integration** (exige SKU Standard+; não cabe no B1 simples).

---

## Cenário A — 3 Máquinas Virtuais

```
                ┌──────────────────────┐
   Internet ──▶ │  VM-Front (pública)  │
                │  IIS :80             │
                │  fifa2026-web/       │
                │  rewrite /api/* ─────┼───┐
                └──────────────────────┘   │
                                           ▼
                                ┌──────────────────────┐
                                │  VM-Back (privada)   │
                                │  IIS+iisnode :3001   │
                                │  fifa2026-api/       │
                                │  mssql ──────────────┼───┐
                                └──────────────────────┘   │
                                                           ▼
                                                ┌──────────────────────┐
                                                │  VM-DB (privada)     │
                                                │  SQL Server :1433    │
                                                │  FIFA2026Tickets     │
                                                └──────────────────────┘
```

### Rede
- **VNet única** (ex.: `vnet-fifa2026`)
- **3 subnets** (uma por VM) ou subnets compartilhadas — qualquer divisão funciona, o que importa são as NSGs.
- **NSG por subnet:**

| NSG | Inbound permitido | Inbound negado |
|---|---|---|
| nsg-front | TCP 80, 443 (Internet); TCP 3389 (seu IP) | Resto |
| nsg-back  | TCP 3001 (origem: subnet/IP da VM-Front); TCP 3389 (seu IP) | Internet |
| nsg-db    | TCP 1433 (origem: subnet/IP da VM-Back); TCP 3389 (seu IP) | Internet, VM-Front |

> **VM-Back NÃO recebe IP público.** Acesso administrativo via Bastion ou jump host.

### Variáveis de ambiente

**VM-Back — `fifa2026-api/.env`:**
```env
DB_SERVER=<IP privado da VM-DB>
DB_PORT=1433
DB_USER=fifa2026_db
DB_PASSWORD=<senha>
DB_NAME=FIFA2026Tickets
PORT=3001
HOST=0.0.0.0
JWT_SECRET=<string longa>
JWT_EXPIRES_IN=7d
FRONTEND_URL=http://<ip-ou-dns-da-VM-Front>
```

**VM-Front — build do `fifa2026-web/`:**
```bash
cd "Lovable/World Cup Tickets Hub"
BACKEND_URL=http://<IP privado VM-Back>:3001 npm run build
# Copiar dist/* para C:\inetpub\wwwroot\fifa2026-web\ na VM-Front
```

### Passo-a-passo IIS
Reaproveite os passos do `Lovable/World Cup Tickets Hub/DEPLOY_IIS_SIMPLIFICADO.md` com **uma diferença chave**: em vez de instalar front+back na mesma VM, instale o backend na VM-Back e o frontend na VM-Front. O `web.config` do frontend já vai apontar para o IP privado correto se você buildou com `BACKEND_URL=...`.

---

## Cenário B — Azure Web App for Windows

```
                ┌──────────────────────────────────┐
   Internet ──▶ │  app-...-web (Web App Windows)   │  conteúdo estático: dist/
                │  serve index.html + bundle JS    │
                └──────────────────────────────────┘
                         │ (browser baixa o bundle; depois chama o backend DIRETO)
   Browser ──────────────┴──────────────▶ ┌──────────────────────────────────┐
   (VITE_API_URL absoluto, CORS)          │  app-...-api (Web App Windows)   │  público
                                          │  conteúdo: fifa2026-api/         │  CORS: só FRONTEND_URL
                                          │  iisnode → src/index.js          │  JWT nas rotas protegidas
                                          │  mssql ─────────────────────────┼───┐
                                          └──────────────────────────────────┘   │
                                                                                 ▼
                                                          ┌──────────────────────┐
                                                          │  Azure SQL Database  │
                                                          │  firewall:           │
                                                          │  AllowAllAzureServices│
                                                          └──────────────────────┘
```

> Diferente do Cenário A: **não há proxy `/api` no frontend** (ARR não funciona no B1). O browser chama o backend pela URL absoluta (`VITE_API_URL`). Logo o backend é **público** e protegido por CORS + JWT.

### Recursos
- **App Service Plan**: Windows, B1 ou superior (S1 para produção real).
- **Web App `fifa2026-web`**: hosting do build estático + `web.config`.
- **Web App `fifa2026-back`**: Node 18+ runtime, deploy de `fifa2026-api/`.
- **Azure SQL Database**: importar o `.bacpac`. Opcional: Private Endpoint para isolamento total.

### Segurança do backend (B1)

> ⚠️ **NÃO** aplique Access Restriction com allowlist dos outbound IPs do frontend no Cenário B. Como o **browser** chama o backend diretamente (`VITE_API_URL`), as requisições partem do IP do **usuário final** — uma allowlist baseada nos IPs do frontend devolveria **403** ao usuário e quebraria o app. (O `infra/provision.*` e versões antigas deste doc faziam isso; é incompatível com o B1.)

No B1 a proteção do backend é em **camada de aplicação**, não de rede:
1. **HTTPS Only** habilitado em ambos os Web Apps (já no Bicep).
2. **CORS** no backend liberando apenas `FRONTEND_URL` (App Setting).
3. **JWT** nas rotas protegidas.

**Privacidade de rede real (opcional, exige Standard+):** com VNet Integration:
1. Crie uma VNet com subnets dedicadas a App Service VNet Integration.
2. Habilite **VNet Integration** nos 2 Web Apps e mantenha o frontend como proxy server-side (que no Standard+ funciona).
3. No backend, ative **Private Endpoint** / Access Restriction baseada em VNet.
4. SQL Database recebe Private Endpoint na mesma VNet.

### App Settings (substitui o .env)

**`fifa2026-back` → Configuration → Application settings:**
| Nome | Valor |
|---|---|
| DB_SERVER | `<server>.database.windows.net` |
| DB_PORT | `1433` |
| DB_USER | `<sql-user>` |
| DB_PASSWORD | `<senha>` |
| DB_NAME | `FIFA2026Tickets` |
| JWT_SECRET | `<string longa>` |
| JWT_EXPIRES_IN | `7d` |
| FRONTEND_URL | `https://fifa2026-web.azurewebsites.net` |
| WEBSITE_NODE_DEFAULT_VERSION | `~18` |

> `PORT` e `HOST` são gerenciados pela plataforma (iisnode injeta named pipe).

**`app-...-web` (frontend)** não precisa de App Settings — a URL do backend é embutida **em build time** no bundle JS via `VITE_API_URL`. No B1, é isso que o browser usa (chamada direta); o `BACKEND_URL`/web.config só importa no Cenário A (VMs).

### Build do frontend para Web App
```bash
cd "Lovable/World Cup Tickets Hub"
# VITE_API_URL = URL ABSOLUTA do backend + /api → embutida no bundle (browser chama direto)
# BACKEND_URL  = mantido por compatibilidade (web.config; só efetivo em VM/Standard+)
VITE_API_URL=https://app-fifa2026-api-dev-brs-001.azurewebsites.net/api \
BACKEND_URL=https://app-fifa2026-api-dev-brs-001.azurewebsites.net \
  npm run build
# Subir dist/ via ZipDeploy, GitHub Actions ou Azure CLI
```

> O workflow `deploy-frontend.yml` já faz isso automaticamente: deriva `VITE_API_URL` de `BACKEND_URL` (input do workflow ou GitHub Variable) e valida que a URL foi embutida no bundle.

---

## Cenário C — Dev local

Backend (terminal 1):
```bash
cd FIFA2026-APP/fifa2026-api
cp .env.example .env  # ajuste DB_SERVER apontando para SQL local ou Azure SQL
npm install
npm run dev           # nodemon em :3001
```

Frontend (terminal 2):
```bash
cd "FIFA2026-APP/Lovable/World Cup Tickets Hub"
cp .env.example .env
npm install
npm run dev           # vite em :8080, com proxy /api -> :3001
```

Acessar: http://localhost:8080

---

## Banco de dados — referência única

Fonte da verdade: **`FIFA2026-APP/FIFA2026Tickets.bacpac`**.

- Para popular um SQL Server / Azure SQL **com dados reais**: importar o bacpac.
- Para criar do zero **sem dados** (apenas schema): rodar `fifa2026-api/database/schema.sql` + `seed-admin.sql`.
- Detalhes em `fifa2026-api/database/README.md`.

---

## Resumo do que muda entre cenários

| Item | VM (3 VMs) | Web App Windows | Dev local |
|---|---|---|---|
| Frontend hospedado em | IIS na VM-Front | App Service `fifa2026-web` | Vite (`npm run dev`) :8080 |
| Backend hospedado em | iisnode na VM-Back (privada) | App Service `fifa2026-back` (Access Restriction) | Node `npm run dev` :3001 |
| Frontend → Backend | web.config rewrite → `http://<IP-priv>:3001` (proxy IIS) | **browser → backend direto** via `VITE_API_URL` (proxy ARR não funciona no B1) | Vite proxy → `http://localhost:3001` |
| Backend exposto | privado (NSG, só VM-Front) | **público** (CORS `FRONTEND_URL` + JWT) | localhost |
| BD | SQL Server na VM-DB | Azure SQL Database | SQL local ou Azure SQL |
| Origem do schema/dados | bacpac | bacpac | bacpac ou schema.sql + seed |
| Build do frontend | `BACKEND_URL=http://<IP>:3001 npm run build` | `BACKEND_URL=https://...azurewebsites.net npm run build` | não há build (Vite dev) |
