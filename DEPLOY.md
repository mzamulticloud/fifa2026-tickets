# FIFA 2026 Tickets — Topologia & Deploy


Aplicação dividida em **3 camadas**, com a mesma codebase rodando tanto em **VMs** quanto em **Azure Web App for Windows**. Em ambos os cenários:

- O **frontend é o único componente público** (porta 80/443).
- O **backend é privado** — só responde para o frontend.
- O **banco é privado** — só responde para o backend.
- A comunicação **frontend → backend** acontece via **reverse proxy** do IIS (regra em `web.config`), então o cliente do browser chama sempre `/api/*` na origem do frontend. CORS não é exercitado em produção.

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
   Internet ──▶ │  fifa2026-web (Web App Windows)  │
                │  conteúdo: dist/                 │
                │  web.config: rewrite /api/* ─────┼───┐
                └──────────────────────────────────┘   │
                                                       ▼
                                ┌──────────────────────────────────┐
                                │  fifa2026-back (Web App Windows) │
                                │  conteúdo: fifa2026-api/         │
                                │  Access Restrictions: somente    │
                                │    outbound IPs do front         │
                                │  mssql ─────────────────────────┼───┐
                                └──────────────────────────────────┘   │
                                                                       ▼
                                                          ┌──────────────────────┐
                                                          │  Azure SQL Database  │
                                                          │  Private Endpoint    │
                                                          │  ou Firewall Rule    │
                                                          │  com IPs do back     │
                                                          └──────────────────────┘
```

### Recursos
- **App Service Plan**: Windows, B1 ou superior (S1 para produção real).
- **Web App `fifa2026-web`**: hosting do build estático + `web.config`.
- **Web App `fifa2026-back`**: Node 18+ runtime, deploy de `fifa2026-api/`.
- **Azure SQL Database**: importar o `.bacpac`. Opcional: Private Endpoint para isolamento total.

### Tornando o backend privado (sem VNet integration)
A forma mais simples no plano básico:

1. Configure **Access Restrictions** em `fifa2026-back`:
   - Permitir: outbound IPs de `fifa2026-web` (lista em Networking → Outbound IP addresses).
   - Negar: o resto (regra default Deny).
2. Habilite **HTTPS Only** em ambos.

Com VNet Integration (mais robusto):
1. Crie uma VNet com 2 subnets dedicadas a App Service VNet integration.
2. Habilite **VNet Integration** nos 2 Web Apps.
3. Em `fifa2026-back`, ative **Private Endpoint** ou Access Restriction baseada em VNet.
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

**`fifa2026-web`** não precisa de App Settings — o `BACKEND_URL` já foi gravado no `web.config` no momento do build.

### Build do frontend para Web App
```bash
cd "Lovable/World Cup Tickets Hub"
BACKEND_URL=https://fifa2026-back.azurewebsites.net npm run build
# Subir dist/ via ZipDeploy, GitHub Actions ou Azure CLI
```

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
| Frontend → Backend | web.config rewrite → `http://<IP-priv>:3001` | web.config rewrite → `https://fifa2026-back.azurewebsites.net` | Vite proxy → `http://localhost:3001` |
| BD | SQL Server na VM-DB | Azure SQL Database | SQL local ou Azure SQL |
| Origem do schema/dados | bacpac | bacpac | bacpac ou schema.sql + seed |
| Build do frontend | `BACKEND_URL=http://<IP>:3001 npm run build` | `BACKEND_URL=https://...azurewebsites.net npm run build` | não há build (Vite dev) |
