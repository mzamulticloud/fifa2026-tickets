# PORTAL GUIDE — F2: Container Registry + Container App do Gateway

> **Bloco 2 do roteiro (45min)** · Demo guiada: o instrutor projeta, você replica.
> **Objetivo:** sair daqui com **Container Registry + Container App do gateway** no ar, a **imagem do gateway** deployada, as **variáveis de ambiente** configuradas e um **smoke test** passando.
> **Story:** [2.2](../../stories/2.2.story.md) (AC-3, AC-10) · **Hosting:** [ADE-004](../../architecture/ade-004-gateway-yarp.md) (Invariante 2 e 5)

---

## Pré-requisitos

- Subscription Azure ativa (a mesma da F1)
- Login em **portal.azure.com**
- Suas **iniciais** definidas (ex.: `jds` para João da Silva) — usamos em todos os nomes
- **A Function App F1 funcionando** — você precisa da **URL pública dela** (ex.: `https://func-fifa2026-jds.azurewebsites.net`). O gateway encaminha para essa URL.
- O Resource Group da F1 (`rg-fifa2026-workshop-<iniciais>`) — reaproveitamos ele

> **Convenção de nomes ([ADE-004 Inv 5](../../architecture/ade-004-gateway-yarp.md)):** substitua `<iniciais>` pelas suas e `<rand>` por 3 dígitos. **Cada aluno tem o seu próprio gateway** — não há recurso compartilhado. O nome do Container Registry é **globalmente único** e só aceita letras/números (sem hífen).

| Recurso | Padrão de nome | Exemplo |
|---|---|---|
| Resource Group | `rg-fifa2026-workshop-<iniciais>` (reuso da F1) | `rg-fifa2026-workshop-jds` |
| Container Registry (ACR) | `acrfifa2026<iniciais><rand>` (só alfanumérico) | `acrfifa2026jds417` |
| Container App Environment | `cae-fifa2026-<iniciais>` | `cae-fifa2026-jds` |
| Container App (gateway) | `gateway-<iniciais>` | `gateway-jds` |

> **Região do workshop: East US 2.** Use sempre a mesma para todos os recursos.

> **Por que duas peças (Registry + App)?** O **Container Registry (ACR)** é o "depósito" onde a **imagem Docker** do seu gateway fica guardada. O **Container App** é quem **roda** essa imagem e expõe uma URL pública. Você publica a imagem no Registry, e o Container App a puxa de lá.

---

## Step 1 — Criar o Azure Container Registry (8min)

O ACR guarda a imagem Docker do gateway (gerada do `Dockerfile` em `src/Fifa2026.V2.Gateway/`).

1. No Portal, na busca do topo, digite **"Container registries"** e abra.
2. Clique **`+ Create`**.
3. **Subscription / Resource group:** os da F1 (`rg-fifa2026-workshop-<iniciais>`).
4. **Registry name:** `acrfifa2026<iniciais><rand>` (globalmente único, **só letras e números**).
5. **Location:** **East US 2**.
6. **Pricing plan:** **Basic** (suficiente para o workshop; mais barato).
7. Clique **`Review + create`** → **`Create`**. Aguarde (~1 min) → **`Go to resource`**.

> `[PRINT 1: formulário "Create container registry" com Registry name e Pricing=Basic]`
> `[PRINT 2: notificação verde + página de overview do ACR mostrando o Login server]`

8. Na página do ACR (**Overview**), anote o **Login server** (ex.: `acrfifa2026jds417.azurecr.io`). Você vai usá-lo.
9. No menu lateral, em **Settings → Access keys**, habilite **`Admin user`** (toggle). Anote **username** e **password** — o Container App vai usá-los para puxar a imagem.

> `[PRINT 3: Access keys com Admin user habilitado, username e password visíveis]`

✅ **Checkpoint:** ACR criado, **Login server** anotado, **Admin user** habilitado.

> ⚠️ **Armadilha:** nome do registry com hífen ou maiúscula → o Portal rejeita. ACR só aceita **minúsculas e números**, 5-50 caracteres.

---

## Step 2 — Publicar a imagem do gateway no ACR (7min)

A imagem é construída a partir do `Dockerfile` real do projeto (multi-stage, .NET 8, expõe a **porta 8080**). Em sala, fazemos isso pela linha de comando do **Azure Cloud Shell** (não precisa de Docker na sua máquina).

1. No topo do Portal, clique no ícone do **Cloud Shell** (`>_`). Escolha **Bash** se perguntar.
2. Garanta que você está no diretório do repositório (clone-o no Cloud Shell se necessário) e rode o build remoto no próprio ACR:

```bash
# az acr build builda a imagem NO Azure (não precisa de Docker local) e publica no ACR.
az acr build \
  --registry acrfifa2026<iniciais><rand> \
  --image gateway:v1 \
  src/Fifa2026.V2.Gateway
```

> O `Dockerfile` está em `src/Fifa2026.V2.Gateway/` — por isso passamos esse caminho como contexto de build. Ele faz `dotnet publish` em Release e gera uma imagem `aspnet:8.0` ouvindo na porta **8080**.

> `[PRINT 4: Cloud Shell rodando az acr build, com a saída "Build complete" + a tag gateway:v1]`

3. Confirme que a imagem está no registry:

```bash
az acr repository show-tags --name acrfifa2026<iniciais><rand> --repository gateway
# Esperado: [ "v1" ]
```

✅ **Checkpoint:** a imagem `gateway:v1` aparece no repositório `gateway` do ACR.

> **Nota:** em produção (e no CI/CD da fase — ver Bloco 5), quem builda e publica é o workflow `deploy-phase-02.yml`, que tagueia a imagem com o `github.sha`. Aqui no Portal usamos a tag `v1` manual só para subir o primeiro deploy à mão e entender as peças.

---

## Step 3 — Criar o Container App do gateway (12min)

O Container App roda a imagem e dá a ela uma URL pública.

1. Na busca do Portal, digite **"Container Apps"** e abra. Clique **`+ Create`**.
2. **Basics:**
   - **Subscription / Resource group:** os da F1.
   - **Container app name:** `gateway-<iniciais>`.
   - **Region:** **East US 2**.
   - **Container Apps Environment:** clique **`Create new`** → nome `cae-fifa2026-<iniciais>` → **Create**. (O *environment* é a "rede/limite" onde os Container Apps vivem.)
3. Aba **Container:**
   - **Desmarque** "Use quickstart image".
   - **Image source:** **Azure Container Registry**.
   - **Registry:** selecione `acrfifa2026<iniciais><rand>`.
   - **Image:** `gateway`.
   - **Image tag:** `v1`.
4. Aba **Ingress:**
   - **Ingress:** **Enabled**.
   - **Ingress traffic:** **Accepting traffic from anywhere** (queremos URL pública).
   - **Target port:** **`8080`** ⚠️ — **exatamente** a porta que o `Dockerfile` expõe (`EXPOSE 8080` / `ASPNETCORE_URLS=http://+:8080`). Errar aqui = 502.
5. Clique **`Review + create`** → **`Create`**. Aguarde (~2-3 min) → **`Go to resource`**.

> `[PRINT 5: aba Container com Image source=ACR, Image=gateway, Tag=v1]`
> `[PRINT 6: aba Ingress com Target port=8080 destacado]`
> `[PRINT 7: Container App provisionado, Overview mostrando a Application Url]`

6. Na **Overview**, anote a **Application Url** (ex.: `https://gateway-jds.<env>.eastus2.azurecontainerapps.io`). É a URL pública do seu gateway.

✅ **Checkpoint:** Container App no ar, com **Application Url** e **Target port 8080**.

> ⚠️ **Armadilha nº1 da F2: Target port errado.** Se você deixar a porta padrão (80) em vez de **8080**, o ingress aponta para a porta errada e toda chamada retorna **502 Bad Gateway**. Confirme `8080`.

---

## Step 4 — Configurar as variáveis de ambiente (8min)

O gateway lê configuração de variáveis de ambiente (App Settings). **Nada é hardcoded** ([ADE-003 Inv 3](../../architecture/ade-003-v2-infrastructure-baseline.md)). A mais importante é a URL da Function F1 — sem ela, o gateway não sabe para onde encaminhar.

1. Na página do Container App, no menu lateral, em **Application → Containers** (ou **Settings → Containers** dependendo do tema), clique em **Edit and deploy**.
2. Selecione o container `gateway` → aba **Environment variables** → adicione:

| Nome da variável | Valor | O que faz |
|---|---|---|
| `FunctionAppF1Url` | `https://func-fifa2026-<iniciais>.azurewebsites.net` | **URL real da sua Function F1.** O gateway sobrescreve a destination do cluster com ela ([ADE-003 Inv 3](../../architecture/ade-003-v2-infrastructure-baseline.md)). **Obrigatória.** |
| `Gateway__FrontendOrigin` | `https://fifa2026-web.azurewebsites.net` | Origem permitida no CORS (AC-7). Use `__` (dois underscores) — é como o .NET mapeia seções aninhadas. |
| `Jwt__TenantId` | `common` (placeholder F2) | Tenant do issuer Entra para o JWT. Em F2 é só placeholder; F3 troca pelo tenant real. |

> `[PRINT 8: aba Environment variables com FunctionAppF1Url preenchida]`

3. Clique **`Save`** → **`Create`** (gera uma nova revisão do Container App com as variáveis).

> **Por que `Gateway__FrontendOrigin` e não `Gateway:FrontendOrigin`?** No `appsettings.json` a chave é `Gateway:FrontendOrigin`. Em variável de ambiente, o .NET lê o **duplo underscore `__`** como o separador de seção `:`. Mesma coisa para `Jwt__TenantId` → `Jwt:TenantId`.

> ⚠️ **Armadilha:** `FunctionAppF1Url` ausente ou errada → o gateway encaminha para `localhost:7071` (o default do `appsettings.json`, que não existe em produção) → **502**. Esta variável é a causa nº1 de 502 na F2.

> 🔒 **O que NÃO vai aqui:** a connection string do SQL **não** entra no gateway. Ela permanece nas Functions (a Function fala com o SQL, o gateway só fala com a Function). Segredo fica onde é usado.

✅ **Checkpoint:** `FunctionAppF1Url` configurada e uma nova revisão ativa.

---

## Step 5 — Smoke test ponta-a-ponta (6min)

Agora validamos que o gateway encaminha, reescreve o caminho e injeta o correlation ID. Use o Cloud Shell ou seu terminal local.

```bash
# Substitua pela Application Url do SEU Container App (Step 3).
GATEWAY=https://gateway-<iniciais>.<env>.eastus2.azurecontainerapps.io

# 1) Health check (endpoint /health do gateway)
curl -s $GATEWAY/health
# Esperado: {"status":"healthy","service":"gateway-yarp"}

# 2) POST /purchase no gateway → reescrito para /api/v2/purchase na Function F1.
#    -i mostra os headers da resposta (queremos ver X-Correlation-ID e X-Cache).
curl -i -X POST $GATEWAY/purchase \
  -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
```

O que você deve ver na resposta do POST:

- Status **`202 Accepted`** (vindo da Function F1, devolvido pelo gateway)
- Corpo `{ "correlationId": "...", "status": "queued" }`
- Header **`X-Correlation-ID: <guid>`** (o gateway injetou e devolveu — AC-8)
- Header **`X-Cache: MISS`** (POST não é cacheado)

> `[PRINT 9: terminal com a resposta 202 + headers X-Correlation-ID e X-Cache: MISS]`

✅ **Checkpoint:** `correlationId` na resposta + header `X-Correlation-ID` presente. **A URL real da Function nunca apareceu** para o cliente — você chamou só o gateway.

> **Nota:** o fluxo completo (mensagem na fila → consumer grava no SQL) é o mesmo da F1 e já acontece por trás. O que a F2 prova aqui é que o **gateway está na frente**, reescrevendo e injetando, sem o cliente ver a Function.

---

## Validation final do Bloco 2

Antes de avançar para o live coding do gateway (Bloco 3), confirme:

- [ ] ✅ Container Registry `acrfifa2026<iniciais><rand>` (Basic) com **Admin user** habilitado
- [ ] ✅ Imagem `gateway:v1` publicada no ACR (`az acr repository show-tags`)
- [ ] ✅ Container App `gateway-<iniciais>` no ar com **Target port 8080** e Application Url pública
- [ ] ✅ `FunctionAppF1Url` apontando para a sua Function F1 (+ `Gateway__FrontendOrigin`, `Jwt__TenantId`)
- [ ] ✅ Smoke test: `POST /purchase` → 202 com `correlationId` + header `X-Correlation-ID`
- [ ] ✅ `/health` retorna `{"status":"healthy","service":"gateway-yarp"}`

> Guarde a Application Url do gateway. Você a usará nos labs de rate-limit (429) e cache (X-Cache: HIT) nos blocos seguintes.

---

## Apêndice — Mapa rápido de 502 / troubleshooting

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| **502 Bad Gateway** em toda chamada | `Target port` ≠ 8080 no ingress | Confirmar **8080** (o `Dockerfile` expõe 8080) |
| **502** só nas rotas `/purchase` | `FunctionAppF1Url` ausente/errada (cai no `localhost:7071` default) | Conferir a App Setting; deve ser a URL pública real da Function F1 |
| **CORS bloqueado** no navegador | `Gateway__FrontendOrigin` diferente do domínio do front | Ajustar a variável (com `__`) para o domínio exato do frontend |
| Container App não puxa a imagem | Admin user do ACR desabilitado / credenciais erradas | Habilitar Admin user no ACR; reconfigurar o registry no Container App |
| 1ª chamada lenta (segundos) | Cold start do Consumption (scale-to-zero) | Esperado no workshop; em prod, "min replicas: 1" |
| `/purchase` chega como `/purchase` no backend (404) | path rewrite não aplicado | Confirmar a imagem certa (`PathSet`/`PathPattern` estão no `appsettings.json` do projeto) |

> **Em produção (e no CI/CD da fase):** a criação do ACR/Container App e a provisão de secrets/variáveis são responsabilidade do **@devops** (por aluno). Em sala, você faz à mão para entender cada peça; o `deploy-phase-02.yml` automatiza isso (Bloco 5).
