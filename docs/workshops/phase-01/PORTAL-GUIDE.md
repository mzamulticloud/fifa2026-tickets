# PORTAL GUIDE — F1: Provisioning do Service Bus

> **Bloco 2 do roteiro (45min)** · Demo guiada: o instrutor projeta, você replica.
> **Objetivo:** sair daqui com **namespace + queue `tickets-purchase` + DLQ** visíveis no Portal e a **connection string** copiada.
> **Story:** [2.1](../../stories/2.1.story.md) (AC-2) · **Naming:** [ADE-000](../../architecture/ade-000-microservice-parallel-pattern.md) (Invariante 6)

---

## Pré-requisitos

- Subscription Azure ativa (free trial US$200 serve)
- Login em **portal.azure.com**
- Suas **iniciais** definidas (ex.: `jds` para João da Silva) — usamos em todos os nomes
- Migration `phase-01.sql` já rodada (ver [README](./README.md) seção 8)

> ⚠️ **BLOQUEANTE — Azure SQL Database (ADE-003):** antes de qualquer Step, confirme que sua camada de dados é um **Azure SQL Database** (`*.database.windows.net`), **não** SQL em VM. As Azure Functions em Consumption **não estão em VNet** e **não alcançam** SQL numa VM com IP privado — sem Azure SQL DB, o consumer do Bloco 4 **não conecta** e a F1 falha. Detalhes e validação no [PRE-WORKSHOP-CHECKLIST seção 1](../PRE-WORKSHOP-CHECKLIST.md#1-️-pré-condição-física-obrigatória--azure-sql-database-ade-003).

> **Convenção de nomes (ADE-000 Inv 6):** substitua `<iniciais>` pelas suas e `<rand>` por 3 dígitos quaisquer. O **namespace é globalmente único** — se o nome já existir, troque os dígitos.

| Recurso | Padrão de nome | Exemplo |
|---|---|---|
| Resource Group | `rg-fifa2026-workshop-<iniciais>` | `rg-fifa2026-workshop-jds` |
| Service Bus namespace | `sb-fifa2026-<iniciais>-<rand>` | `sb-fifa2026-jds-417` |
| Queue | `tickets-purchase` | (fixo — não mude) |

> **Região do workshop: East US 2.** Use sempre a mesma para todos os recursos (evita latência cruzada e simplifica troubleshooting).

---

## Step 1 — Criar o Resource Group (3min)

Um Resource Group (RG) é a "pasta" que agrupa todos os recursos da fase (facilita o teardown no final).

1. No Portal, na busca do topo, digite **"Resource groups"** e abra.
2. Clique **`+ Create`**.
3. **Subscription:** selecione a sua.
4. **Resource group name:** `rg-fifa2026-workshop-<iniciais>`
5. **Region:** **East US 2**
6. Clique **`Review + create`** → **`Create`**.

> `[PRINT 1: tela "Review + create" do Resource Group preenchida]`
> `[PRINT 2: notificação verde "Resource group created" + RG na lista]`

✅ **Checkpoint:** o RG aparece na lista de Resource Groups.

---

## Step 2 — Criar o Service Bus Namespace (10min)

O namespace é o endereço do seu broker (`<nome>.servicebus.windows.net`) e onde você escolhe o tier.

1. Na busca do Portal, digite **"Service Bus"** e abra.
2. Clique **`+ Create`**.
3. **Subscription / Resource group:** os do Step 1 (`rg-fifa2026-workshop-<iniciais>`).
4. **Namespace name:** `sb-fifa2026-<iniciais>-<rand>` (globalmente único).
5. **Location:** **East US 2**.
6. **Pricing tier:** **Standard** ⚠️ — **não escolha Basic** (Basic não tem topics nem todas as features; precisamos de Standard).
7. Clique **`Review + create`** → **`Create`**.
8. Aguarde o provisioning (~2 min). Quando terminar, clique **`Go to resource`**.

> `[PRINT 3: tela de criação com Pricing tier = Standard destacado]`
> `[PRINT 4: namespace provisionado, página de overview]`

✅ **Checkpoint:** o namespace abre na página **Overview** e o tier mostra **Standard**.

> ⚠️ **Armadilha comum:** escolher **Basic** por engano. Se você criou Basic, delete e recrie como Standard — o tier não pode ser rebaixado/alterado depois para o que precisamos.

---

## Step 3 — Criar a Queue `tickets-purchase` (5min)

Aqui criamos a fila com as propriedades exatas que o código espera.

1. Na página do namespace (Overview), no menu lateral, em **Entities**, clique em **Queues**.
2. Clique **`+ Queue`**.
3. **Name:** `tickets-purchase` (exatamente assim — é o nome usado nos bindings `[ServiceBusOutput("tickets-purchase")]` e `[ServiceBusTrigger("tickets-purchase")]`).
4. Expanda as propriedades avançadas e configure:
   - **Max delivery count:** **10** (após 10 falhas → DLQ)
   - **Lock duration:** **30 seconds** (00:00:30)
   - **Message time to live:** deixe o default (14 dias)
   - **Dead lettering on message expiration:** pode deixar habilitado
   - (a **dead-letter queue é automática** — você não cria nada extra)
5. Clique **`Create`**.

> `[PRINT 5: formulário "Create queue" com Name=tickets-purchase, Max delivery count=10, Lock duration=30s]`
> `[PRINT 6: queue tickets-purchase listada em Queues]`

✅ **Checkpoint:** `tickets-purchase` aparece na lista de Queues.

> **Por que esses valores?** `Lock duration 30s` é o default e basta para nosso INSERT (o SDK renova o lock se precisar, via `maxAutoLockRenewalDuration` no `host.json`). `Max delivery count 10` é o número de tentativas antes do dead-lettering (você vai forçar isso no lab do Bloco 5).

### Conferir a DLQ (sem criar nada)

1. Abra a queue `tickets-purchase` → clique em **Service Bus Explorer** (menu lateral).
2. No seletor de fila/subfila, escolha **Dead-letter** — você verá a sub-fila `tickets-purchase/$DeadLetterQueue` (vazia por enquanto).

> `[PRINT 7: Service Bus Explorer mostrando a sub-fila Dead-letter]`

✅ **Checkpoint:** a DLQ `tickets-purchase/$DeadLetterQueue` está visível (e vazia).

---

## Step 4 — Copiar a Connection String (4min)

A connection string é o "segredo" que as Functions usam para falar com o namespace.

1. Na página do **namespace** (não da queue), no menu lateral, em **Settings**, clique em **Shared access policies**.
2. Clique na policy **`RootManageSharedAccessKey`** (existe por padrão).
3. Copie o valor de **Primary Connection String**.

A connection string tem este formato:

```
Endpoint=sb://sb-fifa2026-<iniciais>-<rand>.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<chave-longa-aqui>
```

> `[PRINT 8: Shared access policies → RootManageSharedAccessKey → botão de copiar a Primary Connection String]`

### ⚠️ A armadilha #1 da Fase 1: `EntityPath`

Quando você copia a connection string **do namespace** (policy `RootManageSharedAccessKey`), ela vem **SEM** `EntityPath=...` — que é o correto.

Se em vez disso você copiar a connection string **de dentro da queue** (a queue também tem suas próprias policies), ela vem **COM** `EntityPath=tickets-purchase` no final. **Isso quebra os bindings das Functions**, porque o nome da fila já está no atributo do binding. Sintoma: a Function simplesmente não consome a mensagem.

**Regra:** copie sempre do **namespace** (`RootManageSharedAccessKey`). Se sua string tiver `EntityPath=`, **remova essa parte**.

```
✅ CERTO:  Endpoint=sb://...;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=...
❌ ERRADO: Endpoint=sb://...;SharedAccessKey=...;EntityPath=tickets-purchase
```

### Onde a connection string vai

Essa string será usada como o App Setting **`ServiceBusConnection`** (é o nome de connection referenciado pelos bindings no código — `Connection = "ServiceBusConnection"`). No desenvolvimento local você a coloca no seu `local.settings.json` (cópia local, **nunca commitada**); em produção, nas App Settings da Function App (configurado pelo @devops / pelo deploy).

✅ **Checkpoint:** connection string copiada e **sem** `EntityPath`.

---

## Validation final do Bloco 2

Antes de avançar para o Bloco 3 (escrever a Entry Function), confirme:

- [ ] ✅ Resource Group `rg-fifa2026-workshop-<iniciais>` criado em **East US 2**
- [ ] ✅ Service Bus namespace `sb-fifa2026-<iniciais>-<rand>` no tier **Standard**
- [ ] ✅ Queue `tickets-purchase` com **lock 30s** e **max delivery 10**
- [ ] ✅ DLQ `tickets-purchase/$DeadLetterQueue` visível no Service Bus Explorer
- [ ] ✅ Connection string copiada do **namespace**, **sem `EntityPath`**

> Guarde a connection string num lugar seguro e temporário (bloco de notas local). Você a usará no Bloco 3 ao configurar o `local.settings.json`.

---

## Apêndice — Teaser de IaC (se sobrar tempo)

Tudo o que você fez clicando pode ser declarado como código (Bicep). Não faremos isso agora (a ideia da F1 é entender cada peça pela mão), mas para referência, o equivalente é mais ou menos:

```bicep
resource sb 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-fifa2026-${iniciais}-${rand}'
  location: 'eastus2'
  sku: { name: 'Standard', tier: 'Standard' }
}
resource queue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: sb
  name: 'tickets-purchase'
  properties: {
    lockDuration: 'PT30S'
    maxDeliveryCount: 10
  }
}
```

> IaC vira tema de fases/discussões posteriores. Em F1, o objetivo é construir o modelo mental clicando — assim, quando virar código, você sabe o que cada linha provisiona.
