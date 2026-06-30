# SPEAKER NOTES — F1: Mensageria com Service Bus + Functions .NET

> **Notas do facilitador** · 7 blocos · 6h (360min) · Workshop "Living Lab Azure-Native"
> **Use junto com:** [`slides.md`](./slides.md) (Bloco 1), [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Bloco 2), código em `src/Fifa2026.V2.Functions/` (Blocos 3-4).
> **Story:** [2.1](../../stories/2.1.story.md) · **Pattern:** [ADE-000](../../architecture/ade-000-microservice-parallel-pattern.md)

---

## Visão geral do dia (cole no flip chart)

| # | Bloco | Tempo | Modo | Marco do aluno |
|---|---|---|---|---|
| 1 | Conceitos: sync vs async, SB vs SQ vs EG, anatomia SB | 50min | Expositivo + Q&A | Entende quando usar mensageria |
| 2 | Provisioning SB via Portal | 45min | Demo guiada (PORTAL-GUIDE) | Namespace + queue + DLQ no Portal |
| 3 | PurchaseEntryFunction | 60min | Live coding | Mensagem chega na queue |
| ☕ | Coffee break | 15min | — | — |
| 4 | PurchaseConsumerFunction | 60min | Live coding | Registro grava em SQL com idempotência |
| 5 | DLQ + Failures lab | 45min | Lab investigativo | Reprocessa mensagem do DLQ |
| 6 | CI/CD via GitHub Actions | 40min | Hands-on | Branch verde com deploy |
| 7 | Retro + Q&A + carry-over F2 | 45min | Conversa | Pronto para F2 |

**Mindset do facilitador:** a turma é polyglot, com background cloud mas **sem .NET prévio exigido**. Não assuma C#. Explique sintaxe .NET quando aparecer. O ouro didático da F1 é o **"v1 síncrono vs v2 assíncrono lado a lado"** — volte a essa comparação sempre.

**Pré-checagem (antes de começar):** confirme em voz alta "todo mundo rodou a migration `phase-01.sql`?" Quem não rodou, rode AGORA enquanto você abre o Bloco 1 (não pode chegar no Bloco 4 sem isso).

> ⚠️ **Pré-condição BLOQUEANTE — Azure SQL Database (ADE-003):** confirme também "todo mundo está com a migration rodada num **Azure SQL Database**, não num SQL em VM?" As Functions Consumption **não estão em VNet** e **não alcançam** SQL em VM — quem estiver em VM **vai travar no Bloco 4** (consumer não conecta, timeout sem erro claro). Quem não tiver Azure SQL DB não pode seguir até resolver. Ver [PRE-WORKSHOP-CHECKLIST seção 1](../PRE-WORKSHOP-CHECKLIST.md).

---

## BLOCO 1 — Conceitos (50min · slides + Q&A)

**Objetivo:** ao fim, o aluno sabe *quando* usar mensageria e *por que* ela obriga idempotência.

### Pontos a enfatizar
- **A frase âncora do dia:** "aceite rápido, processe depois". Repita várias vezes.
- O v1 síncrono **não está errado** — é certo para quando o usuário precisa do resultado na hora. Mensageria troca latência percebida por throughput e resiliência. **Não é grátis** (paga-se em complexidade: idempotência, DLQ, ordenação).
- **At-least-once é o conceito-chave da fase inteira.** Gaste tempo aqui. Use o cenário "consumer grava no SQL e cai antes de confirmar → reentrega → duplicata". Se eles entenderem isso, o Bloco 4 (idempotência) flui.
- Service Bus vs Storage Queue vs Event Grid: ensine a **intuição**, não a tabela. "Trabalho a fazer" = Service Bus; "fila barata e simples" = Storage Queue; "reagir a evento" = Event Grid.
- Standard tier: libera topics. Basic só queue. Premium é overkill (VNet/partições, ~US$700/mês).

### Perguntas pra turma (escolher 1-2)
- "Quem já usou RabbitMQ, Kafka ou SQS? Vamos mapear os termos." (cria pontes — exchange/topic, partition/session etc.)
- "Onde no app de ingressos da Copa o assíncrono ajuda mais? E onde ele atrapalharia?" (resposta esperada: ajuda na compra em pico; atrapalha em 'este assento está livre?').
- "Se a mesma mensagem pode chegar 2x, o que acontece com nossa compra?" (leva direto ao Bloco 4).

### Armadilhas (a evitar como instrutor)
- ⚠️ Não mergulhe em sessões/transações do Service Bus — fora do escopo F1, confunde.
- ⚠️ Não prometa "exactly-once" — não existe na prática aqui; a honestidade técnica é didática.
- ⚠️ Não desça em detalhe de OpenTelemetry agora — correlação é tocada de leve, aprofunda em F6.

### Se sobrar tempo (+10min)
- Desenhe no quadro o ciclo lock → process → complete e onde a falha vira reentrega.
- Mostre o `host.json` real do projeto (`maxConcurrentCalls=4`, `prefetchCount=0`) e pergunte "por que limitar concorrência?" (resposta: não exaurir o pool do SQL).

### Se faltar tempo (-10min)
- Corte a matriz detalhada SB/SQ/EG → fica só a regra de bolso (1 frase cada).
- Pule o approfundamento de topics/subscriptions (mencione que existe, segue).

### Transição → Bloco 2
"Conceito na cabeça. Agora vamos criar o broker de verdade. Abram o `PORTAL-GUIDE.md` — eu projeto, vocês replicam passo a passo."

---

## BLOCO 2 — Provisioning Service Bus via Portal (45min · demo guiada)

**Objetivo:** turma sai com **namespace Standard + queue `tickets-purchase` + DLQ** e a **connection string sem `EntityPath`**.

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Steps 1-4). Projete sua tela; aguarde a turma em cada checkpoint.

### Pontos a enfatizar
- "**Standard, não Basic.**" Diga isso pelo menos 3 vezes — é o erro nº1 do bloco.
- "A **DLQ é automática** — você não cria. Ela já vem como `<queue>/$DeadLetterQueue`. A gente só vai *consumir* dela no Bloco 5."
- "**Lock duration 30s** é o default; em produção, depende do tempo de processamento do consumer."
- "A connection string vem **do namespace** (RootManageSharedAccessKey), **sem `EntityPath`**. Essa é a armadilha que mais derruba gente na F1."

### Perguntas pra turma
- "Por que Standard e não Premium aqui?" (custo × features; não precisamos de VNet/partições).
- "O nome do namespace precisa ser único no mundo. Por quê?" (vira um DNS `.servicebus.windows.net`).

### Armadilhas (acompanhar a turma)
- ⚠️ **Região errada** — todos em **East US 2**. Quem criou em outra região: pode seguir, mas alinhe.
- ⚠️ **Naming** — namespace globalmente único; se colidir, troca os 3 dígitos.
- ⚠️ **Pricing tier Basic** por engano — se aconteceu, delete e recrie Standard.
- ⚠️ **Connection string com `EntityPath`** — pegue isso já aqui; mostre o "certo vs errado" do guia.

### Se sobrar tempo (+15min)
- Abra o **Service Bus Explorer** e **envie uma mensagem manual** para `tickets-purchase`. Mostre ela "ativa" na fila (ainda não há consumer). Isso prepara o Bloco 3.
- Mostre as **métricas** no Portal (mensagens ativas, contagem da DLQ).

### Se faltar tempo (-10min)
- Pule o teaser de Bicep (apêndice do guia) — mencione "dá pra fazer tudo isso como código, voltamos nisso".
- Pule o aprofundamento Standard vs Premium (1 frase e segue).

### Transição → Bloco 3
"Recursos prontos. Agora a primeira Function: ela recebe o POST e **publica** uma mensagem nessa queue que vocês acabaram de criar."

---

## BLOCO 3 — PurchaseEntryFunction (60min · live coding)

**Objetivo:** mensagem do POST chega na queue `tickets-purchase`; resposta 202 com `correlationId` em < 100ms.

> Código de referência: `src/Fifa2026.V2.Functions/Functions/PurchaseEntryFunction.cs` + `Models/PurchaseRequest.cs` + `Models/PurchaseMessage.cs`. Configure o `local.settings.json` com a connection string do Bloco 2 (App Setting **`ServiceBusConnection`**).

### Pontos a enfatizar
- **.NET 8 isolated worker:** a Function roda em processo separado do host. O entrypoint é o `Program.cs` (`HostBuilder().ConfigureFunctionsWebApplication()`), parecido com uma app ASP.NET Core.
- **Binding declarativo de saída:** `[ServiceBusOutput("tickets-purchase", Connection = "ServiceBusConnection")]`. Enfatize: **o nome da fila está no atributo** — por isso a connection string **não pode** ter `EntityPath`.
- **O `correlationId` nasce aqui** (`Guid.NewGuid()`) e é a impressão digital da compra. Volte ao conceito do Bloco 1.
- **Resposta 202 Accepted** (não 200): "Aceitei, ainda não terminei". Com `Location: /api/v2/purchase/{correlationId}` apontando para o status. Corpo: `{ correlationId, status: "queued" }`.
- **Validação por DataAnnotations:** `matchId`/`userId` positivos, `category` ∈ {VIP, Cat1, Cat2}, `quantity` 1-10. Body inválido → 400 (não vai pra fila).
- **`authLevel: Anonymous` em F1.** Diga explicitamente: "segurança NÃO é o foco hoje — entra na F2 com o gateway YARP. Em F1, Anonymous para reduzir atrito."
- **`BeginScope(new { CorrelationId })`** — é assim que o correlationId vai parar no App Insights (`customDimensions.CorrelationId`). Sementinha da F6.

### Perguntas pra turma
- "Por que respondemos 202 e não 200?" (não terminamos o trabalho ainda; é um recibo).
- "O que acontece se eu mandar `quantity: 999`?" (DataAnnotations rejeita → 400, nem entra na fila).

### Armadilhas
- ⚠️ Connection string **com `EntityPath`** → Function não publica/consome. (Pode reaparecer aqui.)
- ⚠️ Esquecer de preencher `ServiceBusConnection` no `local.settings.json` → erro de binding ao iniciar.
- ⚠️ `Content-Type` errado no teste com curl → use `application/json`.
- ⚠️ **Cold start:** a 1ª chamada local/Consumption pode levar segundos. É esperado, não é bug.

### Demonstração (faça ao vivo)
```bash
curl -i -X POST http://localhost:7071/api/v2/purchase \
  -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# Esperado: HTTP 202, body { "correlationId": "...", "status": "queued" }
```
Depois, abra o **Service Bus Explorer** e mostre a mensagem **ativa** na queue. "Ela está esperando — ninguém consumiu ainda. Isso é o próximo bloco."

### Se sobrar tempo (+10min)
- Mostre um POST inválido (`category: "XPTO"`) e o 400 com `details`.
- Mostre o log com o `correlationId` no console do `func`.

### Se faltar tempo (-15min)
- Pule a parte de validação detalhada (mostre que existe, não detalhe cada annotation).
- Use o código pronto do repo em vez de digitar tudo (foque em explicar, não em datilografar).

### Transição → Coffee break → Bloco 4
"A mensagem está na fila, parada. Depois do café, escrevemos quem **consome** ela e grava no banco — com o pulo do gato da idempotência."

---

## ☕ Coffee break (15min)
Avise: "voltamos pro pulo do gato da fase — idempotência. Quem não rodou a migration, é a hora."

---

## BLOCO 4 — PurchaseConsumerFunction (60min · live coding)

**Objetivo:** consumer grava em `purchases` (`source='v2'`, `status='completed'`) **com idempotência verificável** (mesma msg 2x = 1 registro).

> Código: `Functions/PurchaseConsumerFunction.cs` + `Data/PurchaseRepository.cs` + `Data/IPurchaseRepository.cs`. App Setting **`SqlConnectionString`** no `local.settings.json`.

### Pontos a enfatizar (este é o coração da F1)
- **`[ServiceBusTrigger("tickets-purchase", Connection = "ServiceBusConnection")]`** — a Function "acorda" sozinha quando há mensagem. Não há loop de polling no seu código.
- **O jeito ERRADO (SELECT-then-INSERT):** desenhe a race TOCTOU no quadro. Dois consumers paralelos (`maxConcurrentCalls=4`) checam "não existe" ao mesmo tempo e ambos inserem → duplicata. **Mostre por que a intuição ingênua falha.**
- **O jeito CERTO (UNIQUE constraint + INSERT-catch):** o banco é o juiz. INSERT direto; captura `SqlException ex when ex.Number is 2627 or 2601` → "duplicata, ignora silenciosamente". É **atômico**, sem janela de race. (ADE-000 Inv 4.)
- **UNIQUE *filtrado*** (`WHERE correlation_id IS NOT NULL`): porque as compras v1 têm `correlation_id NULL` e um UNIQUE comum quebraria com vários NULL.
- **INSERT...SELECT atômico:** o preço e o `ticket_category_id` saem de um JOIN em `ticket_categories` por `match_id` + `category`. Se o par não existe → `rowsAffected == 0` → `CategoryNotFound` → vai virar caso de DLQ (Bloco 5).
- **`source='v2'`:** volte ao "v1 vs v2 na mesma tabela". Mostre no SQL as linhas v1 (NULL/v1) e v2 lado a lado.
- **Queries parametrizadas** (Dapper + Microsoft.Data.SqlClient): nada de concatenar string (anti SQL injection).
- **`maxConcurrentCalls=4`** no `host.json`: limita a concorrência para não exaurir o pool do SQL.

### Perguntas pra turma
- "Por que NÃO podemos fazer SELECT e depois INSERT?" (TOCTOU — deixe alguém explicar).
- "Quem garante a unicidade: o código ou o banco?" (o banco, via UNIQUE index — esse é o ponto).
- "Por que o índice é *filtrado*?" (compras v1 têm correlation_id NULL).

### Armadilhas
- ⚠️ **Migration não rodada** → INSERT falha (sem coluna/index). Pegue isso AGORA: peça `SELECT TOP 5 source, correlation_id FROM purchases`.
- ⚠️ **`SqlConnectionString` vazio** no `local.settings.json` → o repositório lança `InvalidOperationException` no startup com mensagem clara.
- ⚠️ **Lock duration < tempo de processamento** → reentrega no meio do INSERT → processamento em dobro. (Mitigado por `maxAutoLockRenewalDuration` no host.json.)
- ⚠️ Confundir "duplicata" (engole, completa) com "falha permanente" (re-throw → DLQ). São caminhos diferentes no `switch (outcome)`.

### Demonstração de idempotência (faça ao vivo — é o momento "uau")
1. POST de uma compra → pegue o `correlationId`.
2. No Service Bus Explorer, **reenvie a MESMA mensagem** (mesmo `correlationId`) — ou peça pro consumer reprocessar.
3. Rode `SELECT COUNT(*) FROM purchases WHERE correlation_id = '<id>'` → **deve ser 1**.
4. Mostre no log: "Compra v2 já existente — duplicata ignorada".

### Se sobrar tempo (+10min)
- Faça `GET /api/v2/purchase/{correlationId}` e mostre `{ status: "completed", ticketId: N }`. Compare com consultar um id ainda na fila → `{ status: "queued" }`.

### Se faltar tempo (-15min)
- Use o `PurchaseRepository.cs` pronto; foque em explicar o `try/catch` do 2627 e o INSERT...SELECT.
- Adie a demo do GET status para o Bloco 5 ou 7.

### Transição → Bloco 5
"Funciona quando dá certo. Mas o que acontece quando dá **errado** de propósito? É hora de quebrar coisas e conhecer a DLQ."

---

## BLOCO 5 — DLQ + Failures lab (45min · lab investigativo)

**Objetivo:** o aluno **força** uma poison message, vê ela cair na DLQ após 10 tentativas, e **reprocessa** manualmente.

### Pontos a enfatizar
- **Poison message:** mensagem que falhará sempre, não importa quantas vezes reprocesse (ex.: `matchId: 99999` sem categoria, JSON corrompido).
- **O ciclo da DLQ:** falha → reentrega → ... → na 11ª (max delivery = 10) → `tickets-purchase/$DeadLetterQueue`.
- **Distinção crucial no nosso código:** duplicata (2627) **NÃO** vai pra DLQ (é sucesso). `CategoryNotFound`, JSON inválido, correlationId vazio → **`throw`** → reentrega → DLQ. Mostre os dois caminhos no `PurchaseConsumerFunction`.
- A DLQ é um **estacionamento, não uma lixeira**: a mensagem fica preservada para investigação, fora do caminho principal.

### Lab (passo a passo para a turma)
1. POST com `matchId: 99999` (não existe categoria) → `{ correlationId, status: "queued" }`.
2. Observe os logs: o consumer tenta, loga "Categoria inexistente... Encaminhando ao DLQ", faz throw, reentrega. Repete.
3. Após 10 tentativas, abra o **Service Bus Explorer → Dead-letter** → a mensagem está lá.
4. Inspecione: veja `DeadLetterReason` / `DeadLetterErrorDescription`.
5. **Reprocesse:** no Service Bus Explorer, selecione a mensagem na DLQ e faça **Resubmit/Move** para a fila ativa. (Como agora ainda falharia, discuta: "em produção, você corrigiria a causa antes de reprocessar".)

### Perguntas pra turma
- "Se eu não tivesse DLQ, o que aconteceria com essa mensagem-veneno?" (loop infinito ocupando o consumer).
- "Reprocessar uma mensagem da DLQ sem corrigir a causa adianta?" (não — corrige a causa primeiro).
- "Como saber *por que* a mensagem foi dead-lettered?" (`DeadLetterReason`/`DeadLetterErrorDescription`).

### Armadilhas
- ⚠️ Esperar a DLQ "na hora" — são **10 tentativas**; com reentregas pode levar um tempo. Tenha paciência ou reduza expectativa.
- ⚠️ Confundir mensagem "ativa" com "dead-letter" no Explorer — mostre o seletor de subfila.
- ⚠️ Aluno acha que duplicata vai pra DLQ — reforce: **não vai**, é tratada como sucesso.

### Se sobrar tempo (+10min)
- Force um **JSON inválido** direto pelo Service Bus Explorer e veja o caminho de dead-letter por desserialização.
- Mostre as **métricas da DLQ** (count) no Portal.

### Se faltar tempo (-15min)
- Faça o lab como **demo única** (instrutor força, projeta) em vez de cada aluno replicar.
- Pule o resubmit manual; só mostre a mensagem parada na DLQ.

### Transição → Bloco 6
"Tudo isso roda na sua máquina. Agora vamos colocar no ar de forma automática — push no branch dispara o deploy."

---

## BLOCO 6 — CI/CD via GitHub Actions (40min · hands-on)

**Objetivo:** entender o workflow `deploy-phase-01.yml`; ver o pipeline build → publish → deploy → smoke test.

> Arquivo: `.github/workflows/deploy-phase-01.yml`. **Push real / secrets é responsabilidade do @devops** — em sala, foque em ler e entender o pipeline; o deploy real depende de `PHASE01_FUNCTION_APP_NAME` e `PHASE01_FUNCTION_PUBLISH_PROFILE` configurados.

### Pontos a enfatizar
- **Trigger por branch:** push em `phase-01-servicebus-functions` dispara o workflow (mais `workflow_dispatch` manual).
- **Etapas:** checkout → setup-dotnet 8 → `dotnet publish` → `Azure/functions-action@v1` (deploy) → **smoke test** com `curl` + `jq` validando `.correlationId`.
- **Branching cumulativo (ADE-000 Inv 7):** cada fase é um branch na linha do tempo (`phase-01` → `phase-02` → ...). Hotfix em fase antiga = cherry-pick para as seguintes. Não é feature branch paralela.
- **Smoke test no CI é obrigatório:** se o `curl` não retornar `.correlationId`, o pipeline falha. É a rede de segurança do deploy.

### Perguntas pra turma
- "Por que um workflow **por branch/fase** em vez de um único?" (CI/CD isolado por fase; rollback didático).
- "O que o smoke test prova?" (que o endpoint subiu e responde o contrato esperado).

### Armadilhas
- ⚠️ Esperar deploy real sem secrets configurados — explique que isso é etapa do @devops.
- ⚠️ Confundir `vars` (não-secreto, ex. nome da app) com `secrets` (publish profile).

### Se sobrar tempo (+10min)
- Abra a aba **Actions** no GitHub e percorra um run (mesmo que de exemplo): logs de cada step.

### Se faltar tempo (-15min)
- Apenas leia o YAML em conjunto e explique cada step; pule abrir o GitHub Actions ao vivo.

### Transição → Bloco 7
"Pipeline entendido. Vamos fechar conectando os pontos e olhando o que vem na F2."

---

## BLOCO 7 — Retro + Q&A + carry-over para F2 (45min · conversa)

**Objetivo:** consolidar, tirar dúvidas, e plantar a ponte para a Fase 2.

### Roteiro da retro
1. **Revisitar o DoD do aluno** (todos marcados?):
   - namespace + queue + DLQ via Portal
   - Entry responde em < 100ms com correlationId
   - mensagem visível na queue
   - Consumer grava com `source='v2'`
   - idempotência (mesma msg 2x = 1 registro)
   - poison message vai pra DLQ após 10 entregas
   - (App Insights trace — se Azure provisionado) correlationId atravessando entry + consumer
   - workflow do branch em verde
2. **Observabilidade** (se houver App Insights): busque um `correlationId` no App Insights e mostre o trace atravessando entry → consumer (`customDimensions.CorrelationId`). É o raio-x da compra.
3. **Demo final v1 vs v2:** mostre uma compra v1 (síncrona) e uma v2 (assíncrona) gravadas na MESMA tabela `purchases`, diferenciadas por `source`. "Mesma tabela, dois mundos."

### Perguntas pra turma (reflexão)
- "Onde mensageria fez diferença? Onde ela adicionou complexidade?"
- "Qual foi a parte mais contra-intuitiva?" (quase sempre: idempotência / TOCTOU).
- "Como você explicaria 'at-least-once' para um colega?"

### Armadilhas (de fechamento)
- ⚠️ Não deixe ninguém sair achando que "exactly-once" é possível aqui.
- ⚠️ Reforce que segurança (Anonymous → autenticado) vem na F2/F3 — não é esquecimento.

### Carry-over para F2 (plante a curiosidade)
"Hoje qualquer um chama nossa Function (Anonymous). Na **F2** colocamos um **gateway YARP em código** (.NET, Container App) na frente: rate-limiting, cache, CORS, propagação de `X-Correlation-ID` — tudo em C# que vocês leem e escrevem, em vez de XML de APIM. E o `correlationId` que nasceu hoje vai começar a viajar pelos headers HTTP também."

### Se sobrar tempo
- Q&A aberto; mostre o App Insights com mais profundidade.
- Discuta como o pattern de hoje (microsserviço paralelo + schema aditivo) é o que empresas reais fazem em modernização incremental.

### Se faltar tempo
- Corte a demo v1 vs v2 detalhada (resuma verbalmente).
- Faça só o checklist do DoD e a ponte para F2.

---

## Apêndice — Mapa de troubleshooting (consulta rápida em sala)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| Função não consome mensagem | Connection string com `EntityPath=...` no App Setting | Remover `EntityPath`; o nome da fila está no binding |
| Mensagem reentrega infinita | Lock duration < tempo de processamento | Aumentar lock ou confiar no `maxAutoLockRenewalDuration` (já no host.json) |
| `CommandTimeout` no SQL | Connection pool exaurido | `maxConcurrentCalls=4` no host.json (já configurado) |
| Cold start de ~10s na 1ª chamada | Consumption plan | Aceitar (didático); Premium não nesta fase |
| 401 ao chamar a Function | `authLevel: Function` sem chave | F1 usa `authLevel: Anonymous` (segurança vira F2) |
| Mensagem como string vs JSON | Content-Type errado no envio | Forçar `application/json` |
| "Managed Identity not enabled" | Tentativa precoce de usar MI | F1 usa connection string; MI fica para fases de identidade |
| `purchase` table not found | Nome real é `purchases` (minúsculo, schema dbo) | Confirmar conexão ao banco certo |
| INSERT falha por coluna/index ausente | Migration `phase-01.sql` não foi rodada | Rodar a migration (idempotente) antes de processar |

---

## Lembretes finais para o facilitador
- **Volte sempre ao "v1 síncrono vs v2 assíncrono".** É o fio condutor.
- **At-least-once → idempotência** é o conceito que justifica metade da fase. Não economize tempo nele.
- A F1 é o **molde das F2-F6**. O cuidado de hoje paga dividendos nas próximas 30h.
- Tom: prático, honesto sobre trade-offs, sem hype. A turma é técnica e respeita transparência.
