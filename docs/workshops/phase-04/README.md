# F4 — Orquestração Visual: n8n self-hosted em Container Apps, disparado pelo consumer F1

> **Leitura prévia obrigatória** · Workshop "Living Lab Azure-Native" (40h) · Fase 4 de 6
> **Tempo estimado de leitura:** 30-40 min · **Faça ANTES da aula.**
> **Story:** [2.4](../../stories/2.4.story.md) · **Decisão de arquitetura:** [ADE-002](../../architecture/ade-002-mcp-pinning.md) (Invariante 4 — tag n8n `latest`) + [ADE-003](../../architecture/ade-003-v2-infrastructure-baseline.md) (Container Apps baseline) + [ADE-005](../../architecture/ade-005-identity-easy-auth.md) (Inv 3 — `entraOid`)
> **Continuidade:** parte cumulativa da [F1](../phase-01/README.md), [F2](../phase-02/README.md) e [F3](../phase-03/README.md) — o n8n se pendura no **consumer F1** que você já construiu, e recebe o `correlationId` que nasceu no **gateway YARP da F2** e o `entraOid` que você validou na **F3**.

---

## 0. Por que você está lendo isto antes da aula

Até agora, o seu fluxo de compra v2 terminava de um jeito silencioso. O gateway YARP (F2) recebia o request, validava o JWT (F3), publicava na fila; a `PurchaseConsumerFunction` (F1) lia a mensagem e gravava a compra no SQL. Fim. A compra acontecia, mas **nada acontecia depois dela** — nenhum e-mail, nenhum log de negócio, nenhuma notificação.

Na vida real, **depois de uma compra acontece muita coisa**: manda-se um e-mail de confirmação, dispara-se uma notificação ao financeiro, atualiza-se um CRM, talvez um Slack do time de operações. Essas ações pós-evento são o que chamamos de **orquestração** — e escrever cada uma delas à mão, no código do consumer, é uma forma de acumular dívida técnica rapidamente.

A Fase 4 introduz uma forma diferente de fazer isso: **automação de workflow low-code com o n8n**. Em vez de programar a notificação no C# do consumer, você vai:

1. Subir o **n8n self-hosted** num **Azure Container App** (o mesmo tipo de recurso que você já conhece do gateway YARP da F2).
2. **Desenhar visualmente** um workflow de notificação pós-compra — sem escrever código de orquestração: arrastar nodes, conectar, configurar.
3. Fazer o **consumer F1 disparar esse workflow** via webhook, **depois** de gravar a compra no SQL — sem bloquear o consumer e sem risco de mandar a mensagem para o DLQ se o n8n falhar.

> **A frase âncora da fase:** "O consumer grava a compra; o n8n cuida do que vem depois." A separação de responsabilidades é o ouro didático: o caminho crítico (gravar a compra) é do código; o pós-processamento (notificar) é do workflow visual, e ele é **best-effort** — se o n8n cair, a compra **continua gravada**.

Esta leitura cobre:

1. O que é **automação de workflow** e a diferença entre **low-code e código**
2. O que é o **n8n** e por que ele é uma boa escolha didática
3. **Container hosting na Azure**: por que Container Apps (e por que é o mesmo recurso do gateway YARP)
4. **Persistência com Azure Files**: por que um container stateless precisa de um disco montado
5. O **padrão fire-and-forget** e por que o consumer **não pode** bloquear nem ir ao DLQ por causa do n8n
6. Onde o n8n se encaixa no fluxo v2 (o quinto hop) e os **contratos exatos** (payload, env vars, App Setting)
7. Glossário e checklist de pré-aula

> **Pré-requisitos de conhecimento:** você fez a F1 (fluxo `POST /api/v2/purchase` → fila `tickets-purchase` → consumer → SQL), a F2 (gateway YARP com `X-Correlation-ID`) e a F3 (JWT validado, `oid` propagado como `X-Entra-OID`). Você programa em qualquer linguagem; **não exigimos** experiência prévia com n8n, automação de workflow ou containers. Se você já montou uma automação no Zapier, no Make ou no Power Automate, já viveu a ideia — aqui você a roda na **sua própria infraestrutura**.

---

## 1. O que é automação de workflow (e low-code vs código)

Um **workflow** é uma sequência de passos disparada por um evento. "Quando uma compra é gravada → verifique se é VIP → se for, mande um e-mail premium; se não, registre um log padrão." Cada passo é um **node**; a ligação entre eles é o **fluxo**.

Você pode escrever isso de duas formas:

- **Como código:** no consumer C#, você adicionaria `if (category == "VIP") { await SendEmail(...); } else { await Log(...); }`. Funciona — mas cada nova ação (Slack, CRM, webhook de terceiro) é mais código, mais deploy, mais teste, mais acoplamento no caminho crítico.
- **Como low-code (n8n):** você **desenha** o mesmo fluxo numa tela, arrastando nodes. Mudar a lógica (adicionar um node de Slack, trocar a condição) é **alterar o desenho** — sem recompilar, sem novo deploy do backend.

> **A intuição:** programar a orquestração no consumer é como soldar cada eletrodoméstico direto na parede. O n8n é uma **régua de tomadas**: o consumer só "liga na tomada" (dispara o webhook), e o que está plugado do outro lado pode mudar sem mexer na parede.

### Low-code não é "sem código" — é "menos código no lugar errado"

Low-code não significa que código desaparece. Significa que a **lógica de orquestração** sai do caminho crítico (o consumer) e vai para uma ferramenta especializada, visual e fácil de alterar. O consumer continua sendo código C# testado; ele apenas **delega** o pós-processamento.

| Eixo | Orquestração no código (consumer C#) | Orquestração low-code (n8n) |
|---|---|---|
| **Onde a lógica vive** | No `PurchaseConsumerFunction` | Num workflow visual, fora do backend |
| **Mudar a lógica** | Editar C# → recompilar → redeploy | Editar o desenho no n8n UI → salvar |
| **Adicionar uma ação** | Mais código no caminho crítico | Arrastar um node novo |
| **Quem pode mexer** | Quem programa em C# | Quem entende o negócio (visual) |
| **Acoplamento** | Alto (tudo no consumer) | Baixo (consumer só dispara o webhook) |
| **Risco no caminho crítico** | Uma falha na notificação pode afetar o processamento | Falha do n8n **não** afeta a compra (fire-and-forget) |

> **A leitura desta tabela:** o ganho não é "menos trabalho" — é **isolamento**. A compra (caminho crítico, que **não pode** falhar) fica no código robusto e testado; a notificação (pós-evento, que **pode** falhar sem drama) fica no n8n, fácil de evoluir.

---

## 2. O que é o n8n (e por que ele aqui)

O **n8n** (pronuncia-se "n-eight-n", de "nodemation") é uma plataforma de **automação de workflow open-source e self-hostable**. Você desenha workflows ligando nodes numa tela; cada node faz uma coisa (receber um webhook, fazer uma requisição HTTP, ramificar por uma condição, transformar dados). É o primo open-source e auto-hospedável do Zapier/Make.

Por que o n8n no nosso workshop, e não um SaaS de automação?

- **Self-hosted:** ele roda na **sua** infraestrutura (o seu Container App), não num serviço de terceiro. Isso casa com o fio condutor do workshop — você **opera** a sua plataforma na Azure.
- **Visual + reprodutível:** o workflow é desenhado na UI, mas pode ser **exportado como JSON** e versionado. No projeto, o workflow de referência está em [`infra/phase-04/post-purchase-notification.workflow.json`](../../../infra/phase-04/post-purchase-notification.workflow.json) — você o **importa** no n8n UI em vez de desenhar do zero (e ele é reproduzível entre aulas).
- **Container oficial:** existe a imagem `n8nio/n8n` no Docker Hub, então subir o n8n é subir um container — exatamente o que você aprendeu a fazer com o gateway YARP na F2.

### Nota de versão (importante e honesta): usamos `n8nio/n8n:latest`

> **Decisão de owner (2026-06-06, [ADE-002](../../architecture/ade-002-mcp-pinning.md) Invariante 4):** o container n8n usa a tag **`n8nio/n8n:latest`** — sempre a versão mais nova publicada no Docker Hub. Essa decisão **sobrepõe explicitamente** a recomendação original do blueprint (que pedia "não usar `latest`").

Isso é uma exceção consciente ao princípio geral de fixar versões. O **trade-off é honesto**: `latest` significa que **a reprodutibilidade entre aulas não é garantida** — turmas em datas diferentes podem rodar versões diferentes do n8n (que já teve **breaking change em uma major, a 2.0**). A **mitigação aprovada**: (a) o facilitador **revalida o workflow `post-purchase-notification` no início de cada aula** antes da demo ao vivo ([Task 10.2 da story](../../stories/2.4.story.md)); (b) cada aula é **gravada com a versão do dia**, então o material reflete exatamente o que rodou. Fontes oficiais: <https://hub.docker.com/r/n8nio/n8n/tags> · <https://github.com/n8n-io/n8n/releases> · <https://docs.n8n.io/2-0-breaking-changes/>.

> **Por que isso importa para você, aluno:** se a UI do n8n na sua aula estiver ligeiramente diferente dos prints deste guia, é esperado — o `latest` avançou. Os **conceitos** (webhook trigger, switch, HTTP request, set) são estáveis; a posição de um botão pode mudar.

---

## 3. Container hosting na Azure: por que Container Apps

O n8n vem como uma **imagem de container**. Para rodá-la na Azure, precisamos de um serviço de container hosting. Usamos o **Azure Container Apps (ACA)** ([ADE-003](../../architecture/ade-003-v2-infrastructure-baseline.md)) — e isto **não é novidade para você**: o gateway YARP da F2 já roda em Container Apps. Você está reusando um tipo de recurso que já domina.

### Por que Container Apps (e não AKS, App Service ou ACI)

| Opção | O que é | Por que (não) aqui |
|---|---|---|
| **Azure Container Apps** ✅ | Plataforma serverless de containers (Consumption plan, scale-to-zero) | Escolhida: simples, escala a zero (custo ~US$0 em repouso), HTTPS e ingress prontos, mesmo recurso do gateway YARP |
| Azure Kubernetes Service (AKS) | Kubernetes gerenciado | Poderoso demais — operar um cluster K8s é desproporcional para um único container de workshop |
| App Service (containers) | PaaS de apps web | Funciona, mas o ACA é o padrão do epic para containers (ADE-003) e escala a zero melhor |
| Container Instances (ACI) | Container avulso, sem orquestração | Sem ingress/scale geridos como o ACA; menos didático para "operar uma plataforma" |

A configuração do Container App do n8n já está versionada como **IaC** em [`infra/phase-04/n8n-containerapp.yaml`](../../../infra/phase-04/n8n-containerapp.yaml). Os números que importam (de [ADE-003](../../architecture/ade-003-v2-infrastructure-baseline.md) e das Dev Notes da story):

- **Plano:** Consumption (scale-to-zero quando ocioso)
- **Réplicas:** mínimo **0**, máximo **2**
- **Recursos:** **0.5 vCPU**, **1 Gi** de memória
- **Ingress:** **external**, **HTTPS only** (`allowInsecure: false`), **target port 5678** (a porta padrão do n8n)

> **Atenção ao scale-to-zero (vai aparecer na aula):** com `minReplicas: 0`, o n8n "dorme" quando ninguém o usa. A primeira requisição depois de dormir paga um **cold start** — o container precisa subir. Por isso o webhook do consumer tem timeout de 5s e o facilitador faz um **warm-up** antes da demo (ver [SPEAKER-NOTES](./SPEAKER-NOTES.md)).

---

## 4. Persistência com Azure Files: por que um container precisa de disco

Um container é, por natureza, **efêmero**: quando ele reinicia (um redeploy, um scale, uma nova versão do `latest`), **tudo que estava no disco interno dele se perde**. Para o n8n, isso seria um desastre — você desenharia o workflow, o container reiniciaria, e o workflow **sumiria**.

A solução é montar um **disco externo persistente** dentro do container. Na Azure, isso é um **Azure Files share** — um compartilhamento de arquivos gerenciado, montado no Container App como um volume.

No n8n, o estado (workflows, credenciais, banco SQLite) vive em **`/home/node/.n8n`**. Então montamos o Azure Files share **`n8n-data`** exatamente nesse caminho:

```
Azure Files share "n8n-data"  ──montado em──>  /home/node/.n8n  (dentro do container n8n)
```

Com isso, o n8n usa por padrão um banco **SQLite** (`DB_TYPE=sqlite`) gravado nesse diretório persistido — suficiente para o workshop, e seus workflows **sobrevivem** a restarts e redeployments (AC-4).

> **A intuição:** o container é um quarto de hotel — quando você faz check-out (restart), o quarto é limpo. O Azure Files é o seu **cofre na recepção**: o que você guarda nele continua lá mesmo trocando de quarto. Sem o cofre, todo restart apagaria os seus workflows.

> **Troubleshooting clássico (já mapeado na story):** se os workflows somem após um restart, é quase sempre o **Azure Files não montado corretamente** — confira o volume mount no Container Apps Environment.

---

## 5. O padrão fire-and-forget: por que o consumer não pode esperar pelo n8n

Esta é a decisão de engenharia mais importante da fase, e ela é **real no código** ([`N8nWebhookNotifier.cs`](../../../src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs) + [`PurchaseConsumerFunction.cs`](../../../src/Fifa2026.V2.Functions/Functions/PurchaseConsumerFunction.cs)).

### O problema

O `PurchaseConsumerFunction` é acionado por uma mensagem do Service Bus. Se ele **falhar** ao processar (lançar uma exceção), o Service Bus **reentrega** a mensagem; depois de tentativas demais (`maxDeliveryCount`), a mensagem vai para o **Dead Letter Queue (DLQ)**.

Agora imagine que o consumer chamasse o n8n **de forma bloqueante** e tratasse a falha do n8n como erro: se o n8n estivesse fora do ar (ou em cold start, ou apenas lento), o consumer falharia, a mensagem seria reentregue, e **uma compra perfeitamente gravada no SQL acabaria no DLQ por culpa de uma notificação** que é apenas best-effort. Isso é inaceitável — a compra é o que importa, a notificação é secundária.

### A solução: fire-and-forget

O consumer dispara o webhook do n8n de forma **"atira e esquece"**:

1. **Só dispara em `InsertOutcome.Inserted`** — ou seja, **apenas** quando a compra foi de fato gravada. Em `Duplicate` (mensagem reentregue) ou `CategoryNotFound`, o n8n **não** é chamado. Isso preserva a **idempotência**: a mesma compra processada duas vezes notifica o n8n **uma vez só**.
2. **Timeout de 5s** — via `CancellationTokenSource` encadeado ao token do host. O consumer não espera o n8n eternamente.
3. **Qualquer falha é capturada e logada, nunca re-lançada** — timeout, rede indisponível, DNS, HTTP non-2xx do n8n: tudo é engolido e vira um `LogWarning`. A mensagem do Service Bus **nunca** vai ao DLQ por culpa do n8n.
4. **Defesa em profundidade** — há `try/catch` **no notifier E no consumer**. Mesmo que o notifier viole o contrato e lance, o consumer captura. A compra já está no SQL; a mensagem do Service Bus está protegida.
5. **No-op silencioso se não configurado** — se o App Setting `N8N_WEBHOOK_URL` estiver vazio, o disparo é um **no-op** (apenas um `LogDebug`). Não é erro: o aluno pode estar numa fase anterior à F4 ou ainda não ter subido o n8n.

> **A frase âncora, de novo:** "O consumer grava a compra; o n8n cuida do que vem depois." A compra é **garantida** (idempotente, transacional). A notificação é **best-effort** (fire-and-forget). Misturar os dois níveis de garantia seria um erro de arquitetura.

### O que NÃO está no payload (e por quê) — nota de fidelidade (Art. IV)

O blueprint da AC-6 listava um campo **`amount`** (valor da compra) no payload do webhook. **Ele foi omitido de propósito.** A `PurchaseMessage` que viaja no Service Bus **não carrega valor monetário no corpo** — o `unit_price` só é resolvido no momento do INSERT no SQL (via JOIN em `ticket_categories`). Incluir um `amount` no webhook seria **inventar um dado que não existe na mensagem** (violação do Art. IV — No Invention). O payload usa apenas os campos **realmente presentes**: `correlationId`, `matchId`, `category`, `entraOid`.

> **Lição de carreira:** a tentação de "completar" um payload com um campo que parece fazer sentido é real. A disciplina de só enviar o que você **de fato tem** evita bugs sutis (um `amount: 0` ou `amount: null` que confunde quem consome o webhook). Quando o dado não está na fonte, não o invente.

---

## 6. Onde o n8n se encaixa: o quinto hop, e os contratos exatos

### 6.1 O fluxo completo v2 (delta sobre F1-F3)

O n8n é o **quinto hop** do fluxo de compra v2. Tudo antes dele já existia desde a F3; a F4 **adiciona** o disparo do webhook ao final.

```
[Frontend + MSAL.js (F3)]
   │ Authorization: Bearer <token>
   ▼
[Gateway YARP — Container App (F2)]
   │ injeta X-Correlation-ID (novo GUID se ausente)
   │ valida JWT · propaga X-Entra-OID
   ▼
[PurchaseEntryFunction (F1)]
   │ publica em Service Bus (tickets-purchase)
   │ correlationId + entraOid viajam no CORPO da mensagem
   ▼
[Service Bus — tickets-purchase]
   ▼
[PurchaseConsumerFunction (F1)]
   │ INSERT idempotente em SQL (entra_oid, correlation_id)
   │ SE InsertOutcome.Inserted  →  POST fire-and-forget ao webhook n8n   ◄── F4 (NOVO)
   ▼
[n8n — Container App (F4)]
   ├─ Webhook (purchase): recebe o payload
   ├─ Switch (VIP?): category == "VIP" → branch A; senão → branch B
   ├─ HTTP — VIP email (mock): POST httpbin.org/post (branch A)
   └─ Set — structured log: { correlationId, timestamp, notificationType, category }
```

> **O que NÃO mudou:** o gateway da F2, a validação de JWT da F3, o INSERT idempotente do consumer F1 — tudo intacto. A F4 **pendura** o n8n na ponta do consumer, sem alterar nada do que veio antes.

### 6.2 O workflow `post-purchase-notification` (4 nodes)

O workflow de referência (em [`infra/phase-04/post-purchase-notification.workflow.json`](../../../infra/phase-04/post-purchase-notification.workflow.json), que você **importa** no n8n UI) tem exatamente 4 nodes:

| # | Node | Tipo (n8n) | O que faz |
|---|---|---|---|
| 1 | **Webhook (purchase)** | `webhook` | Recebe `POST` no path `purchase`. O payload chega em `$json.body` |
| 2 | **Switch (VIP?)** | `switch` | Condição `{{ $json.body.category }} == "VIP"` → saída VIP; senão → saída padrão (`fallbackOutput`) |
| 3 | **HTTP — VIP email (mock)** | `httpRequest` | Branch VIP: `POST https://httpbin.org/post` com `notificationType: "vip-premium-email"` |
| 4 | **Set — structured log** | `set` | Branch padrão: monta `{ correlationId, timestamp, notificationType: "standard-log", category }` |

> **Atenção ao `$json.body`:** o n8n entrega o corpo do webhook em `$json.body` (não `$json` direto). Por isso as expressões do workflow leem `{{ $json.body.category }}`, `{{ $json.body.correlationId }}` etc. Se você desenhar o workflow do zero e esquecer o `.body`, as condições não baterão — importe o JSON de referência para evitar essa armadilha.

### 6.3 O payload do webhook (corpo JSON) — contrato exato

O consumer envia, no **corpo** da requisição `POST`, este JSON (definido em [`N8nWebhookPayload.cs`](../../../src/Fifa2026.V2.Functions/Data/N8nWebhookPayload.cs)):

```jsonc
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", // GUID — o id de hop, nasceu no gateway YARP (F2)
  "matchId": 1,                                            // int
  "category": "VIP",                                       // string — usada no Switch
  "entraOid": "7c9e6679-7425-40de-944b-e07fc1f90ae7"       // GUID? — claim oid (F3); null se sem identidade Entra
}
```

> **Origem dos campos (fidelidade — Art. IV):** todos saem do **corpo** da `PurchaseMessage` ([`Models/PurchaseMessage.cs`](../../../src/Fifa2026.V2.Functions/Models/PurchaseMessage.cs)), **não** das Application Properties do Service Bus. O `correlationId` e o `entraOid` viajam serializados no body da mensagem. **Sem `amount`** (ver seção 5).

### 6.4 As variáveis de ambiente do n8n (`N8N_*`) — rastreadas à doc oficial

O Container App do n8n é configurado por env vars. **Todas** foram conferidas contra a [documentação oficial do n8n](https://docs.n8n.io/hosting/environment-variables/) (AC-13 — anti-hallucination; não inventamos variável nenhuma). O passo-a-passo de onde defini-las no Portal está no [PORTAL-GUIDE](./PORTAL-GUIDE.md); aqui ficam os nomes para você reconhecê-los:

| Env var | Valor | Para que serve |
|---|---|---|
| `N8N_BASIC_AUTH_ACTIVE` | `true` | Liga o basic auth (AC-10 — n8n não sobe acessível sem auth) |
| `N8N_BASIC_AUTH_USER` | `admin` | Usuário do basic auth |
| `N8N_BASIC_AUTH_PASSWORD` | (gerado pelo aluno) | Senha — **secret**, nunca em texto no YAML versionado |
| `N8N_HOST` | FQDN do Container App | Host público do n8n |
| `WEBHOOK_URL` | `https://<fqdn>` | URL base que o n8n usa para construir a URL pública dos webhooks |
| `N8N_PROTOCOL` | `https` | Protocolo (HTTPS only — AC-10) |
| `N8N_PORT` | `5678` | Porta interna padrão do n8n |
| `DB_TYPE` | `sqlite` | Banco padrão; persistido via Azure Files (seção 4) |
| `GENERIC_TIMEZONE` | `America/Sao_Paulo` | Timezone para schedules/logs |

### 6.5 O App Setting que a Function consome: `N8N_WEBHOOK_URL`

Há **um** setting que vive **do lado da Function** (não do n8n), e ele é o elo que liga o consumer ao workflow:

| App Setting (na Function App) | Valor | Lido por |
|---|---|---|
| **`N8N_WEBHOOK_URL`** | `https://<n8n-fqdn>/webhook/purchase` | `N8nWebhookNotifier` via `IConfiguration["N8N_WEBHOOK_URL"]` |

Regras (NON-NEGOTIABLE, do código real):

- **Nunca hardcoded** — sempre App Setting (AC-6). O código lê `configuration["N8N_WEBHOOK_URL"]`.
- **Vazio = no-op silencioso** — se o setting não estiver configurado, o consumer apenas loga em `Debug` e segue. F4 é opcional para quem ainda não subiu o n8n.
- O valor só existe **depois** de você ativar o workflow no n8n UI e copiar a URL gerada do webhook trigger (`https://<fqdn>/webhook/purchase`). É um passo **manual** da aula (e está no [PORTAL-GUIDE](./PORTAL-GUIDE.md)).

> **A ordem importa:** primeiro suba o n8n → importe e **ative** o workflow → copie a URL do webhook → só então grave em `N8N_WEBHOOK_URL` na Function App. Se você gravar a URL antes de o workflow estar ativo, o webhook retornará **404** (workflow inativo) — armadilha clássica, já mapeada no troubleshooting da story.

---

## 7. Glossário rápido

| Termo | Significado curtíssimo |
|---|---|
| **Workflow** | Sequência de passos (nodes) disparada por um evento. |
| **Automação low-code** | Construir a lógica desenhando, não codando — a orquestração sai do caminho crítico. |
| **n8n** | Plataforma open-source de automação de workflow, self-hostable (primo do Zapier/Make). |
| **Node** | Cada passo de um workflow (webhook, HTTP request, switch, set...). |
| **Azure Container Apps (ACA)** | Plataforma serverless de containers na Azure (Consumption, scale-to-zero). Mesmo recurso do gateway YARP (F2). |
| **Scale-to-zero** | Réplicas chegam a 0 quando ocioso (custo ~US$0); a 1ª requisição depois paga **cold start**. |
| **Azure Files** | Compartilhamento de arquivos gerenciado, montado no container como volume persistente. |
| **`/home/node/.n8n`** | Diretório onde o n8n guarda workflows/credenciais/SQLite — montado no Azure Files. |
| **Webhook** | URL HTTP que dispara um workflow quando recebe um `POST`. No n8n: `https://<fqdn>/webhook/<path>`. |
| **Fire-and-forget** | Disparar uma ação sem esperar o resultado nem deixá-la quebrar o caminho crítico. |
| **DLQ (Dead Letter Queue)** | Fila de mensagens que falharam demais. O n8n **nunca** deve mandar uma compra ao DLQ. |
| **Idempotência** | Processar a mesma mensagem 2x produz 1 efeito. O consumer só notifica o n8n em `Inserted`. |
| **`correlationId`** | GUID de hop, nasce no gateway YARP (F2) e atravessa todo o fluxo até o log do n8n. |
| **`entraOid`** | Claim `oid` do usuário (F3), propagado até o payload do webhook. `null` se sem identidade Entra. |
| **`N8N_WEBHOOK_URL`** | App Setting da **Function** com a URL do webhook do n8n. Nunca hardcoded; vazio = no-op. |
| **`N8N_BASIC_AUTH_*`** | Env vars do **n8n** que ligam o basic auth (AC-10). |
| **basic auth** | Autenticação usuário/senha exigida pelo n8n (sem ela: HTTP 401). |

---

## 8. Checklist antes de entrar na aula

- [ ] Entendi a diferença entre **orquestração no código** e **automação low-code** e por que a notificação sai do caminho crítico (seção 1)
- [ ] Sei o que é o **n8n** e que rodamos `n8nio/n8n:latest` por **decisão de owner** — com a ressalva de versão (seção 2)
- [ ] Entendi por que **Container Apps** (mesmo recurso do gateway YARP da F2) e o que é **scale-to-zero / cold start** (seção 3)
- [ ] Sei por que um container precisa de **Azure Files** montado em `/home/node/.n8n` para não perder os workflows (seção 4)
- [ ] Consigo explicar o **fire-and-forget** e por que o n8n **nunca** pode mandar uma compra ao **DLQ** (seção 5) ← conceito-chave
- [ ] Sei que o webhook é disparado pelo **consumer F1** (não pelo gateway), **só em `Inserted`**, com payload `{ correlationId, matchId, category, entraOid }` (**sem `amount`**) (seções 5 e 6)
- [ ] Conheço o App Setting **`N8N_WEBHOOK_URL`** (na Function, nunca hardcoded) e a **ordem** de configuração (seção 6.5)
- [ ] Tenho minha **Function F1**, meu **gateway F2** e a **identidade F3** funcionando, e acesso ao Portal Azure com uma subscription ativa

Nos vemos na aula. Próximo artefato que você vai usar: [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md), a partir do Bloco 2 (criar o Container Apps Environment e o n8n).

---

## 9. Nota de continuidade — Reutilização do padrão webhook em F5+ (Fase B)

> **Para onde este padrão vai depois.** Esta nota foi adicionada quando a F5 ganhou uma extensão agêntica (Fase B, [Story 2.9](../../stories/2.9.story.md)). Você **não precisa** dela para a aula da F4 — ela existe para mostrar que o conceito que você aprende aqui **não morre na F4**: ele é reaproveitado mais à frente, num contexto diferente.

O **padrão de webhook fire-and-forget** que você acabou de estudar (seção 5) é a parte mais reutilizável desta fase. Na F4 quem dispara o webhook é o **consumer F1** (depois de gravar a compra). Na **Fase B da F5**, quem dispara um webhook para o n8n é uma **tool do chatbot** — a primeira tool de **ação** (`criar_alerta_ingresso`), acionada quando o usuário pede "me avise quando abrir ingresso para a final".

O importante: **as regras de engenharia são exatamente as mesmas** que você leu na seção 5 e nos contratos da seção 6 — só muda **quem aperta o gatilho**. Nada do conteúdo de F4 abaixo é re-explicado lá; a Fase B **referencia** este guia.

| Regra (origem nesta F4) | Como a Fase B (F5) a reaproveita |
|---|---|
| **URL via App Setting, nunca hardcoded** (seção 6.5 — `N8N_WEBHOOK_URL`) | A tool de ação lê **`N8N_ALERT_WEBHOOK_URL`** — um App Setting **distinto** (`_ALERT_`), apontando para outro workflow no n8n, para não misturar o fluxo de compra com o fluxo de alerta. |
| **Timeout de 5s** (seção 5) | Idêntico: `CancellationTokenSource` encadeado, `CancelAfter(5s)`. |
| **Falha capturada, nunca re-lançada** (seção 5 — fire-and-forget) | Idêntico: timeout/rede/non-2xx viram `false` + `LogWarning`; a tool retorna `{ registrado: false }` sem estourar exceção. Aqui não há DLQ (não é um consumer de fila), mas a filosofia "o secundário nunca derruba o principal" é a mesma. |
| **No-op silencioso se não configurado** (seção 6.5) | Idêntico: sem `N8N_ALERT_WEBHOOK_URL`, a tool responde `{ registrado: false, mensagem: "Webhook de alerta não configurado neste ambiente." }`. |
| **`correlationId` propagado** (seções 5 e 6) | Idêntico em espírito: a tool gera um **novo GUID** `correlationId` por disparo e o propaga em cada node do workflow do n8n (rastreabilidade rumo à F6). |

> **O que muda de fato na Fase B (e por isso ela é uma fase nova, não uma repetição):** na F4 o destino do webhook é um workflow **determinístico** (Webhook → Switch → HTTP/Set). Na Fase B o destino é um workflow **agêntico** — um nó **AI Agent** com seu próprio LLM, que decide sozinho **como** reagir ao alerta. O canal (webhook fire-and-forget) é o mesmo que você domina aqui; o que está do outro lado da tomada é que ficou mais inteligente. O detalhamento desse lado agêntico está no guia da [F5, seção "Fase B — A primeira mão"](../phase-05/README.md).
