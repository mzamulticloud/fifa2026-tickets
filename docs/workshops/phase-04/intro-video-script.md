# INTRO VIDEO SCRIPT — F4: Orquestração Visual com n8n em Container Apps

> **Vídeo de abertura da Fase 4** · Duração-alvo: **~5 minutos** · Assistir ANTES da aula (junto com o [README](./README.md)).
> **Tom:** acolhedor, direto, sem hype. Público: devs polyglot com background cloud, que fizeram F1, F2 e F3. Sem n8n/automação/containers prévio exigido.
> **Formato:** apresentador em câmera + cortes para tela (diagramas/terminal/n8n UI/Portal). Marcações `[TELA: ...]` indicam o que mostrar.

---

## Estrutura e tempos

| Seção | Tempo | Conteúdo |
|---|---|---|
| 0. Cold open | 0:00–0:25 | O gancho: a compra grava... e nada acontece depois |
| 1. Boas-vindas + o que é a F4 | 0:25–1:05 | Onde estamos; vamos pendurar o "depois da compra" |
| 2. Low-code vs código | 1:05–2:05 | Soldar na parede (código) vs régua de tomadas (n8n) |
| 3. O n8n em 60s | 2:05–3:00 | Zapier self-hosted, num container na sua Azure |
| 4. Fire-and-forget | 3:00–4:05 | A compra nunca cai no DLQ por culpa do n8n |
| 5. A nota honesta: `latest` | 4:05–4:35 | Por que rodamos a versão mais nova (e o trade-off) |
| 6. Sua tarefa antes da aula | 4:35–5:00 | Ler o README + fluxo v2 (F1/F2/F3) no ar |

---

## ROTEIRO

### [0:00–0:25] Cold open — o gancho

**[TELA: o fluxo v2 desenhado — gateway → Service Bus → consumer → SQL — terminando num "fim" silencioso]**

> **Apresentador (em câmera):**
> "Até agora, o seu fluxo de compra v2 terminava de um jeito estranhamente quieto. O gateway validava o token, a fila entregava a mensagem, o consumer gravava a compra no SQL — e pronto. A compra acontecia, mas **nada acontecia depois dela**. Nenhum e-mail, nenhuma notificação, nenhum log de negócio."

**[TELA: ícones aparecendo — e-mail, Slack, CRM — com um ponto de interrogação]**

> "Na vida real, depois de uma compra acontece muita coisa. Hoje a gente constrói esse 'depois' — e de um jeito que **não** coloca a sua compra em risco."

---

### [0:25–1:05] Boas-vindas + onde estamos

**[TELA: trilha das 6 fases, F4 destacada, F1/F2/F3 marcadas como concluídas]**

> **Apresentador:**
> "Bem-vindos à Fase 4 do Living Lab Azure-Native. Vocês já têm a Function que processa a compra — a F1. O gateway YARP na frente dela — a F2. E a identidade, com o login moderno e o token validado — a F3."

**[TELA: a frase âncora aparece — "O consumer grava a compra; o n8n cuida do que vem depois."]**

> "Hoje a gente pendura o pós-processamento na ponta desse fluxo, usando uma ferramenta de **automação de workflow** chamada n8n. E a regra de ouro do dia é esta: o consumer grava a compra; o n8n cuida do que vem depois. Duas coisas separadas, com garantias diferentes."

---

### [1:05–2:05] Low-code vs código

**[TELA: split — à esquerda, código C# `if (VIP) SendEmail()...`; à direita, um canvas visual de nodes]**

> **Apresentador:**
> "Pensem em como você mandaria esse e-mail de confirmação. A forma óbvia é escrever no código do consumer: se for VIP, manda um e-mail premium; se não, registra um log. Funciona. Mas cada nova ação — um Slack, um CRM, um webhook de terceiro — é mais código, mais deploy, mais acoplamento, bem no meio do caminho crítico da sua compra."

**[TELA: animação de eletrodomésticos soldados na parede vs uma régua de tomadas]**

> "Programar a orquestração direto no consumer é como soldar cada aparelho na parede. O low-code é uma **régua de tomadas**: o consumer só 'liga na tomada' — dispara um sinal — e o que está plugado do outro lado pode mudar sem você nunca mais mexer na parede. Low-code não é 'sem código'. É tirar a lógica de orquestração do lugar errado."

---

### [2:05–3:00] O n8n em 60 segundos

**[TELA: a UI do n8n com um workflow de nodes conectados]**

> **Apresentador:**
> "A ferramenta que usamos é o **n8n**. Pensem nele como o Zapier ou o Make — aqueles montadores de automação visual — só que **open-source** e rodando na **sua própria infraestrutura**, não num serviço de terceiro."

**[TELA: o ícone do Docker + "n8nio/n8n" + um Container App na Azure]**

> "O n8n vem como um container. E adivinhem onde a gente vai rodar esse container? Em **Azure Container Apps** — exatamente o mesmo tipo de recurso onde vocês já subiram o gateway YARP na F2. Vocês já sabem fazer isso."

**[TELA: canvas com 4 nodes — Webhook → Switch → HTTP / Set]**

> "Vocês vão desenhar — ou melhor, importar — um workflow de quatro nodes: ele recebe um webhook, verifica se a compra é VIP, e ramifica: VIP ganha um e-mail premium mock; os outros, um log estruturado. Tudo visual."

---

### [3:00–4:05] Fire-and-forget — o coração da fase

**[TELA: diagrama — consumer grava no SQL, depois dispara seta tracejada "fire-and-forget" ao n8n]**

> **Apresentador:**
> "Agora a parte mais importante — e é uma decisão de engenharia, não de ferramenta. O consumer dispara o webhook do n8n de um jeito específico: **fire-and-forget**. Atira e esquece."

**[TELA: lista aparecendo item a item]**

> "Três regras. Um: ele só dispara quando a compra é **realmente gravada** — nunca numa duplicata, o que preserva a idempotência. Dois: tem um timeout de cinco segundos — o consumer não fica esperando o n8n. Três, e a mais importante: **qualquer** falha do n8n é capturada e logada, **nunca** re-lançada."

**[TELA: zoom numa mensagem indo para o "DLQ" com um X vermelho por cima]**

> "Por quê isso importa tanto? Porque se o consumer falhasse por causa do n8n, uma compra perfeitamente gravada no SQL acabaria no Dead Letter Queue — por culpa de um e-mail. Inaceitável. A compra é **crítica e garantida**. A notificação é **best-effort**. Se o n8n estiver fora do ar, a compra continua gravada; a notificação só não acontece desta vez. Misturar esses dois níveis de garantia seria um erro de arquitetura."

---

### [4:05–4:35] A nota honesta: por que `latest`

**[TELA: `n8nio/n8n:latest` em destaque]**

> **Apresentador:**
> "Uma nota honesta sobre a versão. A gente roda o n8n na tag `latest` — sempre a versão mais nova. Isso foi uma **decisão de owner**: a ideia é que cada turma experimente o n8n mais atual."

**[TELA: dois calendários de aulas diferentes apontando para versões diferentes]**

> "O trade-off, e a gente conta na cara: `latest` significa que a reprodutibilidade entre aulas não é garantida — turmas em datas diferentes podem rodar versões um pouco diferentes. Por isso o facilitador revalida o workflow no início de cada aula, e cada aula é gravada com a versão do dia. Se a UI no seu vídeo estiver um pouco diferente da sua tela, é isso — os conceitos não mudam."

---

### [4:35–5:00] Sua tarefa antes da aula

**[TELA: checklist do README aparecendo]**

> **Apresentador:**
> "Antes da aula, faça duas coisas. Primeiro: leia o README da F4 inteiro — tem o conceito de fire-and-forget, o contrato do payload e a tabela de variáveis. Segundo: confirme que o seu fluxo de compra v2 está no ar — Function F1, gateway F2 e identidade F3 — porque hoje a gente pendura o n8n na ponta dele."

**[TELA: a frase âncora de novo, e "Próxima: F5 — MCP + LLM"]**

> "Lembrem da frase do dia: o consumer grava a compra; o n8n cuida do que vem depois. Nos vemos na aula — e na F5, em vez de uma máquina, vai ser uma **IA** conversando com a sua plataforma. Até já."

**[FIM]**

---

> **Notas de produção:**
> - Onde aparecer a UI do n8n, use a **versão do dia** da gravação (coerente com a decisão de `latest`).
> - O diagrama do "quinto hop" pode ser reusado do [README seção 6.1](./README.md) para consistência visual.
> - Manter o tom da série (F1-F3): sem jargão gratuito, analogias concretas (régua de tomadas, cofre na recepção), honestidade sobre trade-offs.
