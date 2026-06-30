---
title: "F1 — Mensageria Desacoplada: Service Bus + Functions .NET"
subtitle: "Workshop Living Lab Azure-Native · Fase 1 de 6"
theme: black
revealOptions:
  transition: slide
---

# F1 — Mensageria Desacoplada

## Service Bus + Functions .NET

Workshop **Living Lab Azure-Native** · Fase 1 de 6

`POST /api/v2/purchase` → fila → `INSERT`

---

## O dia em 7 blocos · 6h

1. Conceitos (sync vs async, anatomia SB) — 50min
2. Provisioning SB via Portal — 45min
3. PurchaseEntryFunction (live coding) — 60min
4. ☕ Coffee — 15min
5. PurchaseConsumerFunction (live coding) — 60min
6. DLQ + Failures lab — 45min
7. CI/CD + Retro — 40+45min

---

## A frase do dia

# "Aceite rápido, <br/> processe depois."

<small>Esse é todo o espírito da mensageria.</small>

---

## Bloco 1 — Conceitos

### Síncrono vs Assíncrono <br/> · at-least-once · anatomia do Service Bus

---

## v1: o fluxo síncrono (hoje)

```
[Browser] --POST /purchase--> [API] --INSERT--> [SQL]
                              (espera tudo terminar)
   <----------- 200 OK + ticket ---------------
```

- O navegador **espera** gravar tudo
- Pico de carga → pool esgota → timeouts
- Banco lento/fora → compra falha **na cara** do usuário

<small>Não está errado — é certo quando o usuário precisa do resultado **agora**.</small>

---

## v2: o fluxo assíncrono (vamos construir)

```
[Browser] --POST /api/v2/purchase--> [Entry Fn] --msg--> [Service Bus]
   <-- 202 { correlationId, status:"queued" }  (< 100ms)
                                                   [Consumer Fn] <-- msg
                                                         INSERT --> [SQL]
```

- Resposta **imediata**: um recibo (`correlationId`)
- A fila **absorve o pico** (buffer)
- Produtor e consumidor **independentes**

---

## Sync vs Async — resumo

| | Síncrono (v1) | Assíncrono (v2) |
|---|---|---|
| Resposta | após gravar tudo | imediata (recibo) |
| Pico | pool esgota | fila absorve |
| Banco fora | falha na hora | mensagem espera |
| Acoplamento | forte | fraco |
| Complexidade | baixa | maior |

<small>Mensageria troca **latência percebida** por **throughput + resiliência**. Não é grátis.</small>

---

## Anatomia do Service Bus

```
[Namespace]  sb-fifa2026-<iniciais>-<rand>.servicebus.windows.net
   ├── Queue: tickets-purchase        ← ponto-a-ponto
   │     └── .../$DeadLetterQueue      ← DLQ automática
   └── Topic (não usamos em F1)        ← pub/sub
         └── Subscription
```

- **Namespace** = endereço + tier (Basic/Standard/Premium)
- **Queue** = 1 mensagem → 1 consumidor lógico
- **DLQ** = automática (você NÃO cria)

---

## SB vs Storage Queue vs Event Grid

| | Service Bus | Storage Queue | Event Grid |
|---|---|---|---|
| Modelo | broker enterprise | fila simples/barata | roteador de eventos |
| Use | **trabalho a fazer** | filas baratas, alto vol. | **reagir a evento** |
| Nós | ✅ | — | — |

**Regra de bolso:**
- "alguém faça este trabalho, confiável" → **Service Bus**
- "fila barata e simples" → Storage Queue
- "aconteceu um evento, notifique" → Event Grid

---

## Por que Standard?

- **Basic** → só queue (sem topics) ❌
- **Standard** → queue + topics, features que precisamos ✅
- **Premium** → VNet/partições, ~US$700/mês → overkill ❌

<small>Decisão registrada em ADE-000 (Alt 3).</small>

---

## At-least-once: a garantia que muda tudo

O Service Bus entrega cada mensagem **pelo menos uma vez**.

> A mesma mensagem **PODE chegar mais de uma vez.**

```
recebe → INSERT no SQL ✅ → 💥 cai antes de confirmar
reentrega → MESMA msg → INSERT de novo ⚠️ DUPLICATA
```

Não é bug — é o **trade-off** de sistemas distribuídos confiáveis.

---

## Conclusão inescapável

# at-least-once <br/> ⟹ <br/> idempotência

<small>Se a mensagem pode chegar 2x, processar 2x precisa ter o mesmo efeito que 1x.</small>

---

## Bloco 2 — Provisioning via Portal

### Namespace → Queue → DLQ → Connection String

Siga o **`PORTAL-GUIDE.md`** · eu projeto, vocês replicam.

---

## Os 4 steps do Portal

1. **Resource Group** `rg-fifa2026-workshop-<iniciais>` · East US 2
2. **Namespace** `sb-fifa2026-<iniciais>-<rand>` · **Standard** ⚠️
3. **Queue** `tickets-purchase` · lock **30s** · max delivery **10**
4. **Connection string** do namespace (`RootManageSharedAccessKey`)

✅ DLQ `tickets-purchase/$DeadLetterQueue` aparece sozinha

---

## ⚠️ Armadilha #1: EntityPath

```
✅ CERTO:  Endpoint=sb://...;SharedAccessKeyName=Root...;SharedAccessKey=...
❌ ERRADO: ...;SharedAccessKey=...;EntityPath=tickets-purchase
```

- Copie do **namespace**, não de dentro da queue
- O nome da fila está **no binding** do código
- Com `EntityPath` → a Function **não consome**

App Setting: **`ServiceBusConnection`**

---

## Bloco 3 — PurchaseEntryFunction

### HTTP trigger → publica na fila → 202

---

## O contrato de entrada

```jsonc
// POST /api/v2/purchase
{ "matchId": 1, "category": "VIP", "userId": 1, "quantity": 1 }
// category: "VIP" | "Cat1" | "Cat2" · quantity: 1-10

// 202 Accepted (< 100ms)
{ "correlationId": "3fa85f64-...", "status": "queued" }
```

- **202**, não 200 → "aceitei, ainda não terminei"
- `correlationId` = GUID gerado aqui (impressão digital da compra)

---

## A Function (essência)

```csharp
public sealed class EntryOutput {
  [ServiceBusOutput("tickets-purchase", Connection = "ServiceBusConnection")]
  public string? Message { get; set; }      // nome da fila NO binding
  public IActionResult? HttpResponse { get; set; }
}

[Function(nameof(PurchaseEntryFunction))]
public async Task<EntryOutput> RunAsync(
  [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v2/purchase")] HttpRequest req)
{
  var correlationId = Guid.NewGuid();
  // ...valida (DataAnnotations), serializa msg, responde 202...
}
```

<small>`authLevel: Anonymous` em F1 — segurança entra na F2 (gateway YARP).</small>

---

## Demo ao vivo

```bash
curl -i -X POST http://localhost:7071/api/v2/purchase \
  -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# → HTTP 202  { "correlationId": "...", "status": "queued" }
```

Depois: **Service Bus Explorer** → mensagem **ativa** na fila ⏳ (ninguém consumiu ainda)

---

## ☕ Coffee break — 15min

<small>Voltamos pro pulo do gato: **idempotência**.</small>

---

## Bloco 4 — PurchaseConsumerFunction

### Service Bus trigger → INSERT idempotente

---

## O jeito ERRADO (parece certo)

```csharp
// ❌ SELECT-then-INSERT
var existe = await db.QueryAsync("SELECT 1 ... WHERE correlation_id=@id");
if (!existe) await db.ExecuteAsync("INSERT ...");
```

**Race condition TOCTOU:** com `maxConcurrentCalls=4`, 2 consumers
checam "não existe" ao mesmo tempo → ambos inserem → **duplicata**.

---

## O jeito CERTO

### UNIQUE constraint + INSERT-catch

```sql
CREATE UNIQUE INDEX UQ_purchases_correlation_id
  ON dbo.purchases(correlation_id)
  WHERE correlation_id IS NOT NULL;   -- filtrado!
```

```csharp
try { await connection.ExecuteAsync("INSERT INTO dbo.purchases ..."); }
catch (SqlException ex) when (ex.Number is 2627 or 2601) {
  /* duplicata → idempotência atingida, ignora silencioso */
}
```

<small>O **banco** é o juiz da unicidade. INSERT atômico, sem janela de race. (ADE-000 Inv 4)</small>

---

## Por que UNIQUE *filtrado*?

- Compras **v1** têm `correlation_id = NULL`
- UNIQUE comum trataria vários NULL como **colisão** → quebraria
- Filtrado (`WHERE ... IS NOT NULL`) → regra **só nas compras v2**

Códigos SQL Server: **2627** (PK/UNIQUE constraint) · **2601** (unique index)

---

## INSERT...SELECT atômico

```sql
INSERT INTO dbo.purchases
  (user_id, ticket_category_id, quantity, unit_price, total_price,
   status, source, correlation_id, created_at, updated_at)
SELECT @UserId, tc.id, @Quantity, tc.price, tc.price * @Quantity,
       'completed', 'v2', @CorrelationId, GETDATE(), GETDATE()
FROM dbo.ticket_categories tc
WHERE tc.match_id = @MatchId AND tc.category = @Category;
```

- Preço/categoria resolvidos via **JOIN** (match_id + category)
- Par inexistente → `rowsAffected == 0` → **CategoryNotFound** → DLQ

---

## Demo: idempotência (momento "uau")

1. POST → pega `correlationId`
2. **Reenvia a MESMA mensagem** (Service Bus Explorer)
3. `SELECT COUNT(*) FROM purchases WHERE correlation_id='<id>'` → **1**
4. Log: *"duplicata ignorada"*

Mesma tabela, `source` distingue **v1 vs v2**.

---

## Bloco 5 — DLQ + Failures lab

### Quebrando coisas de propósito

---

## Dead-Letter Queue: o hospital

```
falha → reentrega → ... (até max delivery = 10) → 11ª: DLQ
                                tickets-purchase/$DeadLetterQueue
```

- **Poison message:** falha sempre (matchId 99999, JSON corrompido)
- DLQ = **estacionamento**, não lixeira (mensagem preservada)
- Sem DLQ → loop infinito ocupando o consumer

---

## Dois caminhos no consumer

```csharp
switch (outcome) {
  case Inserted:        /* sucesso */                         break;
  case Duplicate:       /* idempotência → completa, NÃO DLQ */ break;
  case CategoryNotFound: throw /* → reentrega → DLQ */;
}
```

- **Duplicata (2627)** → engole, completa ✅ (não vai pra DLQ)
- **CategoryNotFound / JSON inválido / correlationId vazio** → `throw` → DLQ

---

## Lab da DLQ

1. POST com `matchId: 99999` (sem categoria)
2. Observe: tenta, loga "Encaminhando ao DLQ", reentrega... 10x
3. **Service Bus Explorer → Dead-letter** → mensagem lá
4. Veja `DeadLetterReason` / `DeadLetterErrorDescription`
5. **Resubmit/Move** manual

<small>Em produção: corrija a causa **antes** de reprocessar.</small>

---

## Bloco 6 — CI/CD

### `deploy-phase-01.yml`

```yaml
on: { push: { branches: [phase-01-servicebus-functions] } }
# checkout → setup-dotnet 8 → dotnet publish
#   → Azure/functions-action@v1 → smoke test (curl + jq .correlationId)
```

- Trigger por **branch** (1 workflow por fase)
- Smoke test **obrigatório** valida `.correlationId`
- Branching **cumulativo** (ADE-000 Inv 7): phase-01 → 02 → ...

<small>Deploy real + secrets = responsabilidade do @devops.</small>

---

## Bloco 7 — Retro & DoD

### Você completou a F1 se...

- ✅ Namespace + queue + DLQ via Portal
- ✅ Entry responde < 100ms com `correlationId`
- ✅ Consumer grava `source='v2'` com idempotência
- ✅ Mesma msg 2x = **1** registro
- ✅ Poison message → DLQ após 10 entregas
- ✅ App Insights mostra correlationId end-to-end
- ✅ Workflow do branch em verde

---

## Correlação ponta-a-ponta (semente da F6)

| Hop | Como viaja |
|---|---|
| HTTP | GUID gerado na Entry |
| Service Bus | corpo da mensagem |
| Logs | `BeginScope(new { CorrelationId })` → App Insights |
| SQL | coluna `correlation_id` |

<small>Um único ID busca a compra inteira no App Insights.</small>

---

## Carry-over → F2

Hoje: qualquer um chama a Function (**Anonymous**).

**F2 — Gateway YARP em código (.NET):**
- rate-limiting, cache, CORS — em **C#**, não XML de APIM
- `X-Correlation-ID` propagado nos headers
- Container App, deploy em segundos

<small>O `correlationId` de hoje começa a viajar pelos headers HTTP.</small>

---

# Obrigado!

## Dúvidas?

Próxima leitura: **F2 — Gateway em código YARP**

`phase-01-servicebus-functions` → `phase-02-gateway`
