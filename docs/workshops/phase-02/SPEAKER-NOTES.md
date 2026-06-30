# SPEAKER NOTES — F2: Gateway Profissional em Código com YARP

> **Notas do facilitador** · 6 blocos · 6h (360min) · Workshop "Living Lab Azure-Native"
> **Use junto com:** [`slides.md`](./slides.md) (Bloco 1), [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Bloco 2), código em `src/Fifa2026.V2.Gateway/` (Blocos 3-4).
> **Story:** [2.2](../../stories/2.2.story.md) · **Decisão:** [ADE-004](../../architecture/ade-004-gateway-yarp.md)

---

## Visão geral do dia (cole no flip chart)

| # | Bloco | Tempo | Modo | Marco do aluno |
|---|---|---|---|---|
| 1 | Conceitos: 6 níveis do gateway, reverse proxy/BFF, construir vs comprar | 50min | Expositivo + Q&A | Sabe o que um gateway faz e por que YARP em vez de APIM |
| 2 | Provisioning Container Registry + Container App via Portal | 45min | Demo guiada (PORTAL-GUIDE) | Gateway no ar, imagem deployada, smoke test OK |
| 3 | Projeto YARP: routing + CORS + header transform (live coding) | 60min | Live coding | `/purchase` roteia + reescreve; `X-Correlation-ID` injetado |
| ☕ | Coffee break | 15min | — | — |
| 4 | Rate limiting + Output Cache + JWT placeholder (live coding) | 60min | Live coding | 429 dispara; `X-Cache: HIT`; JWT configurado anônimo |
| 5 | CI/CD + smoke test (429, cache HIT, trace no App Insights) | 40min | Hands-on | Workflow entendido; ACs validados |
| 6 | Trade-off APIM vs YARP + preparação F3 + Retro | 50min | Conversa + Q&A | Decide build vs buy; pronto para F3 (JWT) |

**Mindset do facilitador:** a turma vem da F1 (sabe o fluxo `POST /api/v2/purchase` → fila → SQL). O ouro didático da F2 é mostrar que **gateway não é caixa-preta**: cada policy que o APIM venderia é **código C# que a gente escreve e lê**. O segundo fio condutor é a decisão de engenharia **construir vs comprar** — volte a ela no Bloco 1 e feche com ela no Bloco 6.

**A frase âncora do dia:** "Um gateway faz por dentro o que você vai escrever em C#." Repita.

**Pré-checagem (antes de começar):** confirme em voz alta "todo mundo tem a Function F1 no ar e sabe a URL pública dela?" Sem isso, o gateway não tem para onde encaminhar no Bloco 2 (502).

---

## BLOCO 1 — Conceitos (50min · slides + Q&A)

**Objetivo:** ao fim, o aluno sabe *o que* um gateway faz (6 níveis), *por que* o cliente não deve ver a URL real do backend, e *por que* construímos com YARP em vez de comprar APIM.

### Pontos a enfatizar
- **A portaria do prédio:** a analogia central. Toda visita passa pela portaria (gateway): identifica-se, respeita as regras, ganha crachá de rastreio, e só então é encaminhada ao apartamento (Function). Os apartamentos não precisam ter portaria própria. Use isso o dia inteiro.
- **Reverse proxy ≠ proxy.** Proxy (forward) representa o cliente; reverse proxy representa o servidor — o cliente acha que fala com a aplicação. Gateways são reverse proxies.
- **Os 6 níveis** (slide dedicado): 0-ponto único, 1-roteamento, 2-transversais (coração), 3-resiliência, 4-observabilidade, 5-meta-aprendizado. Deixe claro que **construímos 0-2 em código** e **tocamos 3-5 conceitualmente**.
- **A pipeline de middleware e a ORDEM.** Este é o conceito técnico mais importante da fase. CORS → RateLimiter → OutputCache → Auth → Proxy. Rate limit ANTES do proxy = barra o abuso antes de gastar o backend. Errar a ordem = comportamento sutilmente quebrado.
- **Construir vs comprar** (slide de trade-off): APIM ~US$50-80 e 30-45min para provisionar vs YARP ~US$0 e segundos. **Não é "YARP > APIM"** — é "para aprender, construir; para muitas produções, comprar". A honestidade aqui é didática.

### Perguntas pra turma (escolher 1-2)
- "Quem já configurou Nginx, HAProxy, Traefik ou Ingress do Kubernetes? Vamos mapear os termos." (cria pontes — upstream/cluster, location/route).
- "No fim da F1 qualquer um chamava sua Function. Que problemas isso traz?" (acoplamento de topologia, sem controle central, superfície exposta → motiva o gateway).
- "Quando você construiria um gateway em código e quando compraria um gerenciado?" (planta o Bloco 6).

### Armadilhas (a evitar como instrutor)
- ⚠️ Não venda YARP como "melhor que APIM". Venda como "a escolha certa para *aprender o conceito*". APIM é ótimo em produção corporativa.
- ⚠️ Não mergulhe em load balancing/health checks agora — é Nível 3, conceitual; aprofundar confunde.
- ⚠️ Não detalhe o fluxo de claims do JWT (oid, iss/aud) — isso é F3. Em F2, JWT é só placeholder.

### Se sobrar tempo (+10min)
- Desenhe no quadro a pipeline de middleware como linha de montagem (ida e volta) e pergunte "onde você colocaria o rate limit? por quê?".
- Mostre a tabela de paridade APIM→YARP e peça pra turma adivinhar o equivalente .NET de cada policy antes de revelar.

### Se faltar tempo (-10min)
- Corte a discussão detalhada dos níveis 3-5 → uma frase cada.
- Pule a comparação Nginx/Traefik (mencione "é o mesmo conceito de outros proxies que vocês conhecem").

### Transição → Bloco 2
"Conceito na cabeça. Agora vamos subir o gateway de verdade — só que primeiro precisamos de um lugar pra guardar a imagem (Registry) e um lugar pra rodá-la (Container App). Abram o `PORTAL-GUIDE.md`."

---

## BLOCO 2 — Provisioning Container Registry + Container App (45min · demo guiada)

**Objetivo:** turma sai com **ACR + Container App do gateway** no ar, imagem `gateway:v1` deployada, `FunctionAppF1Url` configurada, e o smoke test (`POST /purchase` → 202 + `X-Correlation-ID`) passando.

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Steps 1-5). Projete sua tela; aguarde a turma em cada checkpoint.

### Pontos a enfatizar
- "Duas peças: o **Registry** guarda a imagem; o **Container App** roda a imagem. Você publica no Registry e o App puxa de lá."
- "**Target port 8080.**" Diga isso 3 vezes — é o erro nº1 do bloco. O `Dockerfile` expõe 8080 (`EXPOSE 8080` / `ASPNETCORE_URLS=http://+:8080`). Porta errada = 502 em tudo.
- "**`FunctionAppF1Url`** é a variável que liga o gateway à sua Function. Sem ela, o gateway cai no `localhost:7071` default do `appsettings.json` e dá 502."
- "Em variável de ambiente, seção aninhada usa **duplo underscore**: `Gateway__FrontendOrigin` → `Gateway:FrontendOrigin`."
- "**Container App é per-aluno** (ADE-004 Inv 5) — não há gateway compartilhado. O do colega cair não derruba o seu."
- "A connection string do SQL **NÃO** vai no gateway. Fica nas Functions. Segredo mora onde é usado (ADE-003 Inv 3)."

### Perguntas pra turma
- "Por que separar Registry e Container App?" (depósito da imagem vs runtime que a executa).
- "Por que o gateway só precisa da URL da Function, e não da connection string do SQL?" (o gateway fala com a Function; a Function fala com o SQL — separação de responsabilidades).

### Armadilhas (acompanhar a turma)
- ⚠️ **Target port** deixado em 80 → 502. Pegue isso no checkpoint do Step 3.
- ⚠️ **Nome do ACR** com hífen/maiúscula → Portal rejeita (só minúsculas e números).
- ⚠️ **`FunctionAppF1Url`** errada ou ausente → 502 só nas rotas `/purchase`. Mostre o curl ao `/health` (que funciona) vs `/purchase` (que dá 502) para isolar.
- ⚠️ **Admin user do ACR** desabilitado → Container App não puxa a imagem.

### Se sobrar tempo (+15min)
- Mostre as **revisões** do Container App (cada save de env var cria uma revisão nova) e o conceito de blue/green implícito.
- Mostre os **logs** do Container App em tempo real (Log stream) durante um `curl` — prepara a observabilidade do Bloco 5.

### Se faltar tempo (-10min)
- Faça o `az acr build` como demo única (instrutor projeta) em vez de cada aluno rodar.
- Pule a inspeção de `show-tags`; vá direto ao Container App.

### Transição → Bloco 3
"Gateway no ar com a imagem pronta. Agora vamos **abrir o código** e entender — e modificar — o que ele faz por dentro: roteamento, CORS e a injeção do `X-Correlation-ID`."

---

## BLOCO 3 — Routing + CORS + header transform (60min · live coding)

**Objetivo:** o aluno entende e demonstra: `/purchase` é roteado e **reescrito** para `/api/v2/purchase`; CORS restrito ao front; `X-Correlation-ID` injetado downstream e devolvido ao cliente.

> Código de referência: `src/Fifa2026.V2.Gateway/Program.cs` (transform + CORS), `appsettings.json` (routes/clusters), `Infrastructure/FunctionDestinationConfigFilter.cs` (override da destination).

### Pontos a enfatizar
- **Routes vs Clusters** (`appsettings.json`): `Routes` (`purchase-post`, `purchase-get`) dizem *quando* e *para qual cluster*; `Clusters` (`functions-f1`) dizem *onde* está o backend. Mostre os dois lado a lado.
- **Path rewrite** (Nível 1 / AC-4): o `Transforms` da route. `purchase-post` usa `PathSet: /api/v2/purchase`; `purchase-get` usa `PathPattern: /api/v2/purchase/{correlationId}`. Cliente usa caminho limpo; Function mantém a rota interna. **O cliente nunca vê `/api/v2`.**
- **Override da destination via código** (`FunctionDestinationConfigFilter`, um `IProxyConfigFilter`): o `appsettings.json` aponta para `localhost:7071` (dev); em produção, a env `FunctionAppF1Url` sobrescreve a `Address` da destination `f1`. Enfatize: **URL real nunca hardcoded no repo** (ADE-003 Inv 3).
- **CORS** (AC-7): `AddCors` policy `frontend` + `UseCors("frontend")`. Origin lido de `Gateway:FrontendOrigin` (default `https://fifa2026-web.azurewebsites.net`). Restrito ao front — não é `AllowAnyOrigin`.
- **O transform do `X-Correlation-ID`** (AC-8, o momento-chave do bloco): `AddRequestTransform` programático. Lê o header de entrada; se ausente/vazio, **gera um GUID novo** (`Guid.NewGuid()`); injeta downstream (`ProxyRequest.Headers`) E devolve ao cliente (`Response.Headers`). **É a semente da F6** — o gateway é o nó zero do Flow Visualizer.
- **Por que programático e não declarativo?** Porque exige lógica (gerar GUID se ausente) — config declarativa não faz isso.

### Perguntas pra turma
- "Quem garante que o cliente não veja `/api/v2/purchase`?" (o path rewrite na route — `PathSet`/`PathPattern`).
- "Por que a URL da Function não está no `appsettings.json` de produção?" (segredo/config de ambiente; vem da env via `IProxyConfigFilter`).
- "Se eu mandar um `X-Correlation-ID` próprio no request, o gateway respeita ou sobrescreve?" (respeita o que veio; só gera novo se ausente — mostre o `if` no código).

### Armadilhas
- ⚠️ Transform de `X-Correlation-ID` colocado em `clusters` em vez de `routes`/programático → não aplica como esperado. No nosso código é **programático** (`AddRequestTransform`), aplicado a todas as rotas.
- ⚠️ Confundir `PathSet` (define o caminho fixo) com `PathPattern` (usa o template com `{correlationId}`). POST usa Set; GET usa Pattern.
- ⚠️ CORS com `AllowAnyOrigin` "pra facilitar" → derrota o propósito. Mantenha o origin restrito.

### Demonstração (faça ao vivo)
```bash
# Local (gateway em http://localhost:5xxx, Function em :7071) ou contra o Container App.
curl -i -X POST http://localhost:5000/purchase \
  -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# Esperado: 202, body { "correlationId": "...", "status": "queued" }
#           header X-Correlation-ID: <guid>
```
Depois mostre nos logs da Function que a requisição chegou como **`/api/v2/purchase`** (reescrita) com o header `X-Correlation-ID` presente. "O cliente chamou `/purchase`; a Function recebeu `/api/v2/purchase`. O gateway reescreveu no caminho."

### Se sobrar tempo (+10min)
- Mande um `X-Correlation-ID` próprio no curl e mostre que o gateway **preserva** ele (não gera novo).
- Mostre o `GET /purchase/{id}` reescrito para `/api/v2/purchase/{id}`.

### Se faltar tempo (-15min)
- Use o `Program.cs`/`appsettings.json` prontos; foque em explicar o transform e o `IProxyConfigFilter` em vez de digitar.

### Transição → Coffee break → Bloco 4
"Roteamento e correlação prontos. Depois do café, o coração da fase: rate limiting (o 429 de verdade), output cache (o `X-Cache: HIT`), e o placeholder de JWT que a F3 vai ativar."

---

## ☕ Coffee break (15min)
Avise: "voltamos pro coração da F2 — rate limit, cache e o placeholder de segurança. Quem ainda não viu o 429 vai ver agora."

---

## BLOCO 4 — Rate limiting + Output Cache + JWT placeholder (60min · live coding)

**Objetivo:** o aluno demonstra 429 na 6ª chamada, `X-Cache: HIT` na 2ª GET idêntica, e entende o JWT configurado-mas-anônimo (placeholder F3). **Reforce a ORDEM da pipeline.**

> Código: `src/Fifa2026.V2.Gateway/Program.cs` (rate limiter, output cache, JWT, ordem do pipeline), `Infrastructure/XCacheOutputCachePolicy.cs` + `XCacheMiddleware.cs` (header X-Cache).

### Pontos a enfatizar (este é o coração da F2)
- **A ordem do pipeline é lei** (mostre no `Program.cs`): `UseCors → UseRateLimiter → UseMiddleware<XCacheMiddleware> → UseOutputCache → UseAuthentication → UseAuthorization → MapReverseProxy`. Repita o "por quê" de cada posição.
- **Rate limiting** (AC-5, paridade com `rate-limit-by-key`): `AddRateLimiter` com `RateLimitPartition.GetFixedWindowLimiter`, particionado pelo **IP** (`RemoteIpAddress`). `PermitLimit = 5`, `Window = 1 minuto`, `RejectionStatusCode = 429`. Aplicado à rota via `.RequireRateLimiting("fixed")` no `MapReverseProxy`. **6ª chamada em < 1min → 429.**
- **Output cache** (AC-6, paridade com `cache-lookup/store`): `AddOutputCache` policy `purchase-status-30s`, `.Expire(30s)`, aplicada via `.CacheOutput("purchase-status-30s")`. O ASP.NET Core **não expõe HIT/MISS nativamente** — por isso temos `XCacheOutputCachePolicy` (seta `X-Cache: HIT` no `ServeFromCacheAsync`) + `XCacheMiddleware` (seta default `X-Cache: MISS` via `Response.OnStarting`). Conte a história: esse middleware nasceu de um **bug real** — escrever em headers já commitados pelo YARP no caminho de MISS quebrava; a solução foi o `OnStarting`.
- **JWT placeholder** (AC-9, paridade com `validate-jwt` desabilitado): `AddAuthentication().AddJwtBearer("Entra", ...)` com `Authority = https://login.microsoftonline.com/{tenantId}/v2.0`. **MAS as rotas são anônimas** — não há `.RequireAuthorization()`. Aponte o comentário literal no código: `// F3: aplicar .RequireAuthorization()`. "A porta está instalada e destrancada. F3 vira a chave."

### Perguntas pra turma
- "Por que o rate limiter vem ANTES do proxy?" (barra o abuso antes de gastar o backend).
- "Por que precisamos de um middleware extra só pro header X-Cache?" (o OutputCache do ASP.NET Core não diz HIT/MISS sozinho).
- "O JWT está configurado. Por que ainda consigo chamar sem token?" (porque não há `RequireAuthorization` — é placeholder; F3 ativa).

### Armadilhas
- ⚠️ **Ordem do pipeline trocada** → comportamento sutil quebrado (ex.: cachear antes de rate-limitar). Esta é a armadilha nº1 da fase — pegue no code review.
- ⚠️ **`RequireRateLimiting` esquecido** no `MapReverseProxy` → `UseRateLimiter` está no pipeline mas a rota não aplica → nunca dá 429. Mostre que precisa dos dois.
- ⚠️ **Cache no POST** em vez do GET → o POST não deve ser cacheado; a policy é pensada para o GET de status.
- ⚠️ **Aplicar `RequireAuthorization()` em F2** → bloqueia tudo cedo demais. As rotas DEVEM ficar anônimas em F2. Confirme o comentário, não o código ativo.

### Demonstração de rate limit (momento "uau" nº1)
```bash
# Dispare 6 vezes seguidas (< 1 min). As 5 primeiras passam; a 6ª → 429.
for i in $(seq 1 6); do
  echo "--- chamada $i ---"
  curl -s -o /dev/null -w "%{http_code}\n" -X POST $GATEWAY/purchase \
    -H "Content-Type: application/json" \
    -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
done
# Esperado: 202 202 202 202 202 429
```
(No PowerShell, use `1..6 | % { ... }`.) "Cinco passam, a sexta bate no limite. Isso é o `rate-limit-by-key` do APIM, escrito por nós em ~10 linhas de C#."

### Demonstração de output cache (momento "uau" nº2)
```bash
# 1ª GET: X-Cache: MISS (vai à Function). 2ª idêntica em < 30s: X-Cache: HIT.
curl -is $GATEWAY/purchase/<correlationId> | grep -i x-cache   # MISS
curl -is $GATEWAY/purchase/<correlationId> | grep -i x-cache   # HIT
```
"A segunda resposta nem tocou a Function — veio do cache, em milissegundos."

### Se sobrar tempo (+10min)
- Espere os 30s do cache expirarem e mostre o `X-Cache` voltando a `MISS`.
- Mostre o teste de integração real (`RateLimitTests`, `OutputCacheTests`) e como o WireMock mocka o backend.

### Se faltar tempo (-15min)
- Faça só a demo de rate limit (a mais impactante); descreva o cache verbalmente e mostre o header numa chamada.
- Use o código pronto do `Program.cs`; explique a ordem e o JWT placeholder sem digitar.

### Transição → Bloco 5
"Tudo isso roda. Agora vamos automatizar: push no branch builda a imagem, publica no ACR, atualiza o Container App e roda o smoke test sozinho."

---

## BLOCO 5 — CI/CD + smoke test (40min · hands-on)

**Objetivo:** entender o workflow `deploy-phase-02.yml`; ver o pipeline build → test → push imagem → deploy → smoke test; e validar os ACs (429, cache HIT, trace).

> Arquivo: `.github/workflows/deploy-phase-02.yml`. **Push real / secrets é responsabilidade do @devops** — em sala, foque em ler e entender; o deploy real depende de `AZURE_CREDENTIALS`, `ACR_LOGIN_SERVER`, `PHASE02_CONTAINERAPP_NAME`, `PHASE02_RESOURCE_GROUP`.

### Pontos a enfatizar
- **Trigger por branch + paths:** push em `phase-02-gateway` (filtrado por `src/Fifa2026.V2.Gateway/**`) dispara o workflow; mais `workflow_dispatch` manual.
- **Etapas:** checkout → setup-dotnet 8 → restore → build → **test** → azure login → acr login → **build & push** da imagem (tag `github.sha`) → **`az containerapp update`** → **smoke test**.
- **A lição da F1 (H-1):** o step `Test` **NÃO usa `--no-build`**. Conte por quê: na S2.1 o `--no-build` quebrou porque o projeto de testes não tinha sido buildado. Aqui o `dotnet test` faz restore+build do projeto de testes e da dependência. Boa lição de CI/CD.
- **Smoke test no CI** (AC-10): resolve o FQDN do Container App, faz `POST /purchase`, valida `.correlationId` com `jq -e`, E verifica que o header `X-Correlation-ID` voltou (`grep`). Se algo falhar, o job falha. Rede de segurança.
- **Branching cumulativo (ADE-000 Inv 7):** `phase-01-servicebus-functions` → `phase-02-gateway` → ... Cada fase é um branch na linha do tempo, não feature branch paralela.

### Perguntas pra turma
- "Por que o smoke test verifica o header `X-Correlation-ID` além do `correlationId`?" (prova que o transform do gateway funcionou em produção, não só o roteamento).
- "Por que não usamos `--no-build` no test?" (o projeto de testes precisa ser buildado; lição da F1).

### Armadilhas
- ⚠️ Esperar deploy real sem secrets configurados — explique que é etapa do @devops.
- ⚠️ Confundir `vars` (não-secreto: `ACR_LOGIN_SERVER`, nome do Container App) com `secrets` (`AZURE_CREDENTIALS`).
- ⚠️ Cold start no smoke test — o workflow tem um `sleep 20` de warmup; mencione que é por causa do scale-to-zero.

### Validação dos ACs em sala (rode se Azure provisionado)
1. **429 (AC-5):** o loop de 6 chamadas → última 429.
2. **X-Cache: HIT (AC-6):** 2 GETs idênticos → MISS, depois HIT.
3. **Trace end-to-end (AC-11):** no App Insights, busque o `X-Correlation-ID` da resposta e mostre o trace Gateway → Function → Service Bus → Consumer. (Runtime-only; precisa de `APPLICATIONINSIGHTS_CONNECTION_STRING` no Container App.)

### Se sobrar tempo (+10min)
- Abra a aba **Actions** no GitHub e percorra um run: cada step, os logs do smoke test.
- Mostre o App Insights com o trace de borda do gateway (Nível 4).

### Se faltar tempo (-15min)
- Leia o YAML em conjunto e explique cada step; pule abrir o GitHub Actions ao vivo.
- Faça só a demo de 429 (não o cache) se o tempo apertar.

### Transição → Bloco 6
"Pipeline entendido. Vamos fechar com a pergunta de engenharia mais valiosa da fase — construir ou comprar? — e plantar o que vem na F3."

---

## BLOCO 6 — Trade-off APIM vs YARP + preparação F3 + Retro (50min · conversa)

**Objetivo:** consolidar a decisão build-vs-buy, revisitar o DoD, e plantar a ponte para a F3 (ativação do JWT).

### Roteiro da conversa
1. **A grande decisão (slide de trade-off):** retome a tabela. APIM (~US$50-80, 30-45min, XML proprietário, caixa-preta, mas gerenciado e com portal/governança) vs YARP (~US$0, segundos, C# transparente, mas é código seu pra manter). **Frase de fechamento:** "Em produção corporativa, o equivalente gerenciado deste gateway é o APIM. Aqui construímos em código para *ver o mecanismo*. Saber decidir entre os dois é o que esta fase te deu."
2. **Revisitar o DoD do aluno** (todos marcados?):
   - Container Registry + Container App do gateway no ar (per-aluno)
   - `/purchase` roteado e reescrito para `/api/v2/purchase` (cliente não vê a URL real)
   - 6ª chamada em < 1min → 429
   - 2ª GET idêntica → `X-Cache: HIT`
   - CORS restrito ao front
   - `X-Correlation-ID` injetado e devolvido
   - JWT configurado mas rotas anônimas (placeholder F3)
   - workflow do branch + smoke test
3. **A paridade APIM→YARP completa:** revise a tabela; cada policy tem seu equivalente em código. Nenhum conceito de gateway se perdeu.

### Perguntas pra turma (reflexão)
- "Em que cenário real você escolheria APIM em vez de construir? E o contrário?" (governança/portal/escala vs custo/controle/transparência).
- "Qual capacidade do gateway foi mais surdo-muda no APIM e ficou clara agora em código?" (geralmente: rate limit ou o transform de header).
- "O que muda na sua segurança quando a F3 ligar o `RequireAuthorization()`?" (rotas deixam de ser anônimas; token Entra obrigatório).

### Armadilhas (de fechamento)
- ⚠️ Não deixe ninguém sair achando que "APIM é ruim". Reforce o equilíbrio: produto gerenciado tem lugar legítimo.
- ⚠️ Reforce que o JWT de hoje é **placeholder**: configurado, anônimo. F3 é quem ativa — não é esquecimento.

### Carry-over para F3 (plante a curiosidade)
"Hoje a porta de identidade está instalada e **destrancada** — `AddJwtBearer` configurado, mas rotas anônimas. Na **F3** a gente vira a chave: ligamos o `.RequireAuthorization()`, conectamos o gateway ao **Entra ID** (App Registration + MSAL/Easy Auth), e o `X-Correlation-ID` que nasceu na borda vai andar junto com a identidade do usuário (o claim `oid`) por todo o fluxo. O gateway que vocês construíram hoje é o lugar onde a validação do token vai acontecer."

### Se sobrar tempo
- Discuta como empresas reais decidem build-vs-buy (não só gateway: auth, observabilidade, mensageria).
- Mostre o trace de borda do gateway no App Insights com mais profundidade (Nível 4 → semente da F6).

### Se faltar tempo
- Corte a discussão estendida de cenários; faça só o checklist do DoD e a ponte para F3.

---

## Apêndice — Mapa de troubleshooting (consulta rápida em sala)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| **502 Bad Gateway** em tudo | `Target port` ≠ 8080 no ingress | Confirmar **8080** (o `Dockerfile` expõe 8080) |
| **502** só em `/purchase` | `FunctionAppF1Url` ausente/errada (cai no `localhost:7071` default) | Conferir a App Setting com a URL pública real da Function F1 |
| **Rate-limit não dispara (sem 429)** | `RequireRateLimiting("fixed")` não aplicado à rota | Confirmar o `.RequireRateLimiting("fixed")` no `MapReverseProxy` |
| **`X-Cache` sempre MISS** | Policy de cache aplicada ao POST, ou GET não idêntico | Cache é no GET (`purchase-status-30s`); 2ª chamada deve ser idêntica e < 30s |
| **`X-Correlation-ID` não aparece no backend** | Transform não programático / colocado no cluster | No nosso código é `AddRequestTransform` (programático) — aplica a todas as rotas |
| **CORS bloqueado** no navegador | `Gateway__FrontendOrigin` ≠ domínio do front | Ajustar a env (com `__`) para o domínio exato |
| **JWT bloqueia chamadas em F2** | `RequireAuthorization()` aplicado cedo demais | Rotas DEVEM ser anônimas em F2; confirmar que está só no comentário `// F3:` |
| Cold start de segundos na 1ª chamada | Consumption scale-to-zero | Aceitar (didático); "min replicas: 1" em prod |
| `--no-build` falha no CI | Projeto de testes não buildado | NÃO usar `--no-build` no `dotnet test` (lição da S2.1 / H-1) |

---

## Lembretes finais para o facilitador
- **Volte sempre à analogia da portaria** e ao "gateway faz por dentro o que você escreve em C#". São os fios condutores.
- **A ordem do pipeline de middleware** é o conceito técnico mais importante. Não economize tempo nele (Blocos 3 e 4).
- **Construir vs comprar** é a pergunta de engenharia que a fase quer plantar — abra com ela (Bloco 1), feche com ela (Bloco 6), com honestidade (APIM não é ruim).
- O **JWT é placeholder** em F2: configurado, anônimo. F3 ativa. Diga isso explicitamente para não parecer esquecimento.
- Tom: prático, honesto sobre trade-offs, sem hype. A turma é técnica e respeita transparência — igual à F1.
