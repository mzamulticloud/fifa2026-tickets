# F1 — Mensageria Desacoplada com Service Bus + Functions .NET

> **Leitura prévia obrigatória** · Workshop "Living Lab Azure-Native" (40h) · Fase 1 de 6
> **Tempo estimado de leitura:** 25-35 min · **Faça ANTES da aula.**
> **Story:** [2.1](../../stories/2.1.story.md) · **Pattern foundational:** [ADE-000](../../architecture/ade-000-microservice-parallel-pattern.md)

---

## 0. Por que você está lendo isto antes da aula

A Fase 1 é a **porta de entrada** da jornada cloud-native do workshop. Tudo o que construirmos aqui (Service Bus, Functions .NET, idempotência, dead-lettering, correlação ponta-a-ponta) vira o **molde herdado pelas Fases 2 a 6**. Se você chegar na aula tendo entendido os conceitos desta leitura, as 6 horas de hands-on rendem o dobro: gastaremos o tempo escrevendo código e investigando comportamento real, não soletrando teoria.

Esta leitura cobre:

1. Comunicação **síncrona vs assíncrona** — e por que desacoplar
2. **Anatomia do Azure Service Bus** (namespace, queue, topic, DLQ)
3. Service Bus vs Storage Queue vs Event Grid (quando usar cada um)
4. **At-least-once delivery** e por que ele te obriga a ter idempotência
5. **Idempotência no consumer** — o jeito certo (e o jeito errado que parece certo)
6. **Dead-letter queue (DLQ)** — o "hospital" das mensagens
7. O que vamos construir (arquitetura v2) e os contratos exatos
8. **Tarefa de pré-workshop: rodar a migration** (não pule — é a única coisa que você PRECISA fazer antes da aula)

> **Pré-requisitos de conhecimento:** você programa (qualquer linguagem) e já mexeu em cloud (não precisa ser Azure, não precisa ser .NET). Não exigimos experiência prévia com .NET nem com mensageria. Se você já usou RabbitMQ ou Kafka, ótimo — vai reconhecer vários conceitos com nomes diferentes.

---

## 1. Síncrono vs Assíncrono: o problema que estamos resolvendo

### 1.1 O fluxo síncrono (o que o v1 faz hoje)

No backend original (Node/Express, "v1"), comprar um ingresso é **síncrono**:

```
[Browser] --POST /purchase--> [API] --INSERT--> [SQL]
                              (espera tudo terminar)
   <----------- 200 OK + ticket ---------------
```

O navegador **espera** a API gravar no banco, calcular preço, e só então recebe a resposta. Funciona — até não funcionar:

- **Pico de carga** (abertura de vendas da Copa): 50.000 pessoas clicam "comprar" ao mesmo tempo. Cada request segura uma thread/conexão até o banco responder. O pool esgota, requests enfileiram, o usuário vê spinner eterno ou timeout.
- **Acoplamento temporal:** se o banco está lento ou em manutenção, a compra falha na cara do usuário. Não há "tente de novo depois automaticamente".
- **Trabalho pesado no caminho crítico:** validar estoque, calcular preço, gravar, talvez chamar gateway de pagamento — tudo no tempo que o usuário está olhando a tela.

### 1.2 O fluxo assíncrono (o que vamos construir — "v2")

A ideia central: **aceite rápido, processe depois.**

```
[Browser] --POST /api/v2/purchase--> [Entry Function] --msg--> [Service Bus Queue]
   <-- 202 { correlationId, status: "queued" } (em < 100ms)
                                                              [Consumer Function] <--msg--
                                                                    |
                                                                    INSERT --> [SQL]
```

O usuário recebe em **menos de 100ms** um "recibo" (`correlationId`) dizendo "recebi seu pedido, está na fila". O processamento pesado (gravar no banco, resolver preço) acontece **depois**, do outro lado da fila, no seu próprio ritmo.

| Dimensão | Síncrono (v1) | Assíncrono (v2) |
|---|---|---|
| Resposta ao usuário | Após gravar tudo | Imediata (recibo) |
| Pico de carga | Pool esgota, timeouts | Fila absorve o pico (buffer) |
| Banco lento/fora | Compra falha na hora | Mensagem espera na fila, processa quando voltar |
| Acoplamento | Forte (todos online juntos) | Fraco (produtor e consumidor independentes) |
| Complexidade | Baixa | Maior (idempotência, ordenação, DLQ) |

> **Quando NÃO usar assíncrono:** se o usuário PRECISA do resultado na hora (ex.: "este assento ainda está livre?"), o síncrono é o certo. Mensageria troca latência percebida por throughput e resiliência — não é grátis, paga-se em complexidade.

### 1.3 A pergunta de status

"Mas se eu só recebo um recibo, como sei se a compra deu certo?" Por isso existe o segundo endpoint:

```
GET /api/v2/purchase/{correlationId}  ->  { status: queued|processing|completed|failed, ticketId? }
```

O front guarda o `correlationId` e faz polling (ou, em fases futuras, recebe um push) até o status virar `completed`.

---

## 2. Anatomia do Azure Service Bus

O Service Bus é o **message broker** gerenciado do Azure — um carteiro confiável entre produtores e consumidores. Termos que você vai ouvir o dia inteiro:

```
[Service Bus Namespace]   ← o "container" / endereço (sb-fifa2026-<iniciais>-<rand>.servicebus.windows.net)
   ├── Queue: tickets-purchase          ← fila ponto-a-ponto (1 produtor → 1 consumidor lógico)
   │     └── tickets-purchase/$DeadLetterQueue   ← DLQ automática (sub-fila de mensagens "mortas")
   └── Topic (não usamos em F1)         ← publish/subscribe (1 produtor → N assinantes)
         └── Subscription
```

- **Namespace:** unidade de nomeação e isolamento. O nome é **globalmente único** (vira um DNS `.servicebus.windows.net`). É onde você escolhe o **tier** (Basic / Standard / Premium).
- **Queue (fila):** canal ponto-a-ponto. Uma mensagem é entregue a **um** consumidor lógico. É o que usamos: `tickets-purchase`.
- **Topic + Subscription:** modelo publish/subscribe (uma mensagem, vários assinantes). **Não usamos em F1**, mas o Standard tier já libera — vale saber que existe.
- **Dead-Letter Queue (DLQ):** toda queue tem uma sub-fila automática `<queue>/$DeadLetterQueue`. Mensagens que falharam N vezes (ou expiraram) são "estacionadas" lá em vez de sumir. **Você não cria a DLQ** — ela já vem junto. Veja a seção 6.

### 2.1 Propriedades da queue que vamos configurar

Na Fase 1 criamos a queue `tickets-purchase` via Portal com:

| Propriedade | Valor F1 | O que significa |
|---|---|---|
| **Lock duration** | 30s | Quando o consumer pega uma mensagem, ela fica "travada/invisível" por 30s para os outros. Se o consumer não confirmar nesse tempo, a mensagem volta para a fila. |
| **Max delivery count** | 10 | Quantas vezes uma mensagem pode ser reentregue antes de ir para a DLQ. Na 11ª falha, vira dead-letter. |
| **Dead-lettering** | habilitada | Liga o roteamento automático para a DLQ ao estourar o max delivery. |

> Detalhe que vai te morder: **lock duration < tempo de processamento = reentrega infinita.** Se o consumer leva 40s para gravar e o lock é 30s, a mensagem "destrava" no meio do processamento, é reentregue, e você processa em dobro. Por isso o `host.json` do projeto define `maxAutoLockRenewalDuration: "00:05:00"` — o SDK renova o lock automaticamente enquanto o consumer trabalha.

---

## 3. Service Bus vs Storage Queue vs Event Grid

Três serviços de mensageria no Azure, três casos de uso. Decore a intuição, não a tabela:

| | **Service Bus** | **Storage Queue** | **Event Grid** |
|---|---|---|---|
| Modelo | Broker enterprise (queue + topic) | Fila simples e barata | Roteador de **eventos** (pub/sub reativo) |
| Garantias | At-least-once, FIFO opcional, DLQ, transações, sessões | At-least-once, simples | At-least-once, retry, dead-letter para storage |
| Tamanho msg | até 256 KB (Standard) | até 64 KB | até 1 MB (evento) |
| Quando usar | **Comandos / trabalho a processar** ("faça esta compra") | Filas baratas, alto volume, sem features avançadas | **Reagir a eventos** ("um blob foi criado", "um recurso mudou") |
| Nosso caso | ✅ | — | — |

**Regra de bolso:**
- "Preciso que **alguém faça este trabalho**, de forma confiável, com retry e DLQ" → **Service Bus** (é o nosso caso: processar uma compra).
- "Preciso de uma fila simples e barata, milhões de mensagens, sem firulas" → **Storage Queue**.
- "Aconteceu **um evento** e quero notificar quem se interessar, em tempo real" → **Event Grid**.

> Por que Standard e não Basic? Basic só tem queue (sem topics). Por que não Premium? Premium dá VNet/partições/isolamento dedicado (~US$700/mês) — overkill didático. **Standard** é o ponto certo de custo × features para o workshop (decisão registrada em ADE-000, Alt 3).

---

## 4. At-least-once delivery: a garantia que muda tudo

O Service Bus (no modo que usamos) entrega cada mensagem **pelo menos uma vez** — `at-least-once`. Isso significa, em português claro: **a mesma mensagem PODE chegar mais de uma vez ao seu consumer.**

Por quê isso acontece? Imagine o ciclo:

1. Consumer recebe a mensagem (ela fica travada por `lock duration`).
2. Consumer processa (grava no SQL).
3. Consumer confirma ("complete") → mensagem sai da fila.

Agora, e se o consumer **gravar no SQL (passo 2) mas cair ANTES de confirmar (passo 3)**? O lock expira, o Service Bus assume "ninguém processou isto" e **reentrega**. Resultado: a compra é processada **duas vezes**.

```
Tentativa 1: recebe → INSERT no SQL ✅ → 💥 consumer cai antes de confirmar
Tentativa 2: recebe a MESMA msg → INSERT no SQL de novo ⚠️ DUPLICATA
```

Isso não é um bug do Azure — é o **trade-off fundamental** de sistemas distribuídos confiáveis. A alternativa, `at-most-once`, perderia mensagens em falhas (pior). `Exactly-once` de verdade é caríssimo/impossível na prática para a maioria dos cenários.

**Conclusão inescapável:** se a entrega é at-least-once, **o consumer TEM que ser idempotente.** Processar a mesma mensagem 2x precisa ter o mesmo efeito que processar 1x.

---

## 5. Idempotência no consumer: o jeito certo

Idempotente = "fazer de novo não muda nada além da primeira vez". Ligar um interruptor que já está ligado não muda o estado. Nosso consumer precisa ser assim: **mesma mensagem 2x = 1 registro em `purchases`.**

### 5.1 O jeito ERRADO que parece certo (SELECT-then-INSERT)

A intuição ingênua:

```csharp
// ❌ NÃO FAÇA ISSO
var existe = await db.QueryAsync("SELECT 1 FROM purchases WHERE correlation_id = @id", ...);
if (!existe) {
    await db.ExecuteAsync("INSERT INTO purchases ...");
}
```

Parece resolver, mas tem uma **race condition TOCTOU** (Time-Of-Check to Time-Of-Use). Com `maxConcurrentCalls=4`, várias mensagens são processadas em paralelo. Dois consumers podem rodar o SELECT **ao mesmo tempo**, ambos verem "não existe", e ambos fazerem o INSERT → **duas linhas duplicadas**. A janela entre "checar" e "usar" é o buraco.

### 5.2 O jeito CERTO (UNIQUE constraint + INSERT-catch)

Deixe o **banco de dados** ser o juiz da unicidade. Esse é o pattern obrigatório do workshop (ADE-000, Invariante 4):

1. A migration cria um **índice UNIQUE filtrado** em `correlation_id`:

   ```sql
   CREATE UNIQUE INDEX UQ_purchases_correlation_id
       ON dbo.purchases(correlation_id)
       WHERE correlation_id IS NOT NULL;
   ```

2. O consumer faz o **INSERT direto** e **captura a exceção de violação de unicidade** como "duplicata, ignore":

   ```csharp
   try {
       await connection.ExecuteAsync("INSERT INTO dbo.purchases (...) SELECT ...");
   }
   catch (SqlException ex) when (ex.Number is 2627 or 2601) {
       // 2627/2601 = violação de UNIQUE → a mensagem já foi processada.
       // Idempotência atingida: trata como sucesso silencioso.
   }
   ```

Não há janela de race: o INSERT é **atômico**. Se dois consumers tentam inserir o mesmo `correlation_id` simultaneamente, **um vence e o outro recebe o erro 2627** — que tratamos como "ok, já estava lá". O banco garante a unicidade de forma atômica.

> **Por que UNIQUE *filtrado* (`WHERE correlation_id IS NOT NULL`)?** As compras v1 históricas têm `correlation_id = NULL`. Um UNIQUE comum trataria múltiplos NULL como colisão e quebraria. O índice filtrado aplica a regra **só nas compras v2** (onde `correlation_id` existe).

> Códigos de erro do SQL Server: **2627** = violação de PRIMARY KEY/UNIQUE constraint; **2601** = violação de unique index. Capturamos os dois por segurança.

### 5.3 Onde o `correlationId` nasce

O `correlationId` (um GUID) é gerado **uma única vez**, na entrada (`PurchaseEntryFunction`), e viaja na mensagem até o banco. É a "impressão digital" daquela compra específica — a chave que torna a idempotência possível e, de quebra, permite rastrear a compra ponta-a-ponta (ver seção 7.3).

---

## 6. Dead-Letter Queue: o hospital das mensagens

Nem toda mensagem dá certo. Algumas são **veneno** (poison messages): por mais que você reprocesse, vão falhar sempre — ex.: um `matchId` que não existe, JSON corrompido, regra de negócio impossível.

Se você só fizer "re-throw e tenta de novo", uma poison message fica em **loop infinito**, ocupando o consumer e nunca saindo da fila. A DLQ resolve isso:

```
Mensagem falha → reentrega (tenta de novo) → falha → ... (até max delivery = 10)
                                                          → 11ª falha: vai para a DLQ
                                                            (tickets-purchase/$DeadLetterQueue)
```

A DLQ é um **estacionamento**: a mensagem sai do caminho principal (para de atrapalhar) mas **não é perdida** — fica lá para investigação manual. No workshop, você vai:

1. Forçar uma mensagem-veneno (ex.: `matchId: 99999`, que não tem categoria).
2. Ver o consumer falhar 10x e a mensagem cair na DLQ.
3. Abrir o **Service Bus Explorer** no Portal, inspecionar a mensagem na DLQ.
4. Reprocessá-la manualmente (re-submit / move).

> **No nosso código:** o consumer distingue dois tipos de falha. **Duplicata** (erro 2627) → engole silenciosamente, completa com sucesso (NÃO vai para DLQ). **Categoria inexistente / JSON inválido / correlationId vazio** → faz `throw`, o que força a reentrega e, após 10 tentativas, manda para a DLQ. Essa distinção é o coração do tratamento de falhas da Fase 1.

---

## 7. O que vamos construir (arquitetura v2)

### 7.1 Diagrama

```
[Browser]
   │ POST /api/v2/purchase  { matchId, category, userId, quantity }
   ▼
[Function App: fifa2026-v2-functions (.NET 8 isolated, Consumption)]
   ├─ PurchaseEntryFunction (HTTP trigger, authLevel: Anonymous em F1)
   │     │ gera correlationId (GUID)
   │     └─ publica msg → [Service Bus Standard]
   │     ◄─ responde 202 { correlationId, status: "queued" }   └─ Queue: tickets-purchase
   │                                                                  └─ DLQ: tickets-purchase/$DeadLetterQueue
   ├─ PurchaseConsumerFunction (Service Bus trigger)
   │     └─ INSERT idempotente em [SQL Server] → tabela purchases (source='v2', correlation_id, status='completed')
   └─ PurchaseStatusFunction (HTTP trigger GET)
         └─ SELECT purchases WHERE correlation_id → { status, ticketId? }
```

> **O que NÃO tocamos:** a API Node (`fifa2026-api/`) e o frontend continuam intactos. O fluxo v2 é um **microsserviço paralelo** que grava na **mesma** tabela `purchases`. O schema só ganha **2 colunas aditivas** (`source`, `correlation_id`). Isso é o pattern foundational do workshop (ADE-000, Invariante 1 e 2).

### 7.2 Os contratos exatos (decore — você vai testar com `curl`)

**POST `/api/v2/purchase`** — entrada da compra:

```jsonc
// Request body
{ "matchId": 1, "category": "VIP", "userId": 1, "quantity": 1 }
// category aceita: "VIP" | "Cat1" | "Cat2"
// quantity: 1 a 10 · matchId/userId: inteiros positivos

// Response (202 Accepted, < 100ms)
{ "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "status": "queued" }
```

**GET `/api/v2/purchase/{correlationId}`** — status da compra:

```jsonc
// Response (200 OK, < 200ms)
{ "status": "completed", "ticketId": 42 }
// status: "queued" | "processing" | "completed" | "failed"
// ticketId só aparece quando completed (é o purchases.id)
```

> Se você consultar o status **antes** do consumer ter gravado (mensagem ainda na fila), recebe `{ "status": "queued" }` — porque ainda não há linha em `purchases` para aquele `correlationId`.

### 7.3 Correlação ponta-a-ponta (preparando a F6)

O mesmo `correlationId` atravessa **todas as camadas** — isso prepara o terreno para o Flow Visualizer da Fase 6:

| Camada (hop) | Como o correlationId viaja |
|---|---|
| HTTP (entrada) | gerado como GUID na `PurchaseEntryFunction` |
| Service Bus | viaja no corpo da mensagem (`PurchaseMessage.correlationId`) |
| Logs | `ILogger.BeginScope(new { CorrelationId })` → App Insights `customDimensions.CorrelationId` |
| SQL | coluna `correlation_id UNIQUEIDENTIFIER` |

Na aula, você vai buscar **um único correlationId no Application Insights** e ver o trace atravessando a entry e o consumer. É o "raio-x" da compra.

---

## 8. ⚠️ Tarefa de pré-workshop: rode a migration ANTES da aula

Esta é a **única** coisa que você precisa fazer com as mãos antes da Fase 1. Levamos isso para fora da aula de propósito: rodar migration ao vivo gera atrito e come tempo de hands-on (AC-5 da story).

### O que a migration faz

O arquivo [`fifa2026-api/database/migrations/phase-01.sql`](../../../fifa2026-api/database/migrations/phase-01.sql) adiciona à tabela `purchases`:

- coluna `source NVARCHAR(20) NOT NULL DEFAULT 'v1'` — rastreia quem produziu a linha (v1 Node ou v2 Functions)
- coluna `correlation_id UNIQUEIDENTIFIER NULL` — a chave de correlação/idempotência
- índice `UNIQUE` filtrado `UQ_purchases_correlation_id` — garante idempotência no banco

A migration é **idempotente** (`IF NOT EXISTS`): rodar 2x não dá erro nem duplica nada. E é **somente aditiva** (nenhum DROP, nenhum ALTER de coluna existente) — o fluxo v1 continua funcionando exatamente igual.

### Como rodar

Use a connection string do SQL Server do workshop (a mesma do v1). Qualquer cliente serve:

```bash
# Opção A — sqlcmd (CLI)
sqlcmd -S <servidor>.database.windows.net -d <database> -U <usuario> -P <senha> \
       -i fifa2026-api/database/migrations/phase-01.sql

# Opção B — Azure Data Studio / SSMS: abra o arquivo e execute (F5)
```

### Como saber que deu certo

A própria migration imprime, no final, uma validação. Você deve ver:

- duas linhas em `column_name`: `source` e `correlation_id`
- uma linha em `index_name`: `UQ_purchases_correlation_id` com `is_unique = 1` e `has_filter = 1`
- a mensagem: `phase-01.sql aplicada/verificada — ...`

Se aparecer isso, você está pronto. Se der erro de "tabela `purchases` não encontrada", confira que está conectado ao banco certo — o nome real da tabela é **`purchases`** (minúsculo, schema `dbo`).

---

## 9. Glossário rápido

| Termo | Significado curtíssimo |
|---|---|
| **Broker** | Intermediário confiável entre quem manda e quem recebe mensagens (Service Bus). |
| **Producer / Consumer** | Quem publica / quem processa a mensagem. |
| **Queue** | Fila ponto-a-ponto (1 mensagem → 1 consumidor lógico). |
| **DLQ** | Dead-Letter Queue: sub-fila automática de mensagens que falharam demais. |
| **At-least-once** | Cada mensagem chega ≥ 1 vez (pode duplicar) → exige idempotência. |
| **Idempotência** | Processar 2x tem o mesmo efeito que 1x. |
| **TOCTOU** | Race condition entre "checar" e "usar" (por isso evitamos SELECT-then-INSERT). |
| **Lock duration** | Tempo que uma mensagem fica travada para um consumer antes de voltar à fila. |
| **Max delivery count** | Nº de tentativas antes da mensagem ir para a DLQ (10 na F1). |
| **Correlation ID** | GUID que identifica e rastreia uma compra por todas as camadas. |
| **Binding** | Forma declarativa do Functions conectar a triggers/saídas (`[ServiceBusTrigger]`, `[ServiceBusOutput]`). |
| **Cold start** | Latência extra (5-10s) na 1ª chamada de uma Function no plano Consumption. |
| **Isolated worker** | Modelo do .NET 8 onde a Function roda em processo separado do host. |

---

## 10. Checklist antes de entrar na aula

- [ ] Li e entendi síncrono vs assíncrono (seção 1)
- [ ] Entendi por que at-least-once obriga idempotência (seções 4 e 5)
- [ ] Sei a diferença entre SELECT-then-INSERT (errado) e UNIQUE + INSERT-catch (certo)
- [ ] Sei o que é a DLQ e quando uma mensagem cai nela (seção 6)
- [ ] Tenho uma subscription Azure ativa e login em portal.azure.com
- [ ] **Rodei a migration `phase-01.sql` e vi a validação OK (seção 8)** ← imprescindível
- [ ] Conheço os 2 contratos de endpoint (seção 7.2)

Nos vemos na aula. Próximo artefato que você vai usar: [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md), no Bloco 2.
