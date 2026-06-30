# SPEAKER NOTES — F4: Orquestração Visual com n8n em Container Apps

> **Notas do facilitador** · 7 blocos · 6h (360min) · Workshop "Living Lab Azure-Native"
> **Use junto com:** [`slides.md`](./slides.md) (Bloco 1), [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Blocos 2-4), [`README.md`](./README.md) (leitura prévia da turma), e o código real em `src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs` + `Functions/PurchaseConsumerFunction.cs` (Bloco 5).
> **Story:** [2.4](../../stories/2.4.story.md) · **Decisão:** [ADE-002](../../architecture/ade-002-mcp-pinning.md) Inv 4 (n8n `latest`) + [ADE-003](../../architecture/ade-003-v2-infrastructure-baseline.md) (Container Apps) + [ADE-005](../../architecture/ade-005-identity-easy-auth.md) Inv 3 (`entraOid`)

---

## Visão geral do dia (cole no flip chart)

| # | Bloco | Tempo | Modo | Marco do aluno |
|---|---|---|---|---|
| 1 | Conceitos: automação de workflow, low-code vs código, o que é o n8n, fire-and-forget | 50min | Expositivo + Q&A | Sabe por que a notificação sai do caminho crítico |
| 2 | Container Apps Environment + Azure Files (Portal) | 45min | Demo guiada (PORTAL-GUIDE) | CAE + file share `n8n-data` no ar |
| 3 | n8n Container App: imagem `latest`, env vars, basic auth, ingress, volume mount | 60min | Demo guiada | n8n acessível via HTTPS, basic auth funcional, persistente |
| ☕ | Coffee break | 15min | — | — |
| 4 | Desenhar/importar o workflow `post-purchase-notification` (4 nodes) no n8n UI | 50min | Hands-on | Workflow ativo; URL do webhook copiada |
| 5 | Integração: o consumer F1 dispara o webhook (fire-and-forget); App Setting `N8N_WEBHOOK_URL` | 55min | Live coding / leitura de código | Entende o disparo, idempotência e o no-DLQ |
| 6 | Smoke ponta-a-ponta + segurança (401 sem auth) + correlationId no App Insights | 45min | Hands-on | Fluxo completo visível; correlationId atravessa os 5 hops |
| 7 | Retro + Q&A: low-code vs código, custo do scale-to-zero, carry-over para F5 | 40min | Conversa | Pronto para F5 (MCP + LLM) |

**Mindset do facilitador:** a turma chega da F3 com o fluxo de compra v2 **completo e silencioso** — a compra grava e nada acontece depois. O ouro didático da F4 é mostrar que **o "depois da compra" é um problema diferente**, com garantias diferentes: a compra é crítica (idempotente, transacional), a notificação é best-effort (fire-and-forget). Não misture os dois níveis. O segundo fio é **low-code**: a orquestração sai do C# e vira um desenho que qualquer um evolui.

**A frase âncora do dia:** "O consumer grava a compra; o n8n cuida do que vem depois." Repita. (Em F2 era "o gateway faz por dentro o que você escreve em C#"; em F3 "o gateway é o guardião único da identidade"; em F4 o consumer ganha um braço de orquestração que **não pode** derrubá-lo.)

**Pré-checagem (antes de começar — faça em voz alta):**
- "Todo mundo tem o fluxo de compra v2 gravando no SQL (gateway F2 + Function F1 + identidade F3)?" Sem isso, o Bloco 6 trava.
- "Todo mundo consegue abrir Container Apps no Portal?" (já usaram na F2 para o YARP.)
- **[REVALIDAÇÃO DE VERSÃO — OBRIGATÓRIA, Task 10.2]:** suba/atualize o seu próprio n8n para `n8nio/n8n:latest` e **rode o workflow `post-purchase-notification` ponta-a-ponta ANTES da aula**. Como usamos `latest` (decisão de owner), a UI pode ter avançado desde a última turma. Confira que os 4 nodes e as expressões `$json.body.*` ainda funcionam. Grave a aula com a versão do dia.

---

## BLOCO 1 — Conceitos (50min · slides + Q&A)

**Objetivo:** ao fim, o aluno entende **automação de workflow**, a diferença **low-code vs código**, o que é o **n8n**, e — o mais importante — o padrão **fire-and-forget** e por que o n8n nunca pode mandar uma compra ao DLQ.

### Pontos a enfatizar
- **Orquestração é o "depois da compra".** E-mail, log, CRM, Slack. Programar cada um no consumer = dívida técnica no caminho crítico. Use a analogia da **régua de tomadas** (consumer "liga na tomada"; o que está plugado muda sem mexer na parede).
- **Low-code não é "sem código".** É tirar a lógica de orquestração do lugar errado (o caminho crítico) e pô-la numa ferramenta visual. O consumer continua C# testado; ele só **delega**.
- **n8n = Zapier/Make self-hosted.** Open-source, roda na **sua** infra (Container App). Workflow é visual, mas **exportável como JSON** e versionável (`infra/phase-04/post-purchase-notification.workflow.json`).
- **Fire-and-forget é o coração técnico.** O consumer dispara o webhook **só em `Inserted`**, com **timeout 5s**, e **engole qualquer falha** (log, nunca re-throw). A compra já está no SQL — o n8n é best-effort. **Falha do n8n NÃO manda a mensagem ao DLQ.**
- **Garantias diferentes para coisas diferentes.** Compra = idempotente/garantida. Notificação = best-effort. Esse é o insight de arquitetura da fase.

### Perguntas pra turma (as quatro perguntas-chave da fase)

**1. "Por que não programar o e-mail direto no consumer?"**
> Resposta-guia: porque toda ação pós-compra que você solda no consumer aumenta o acoplamento e o risco no caminho crítico — e, se tratada como erro, pode mandar uma compra boa ao DLQ. O low-code isola: o consumer só dispara um webhook; a lógica de notificação vive fora, fácil de evoluir sem redeploy do backend.

**2. "O que acontece se o n8n estiver fora do ar quando a compra é gravada?"**
> Resposta-guia: **nada de ruim para a compra.** O disparo é fire-and-forget com timeout de 5s; qualquer falha (n8n down, cold start, rede) é capturada e logada como `Warning`, **nunca re-lançada**. A compra continua gravada no SQL e a mensagem do Service Bus **não** vai ao DLQ. A notificação simplesmente não acontece desta vez (best-effort).

**3. "Por que o webhook dispara só em `Inserted` e não em `Duplicate`?"**
> Resposta-guia: **idempotência.** O Service Bus pode reentregar a mesma mensagem. O consumer faz INSERT idempotente; se a compra já existe (`Duplicate`), ele não notifica de novo — senão a mesma compra geraria dois e-mails. Só a **primeira gravação real** (`Inserted`) dispara o n8n. (`CategoryNotFound` também não dispara — vai ao DLQ.)

**4. "Quem dispara o n8n — o gateway ou o consumer?"**
> Resposta-guia: **o consumer F1** (`PurchaseConsumerFunction`), **não** o gateway. O gateway YARP (F2) está lá na frente, validando JWT e roteando. O n8n é o **quinto hop**, disparado **depois** que a compra foi gravada no SQL pelo consumer. Faz sentido: você só notifica uma compra que **de fato** aconteceu.

### Armadilhas (a evitar como instrutor)
- ⚠️ Não diga "low-code substitui código". Não substitui — **realoca**. O consumer continua sendo código robusto; só a orquestração saiu dele.
- ⚠️ Não trate a notificação como parte da transação da compra. Ela é **best-effort**. Se você sugerir "garantir entrega do e-mail", abre a porta para alguém querer re-throw na falha do n8n — que é exatamente o que **não** queremos.
- ⚠️ Não prometa reprodutibilidade exata da UI do n8n. Rodamos `latest` (decisão de owner) — a UI pode variar entre turmas. Diga isso à turma com honestidade (ver README seção 2).

### Se sobrar tempo (+10min)
- Mostre o `N8nWebhookNotifier.cs` ao vivo: aponte o `CancellationTokenSource.CancelAfter(WebhookTimeout)` (5s) e o `catch (Exception)` que só loga. "Vejam: nenhum `throw` aqui dentro."
- Pergunte: "onde mais na vida real você usaria fire-and-forget?" (analytics, métricas, notificações — tudo que é secundário ao caminho crítico).

### Se faltar tempo (-10min)
- Corte a comparação detalhada de plataformas SaaS; fique em "n8n = Zapier self-hosted".
- Foque fire-and-forget + "só em Inserted" — sustentam os Blocos 5-6.

### Transição → Bloco 2
"Conceito na cabeça. Agora a gente sobe o n8n na sua Azure — começando pelo Environment e pelo disco que vai guardar os workflows. Abram o `PORTAL-GUIDE.md`, Step 1."

---

## BLOCO 2 — Container Apps Environment + Azure Files (45min · demo guiada)

**Objetivo:** turma sai com um **Container Apps Environment** (`cae-fifa2026-<iniciais>`) e um **Azure Files share `n8n-data`** registrado nele.

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) Steps 1-2. Projete a tela; espere a turma em cada checkpoint.

### Pontos a enfatizar
- **O CAE é o mesmo tipo de recurso do gateway YARP (F2).** "Vocês já fizeram isso." Reusar o CAE do YARP é OK (até melhor para a observabilidade — mesmo Log Analytics).
- **Por que Azure Files?** Container é efêmero — reiniciou, perdeu o disco interno. O n8n guarda tudo em `/home/node/.n8n`. Sem o file share montado lá, **os workflows somem a cada restart**. Use a analogia do **cofre na recepção** vs quarto de hotel.
- **A ordem do registro de storage:** o file share precisa ser **registrado no CAE** (Step 2.3) **antes** de o app montá-lo. Storage lógico no CAE = `n8ndata` (`PHASE04_ACA_STORAGE_NAME`); file share físico = `n8n-data`.

### Perguntas pra turma
**"Por que o Container Apps e não Kubernetes (AKS)?"**
> Resposta-guia: para **um** container de workshop, operar um cluster K8s é desproporcional. O ACA dá ingress, HTTPS e scale-to-zero prontos, com custo ~US$0 em repouso. AKS é para quando você precisa orquestrar muitos serviços com controle fino — não é o caso aqui (ADE-003).

### Armadilhas
- ⚠️ Nome de Storage Account: só **minúsculas e números**, ≤ 24 chars, **globalmente único**. Se `stn8n<iniciais>` colidir, sufixe com números.
- ⚠️ Não confundir o **storage lógico no CAE** (`n8ndata`) com o **file share** (`n8n-data`). O mount usa o nome lógico.

### Transição → Bloco 3
"Disco pronto. Agora o n8n em si — a imagem, as variáveis, a senha e o HTTPS."

---

## BLOCO 3 — n8n Container App (60min · demo guiada)

**Objetivo:** turma sai com o **n8n no ar** via HTTPS, **basic auth obrigatório**, env vars corretas e o **volume Azure Files montado** em `/home/node/.n8n`.

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) Step 3-4.

### Pontos a enfatizar
- **A imagem é `n8nio/n8n:latest`** — decisão de owner (ADE-002 Inv 4). Diga em voz alta o trade-off: sempre a versão nova, mas reprodutibilidade entre aulas não garantida; por isso você revalidou o workflow antes da aula.
- **As env vars são REAIS e rastreadas à doc oficial** (`docs.n8n.io/hosting/environment-variables/`). Não invente nenhuma (AC-13). As 9 estão na tabela do PORTAL-GUIDE Step 3.4.
- **A senha NUNCA em texto** — vai como **secret** (`n8n-basic-auth-password`) e a env var **referencia** o secret. Isso é AC-10 e padrão de produção.
- **Ingress: external + HTTPS only + target port 5678.** `allowInsecure: false` → o ACA redireciona HTTP para HTTPS automaticamente.
- **`WEBHOOK_URL` depende do FQDN**, que só existe depois de criar o app. Ordem: cria → copia FQDN → atualiza `N8N_HOST`/`WEBHOOK_URL`. Sem isso, o n8n monta URLs de webhook erradas.
- **Volume mount em `/home/node/.n8n`** — feito na revisão (Step 3.4). É o que torna o n8n persistente.

### Perguntas pra turma
**"Por que basic auth e não deixar aberto no workshop?"**
> Resposta-guia: porque o n8n com ingress **external** está exposto na internet. Sem auth, qualquer um acessaria seus workflows e dispararia webhooks. Basic auth é o mínimo (AC-10). Em produção real você iria além (SSO, IP restrictions), mas o princípio "nunca exponha um painel admin sem auth" se aprende aqui.

### Demonstração obrigatória (Step 4 — segurança)
- `curl -s -o /dev/null -w '%{http_code}' https://<fqdn>/` → **401** (sem auth). Mostre na tela.
- Login no browser com `admin` + senha → UI abre. "Vejam: 401 sem credencial, 200 com. Segurança de pé."

### Armadilhas
- ⚠️ Esquecer de referenciar o secret na env var `N8N_BASIC_AUTH_PASSWORD` (deixar em texto) → falha de segurança E vaza no YAML.
- ⚠️ Esquecer de atualizar `WEBHOOK_URL` com o FQDN real → webhooks com URL errada.
- ⚠️ Cold start: ao abrir a UI pela primeira vez, demora. Não é erro — é o scale-to-zero acordando.

### Transição → Bloco 4 (após coffee)
"n8n no ar e seguro. Café rápido — e na volta a gente desenha o workflow que vai notificar cada compra."

---

## ☕ COFFEE BREAK (15min)

> Aproveite para **warm-up**: deixe o n8n de todos acordado (cold start fora do caminho da demo). Confira que ninguém ficou preso no basic auth.

---

## BLOCO 4 — Desenhar/importar o workflow (50min · hands-on)

**Objetivo:** turma sai com o workflow **`post-purchase-notification`** (4 nodes) **ativo** e a **URL do webhook copiada**.

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) Step 5.

### Pontos a enfatizar
- **Importe o JSON de referência** (`infra/phase-04/post-purchase-notification.workflow.json`) — mais rápido e reprodutível que desenhar do zero. Reforça a mitigação do `latest`.
- **Os 4 nodes e o que cada um faz:** Webhook (recebe `POST /webhook/purchase`) → Switch (VIP vs outros) → HTTP Request (mock e-mail VIP via httpbin.org) | Set (log estruturado padrão).
- **`$json.body.*` é a chave.** O corpo do POST chega em `$json.body`. As expressões leem `$json.body.category`, `$json.body.correlationId`. **Esqueceu o `.body` → Switch não bate.** Esta é a armadilha #1 de quem desenha do zero.
- **O `correlationId` aparece no log** (node Set). É o que permitirá rastrear o hop no App Insights (Bloco 6) e, em F6, no Flow Visualizer.
- **Ativar (toggle Active) é obrigatório** — sem isso, webhook = **404**. Copie a Production URL **depois** de ativar.

### Perguntas pra turma
**"Por que httpbin.org e não um e-mail de verdade?"**
> Resposta-guia: para o workshop, `httpbin.org/post` é um **mock** — ele ecoa o que você manda, então você vê o payload sair sem precisar configurar SendGrid/SMTP. O conceito (um HTTP Request node que "manda o e-mail") é idêntico; só o destino é um eco. Em produção, troca-se o node por um de e-mail real.

**"Posso adicionar um node de Slack/Teams?"**
> Resposta-guia: sim — e é o ponto do low-code! Arrasta o node, conecta na saída do Switch, configura. **Sem redeploy do backend.** Isso é o que o código no consumer não te dá de graça.

### Armadilhas
- ⚠️ Workflow não ativado → 404 no webhook (a armadilha mais comum da fase).
- ⚠️ Usar `$json.category` em vez de `$json.body.category` → Switch sempre cai no fallback.
- ⚠️ Copiar a **Test URL** em vez da **Production URL** do webhook — use a Production (sem `/webhook-test/`).

### Transição → Bloco 5
"Workflow ativo, URL na mão. Falta o gatilho: fazer o **consumer** disparar esse webhook depois de gravar a compra. Vamos ler o código — e entender por que ele é blindado contra o DLQ."

---

## BLOCO 5 — Integração: o consumer dispara o webhook (55min · live coding / leitura de código)

**Objetivo:** turma entende **como** e **por que** o `PurchaseConsumerFunction` dispara o webhook fire-and-forget, e configura o App Setting `N8N_WEBHOOK_URL`.

> Código real: `src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs`, `Data/N8nWebhookPayload.cs`, `Functions/PurchaseConsumerFunction.cs`, `Program.cs`. PORTAL-GUIDE Step 6.

### Roteiro de leitura de código (projete na tela)
1. **`Program.cs`** — `AddHttpClient<IN8nWebhookNotifier, N8nWebhookNotifier>()`. "O webhook é um `HttpClient` tipado, injetado por DI — testável e isolado."
2. **`N8nWebhookPayload.cs`** — os 4 campos: `correlationId`, `matchId`, `category`, `entraOid`. **Aponte a ausência de `amount`** e leia o comentário: o blueprint pedia `amount`, mas a `PurchaseMessage` não carrega valor monetário no corpo (resolvido só no INSERT/JOIN). **Não inventamos** (Art. IV).
3. **`N8nWebhookNotifier.cs`** — três coisas:
   - **No-op se `N8N_WEBHOOK_URL` vazio** (`LogDebug`, return). F4 é opcional.
   - **Timeout 5s** (`CancellationTokenSource.CancelAfter`).
   - **`catch (Exception)` que só loga** — timeout, rede, non-2xx: tudo engolido. "Procurem um `throw` aqui. Não tem."
4. **`PurchaseConsumerFunction.cs`** — o `switch (outcome)`:
   - **`Inserted`** → dispara o webhook (dentro de **outro** `try/catch` — defesa em profundidade).
   - **`Duplicate`** → **não** dispara (idempotência).
   - **`CategoryNotFound`** → re-throw → DLQ (mas isso é falha da **compra**, não do n8n).

### Pontos a enfatizar
- **Defesa em profundidade:** há `try/catch` no **notifier E no consumer**. Mesmo que o notifier viole o contrato e lance, o consumer captura. "Duas camadas garantem: o n8n nunca derruba a compra."
- **O payload sai do CORPO da `PurchaseMessage`**, não das Application Properties do Service Bus. `correlationId` e `entraOid` viajam serializados no body.
- **`N8N_WEBHOOK_URL` é App Setting, nunca hardcoded** (AC-6). Mostre o `IConfiguration["N8N_WEBHOOK_URL"]`. Configure no Portal (Step 6) ao vivo.
- **A ordem importa:** workflow Active → copia URL → grava o App Setting. URL gravada com workflow inativo = consumer dispara, n8n responde 404, consumer loga Warning e segue (a compra está gravada).

### Perguntas pra turma
**"Por que dois try/catch (notifier e consumer)?"**
> Resposta-guia: defesa em profundidade. O notifier já promete nunca lançar — mas se um dia alguém quebrar esse contrato (ex.: uma exceção antes do try), o consumer ainda captura. O custo é baixo; o benefício (garantir que o Service Bus nunca vá ao DLQ por causa do n8n) é alto. É um padrão de robustez para caminhos críticos.

**"E se eu quiser o `amount` no e-mail?"**
> Resposta-guia: então você precisa resolvê-lo **antes** de mandar — fazer o consumer ler o `unit_price` do SQL e enriquecer o payload. Mas isso é trabalho extra e estava fora do que a mensagem carrega. A decisão honesta foi **não inventar**: enviar só o que existe. Se o negócio exigir `amount`, é uma evolução consciente, não um chute.

### Armadilhas
- ⚠️ Tentar tratar a falha do n8n como erro do consumer (re-throw). **Nunca.** Isso é o anti-padrão que a fase inteira combate.
- ⚠️ Hardcodar a URL do webhook "só para testar". Não — App Setting sempre (AC-6).
- ⚠️ Esquecer que `Duplicate` não dispara → aluno reclama "mandei a mesma compra 2x e só veio 1 notificação". Isso é **correto** (idempotência).

### Transição → Bloco 6
"Código entendido, App Setting gravado. Agora a prova dos nove: uma compra real percorrendo os cinco hops, terminando numa execução visível no n8n."

---

## BLOCO 6 — Smoke ponta-a-ponta + segurança (45min · hands-on)

**Objetivo:** turma vê o fluxo completo (gateway YARP → Entry → Service Bus → Consumer → n8n) e o **`correlationId`** atravessando todos os hops; confirma a segurança (401 sem auth).

> PORTAL-GUIDE Step 7 (smoke) e Step 4 (segurança).

### Roteiro
1. **Warm-up** o n8n (cold start fora da demo).
2. **Dispare uma compra VIP** pelo gateway com Bearer token Entra válido (F3) → `202 { correlationId, status: "queued" }`.
3. **n8n → Executions:** execução nova, payload com o `correlationId`, status verde, branch **VIP** (HTTP node).
4. **App Insights:** busque o `correlationId` → ele aparece em gateway, Entry, Consumer e no log "Webhook n8n disparado com sucesso".
5. **Branch B:** compra `category: "Economy"` → no n8n segue o node **Set** (`notificationType: "standard-log"`).
6. **Segurança:** `curl` sem auth → 401; com `-u admin:<senha>` → 200.

### Pontos a enfatizar
- **O correlationId é o fio de Ariadne.** Nasceu no gateway (F2), atravessou Service Bus e consumer, e agora aparece no log do n8n. É o que tornará possível o **Flow Visualizer da F6**.
- **A demo é resiliente a versão.** Mesmo que o `latest` tenha avançado, os 4 nodes funcionam — você revalidou de manhã.
- **Idempotência ao vivo (se der):** reentregar a mesma mensagem → uma execução só no n8n.

### Armadilhas
- ⚠️ Cold start no meio da demo → "deu timeout!". Warm-up resolve. Se persistir, suba `minReplicas` para 1 temporariamente.
- ⚠️ Workflow inativo → 404; o consumer loga Warning mas nada aparece no n8n. Cheque o toggle Active.
- ⚠️ `N8N_WEBHOOK_URL` não aplicado (Function não reiniciou) → consumer faz no-op. Confirme o App Setting + restart.

### Transição → Bloco 7
"Funcionou ponta a ponta. Vamos fechar com o que aprendemos e olhar para a F5 — onde um LLM vai conversar com a sua plataforma."

---

## BLOCO 7 — Retro + Q&A + carry-over para F5 (40min · conversa)

**Objetivo:** consolidar low-code vs código, custo do scale-to-zero, e preparar a transição para a F5.

### Conduza a retro com estas perguntas
- "Quando vocês prefeririam **código** no consumer em vez do n8n?" (quando a ação é parte da garantia transacional, ou precisa de performance/controle fino).
- "E quando o **n8n** é melhor?" (pós-eventos best-effort, lógica que muda muito, gente não-dev que precisa evoluir o fluxo).
- "O que o **fire-and-forget** te ensinou sobre separar garantias?" (caminho crítico ≠ pós-processamento).
- "Quanto custou deixar o n8n no ar?" (~US$0 em repouso, graças ao scale-to-zero — mas com cold start como custo).

### Tabela de fechamento (cole no slide)
| Dimensão | Orquestração no código | Orquestração no n8n (low-code) |
|---|---|---|
| Garantia | Transacional / crítica | Best-effort (fire-and-forget) |
| Evoluir | Redeploy do backend | Editar o desenho |
| Risco no caminho crítico | Alto | Nenhum (não derruba a compra) |
| Quem mexe | Devs C# | Quem entende o negócio |

### Carry-over para F5 (transição honesta)
> "Na F4, o consumer disparou um webhook para uma **máquina** (o n8n) reagir a uma compra. Na **F5**, vamos plugar uma **IA** na sua plataforma: um MCP Server (.NET) que expõe *tools* (consultar disponibilidade, verificar ingresso, consultar bracket), e um chatbot com LLM que decide quais tools chamar — tudo passando pelo **mesmo gateway YARP** com o **mesmo `correlationId`** que você viu hoje. O `entraOid` que viajou até o payload do n8n vai identificar o usuário também lá. O fio condutor continua: um correlationId, um gateway, uma identidade."

### Perguntas que costumam aparecer
- **"O n8n aguenta produção real?"** Sim, mas com setup mais robusto (Postgres em vez de SQLite, fila de execução, mais réplicas). O nosso é didático (SQLite + Azure Files). O conceito escala; a config muda.
- **"E se eu quiser retry no webhook?"** O n8n tem retry/error workflows nativos — mas isso é do lado do **n8n**, não do consumer. O consumer continua fire-and-forget; quem garante reprocessamento da notificação é o n8n.

---

## Apêndice — Checklist do facilitador (antes/durante)

**Antes da aula:**
- [ ] **[Task 10.2]** n8n próprio em `latest` revalidado ponta-a-ponta (4 nodes funcionando)
- [ ] Aula configurada para **gravação** (registro da versão do dia)
- [ ] Token Entra válido em mãos para a demo de compra (F3)
- [ ] Function F1 + gateway F2 + identidade F3 confirmados no ar

**Durante (marcos por bloco):**
- [ ] B2: CAE + file share `n8n-data` registrado no CAE
- [ ] B3: n8n via HTTPS + 401 sem auth demonstrado + volume montado
- [ ] B4: workflow Active + Production URL copiada
- [ ] B5: `N8N_WEBHOOK_URL` no App Setting da Function + leitura do `N8nWebhookNotifier`
- [ ] B6: execução verde no n8n + correlationId no App Insights atravessando 5 hops

> **Anti-hallucination (Art. IV):** todos os nomes citados aqui são reais — `N8N_WEBHOOK_URL`, `n8nio/n8n:latest`, env vars `N8N_*` (doc oficial), payload `{ correlationId, matchId, category, entraOid }` (sem `amount`), webhook só em `Inserted`. Se algo na sua versão do n8n divergir da UI descrita, é o `latest` — os conceitos não mudam.
