# F6 — Roteiro do vídeo de introdução (~5 min)

> **Vídeo de abertura** · Workshop "Living Lab Azure-Native" · **Fase 6 de 6 — a final**
> **Objetivo:** em ~5 min, fazer o aluno **sentir** que chegou ao fim da jornada e entender as 3 ideias-chave (correlation ID / nó zero = Gateway YARP / tempo real via SignalR) antes da aula.
> **Tom:** celebrativo, mas direto. Esta é a fase final — reconheça a jornada, mas sem floreio vazio. Tudo dito aqui bate com o código real.
> **Story:** [2.6](../../stories/2.6.story.md) · **Leitura prévia:** [README](./README.md)

---

## Estrutura (5 blocos · ~5 min)

| Bloco | Tempo | Conteúdo |
|---|---|---|
| 1 | 0:00–0:50 | Gancho: você chegou na final — e o sistema é invisível |
| 2 | 0:50–2:10 | A ideia: o correlation ID e o nó zero (Gateway YARP) |
| 3 | 2:10–3:20 | Como funciona: App Insights + SignalR + a bolinha |
| 4 | 3:20–4:15 | O alerta: NÃO existe APIM aqui |
| 5 | 4:15–5:00 | O que você vai fazer hoje + chamada para a ação |

---

## Bloco 1 — Gancho (0:00–0:50)

**[TELA: montagem rápida das 5 fases anteriores — compra, gateway, login, n8n, chatbot]**

> Cinco fases. Seis microserviços. Quase quarenta horas. Você construiu um sistema de verdade: compra de ingressos, um gateway profissional, identidade segura, automação visual e um chatbot que conversa com dados reais.

**[TELA: o app rodando, alguém clica em "comprar". Tela some, fica preto.]**

> Mas repare numa coisa estranha. Quando você clica em "comprar", uma porção de coisas acontece por baixo — em seis processos diferentes, em meio segundo, e **ninguém nunca vê**. O sistema é invisível.

**[TELA: texto "Fase 6 — a final"]**

> Bem-vindo à fase final. Hoje você não vai construir uma nova peça. Hoje você vai construir o **espelho** de tudo: uma tela que mostra a sua compra atravessando o sistema inteiro, **ao vivo**.

---

## Bloco 2 — A ideia central (0:50–2:10)

**[TELA: seis caixinhas em fila, cada uma um processo diferente, com logs entrelaçados]**

> Aqui está o desafio. A sua compra passa por seis componentes, cada um com seu próprio log, no seu próprio tempo, misturado com outras compras acontecendo ao mesmo tempo. Como você sabe **quais** dessas milhares de linhas pertencem à **mesma** compra?

**[TELA: analogia da etiqueta de bagagem — uma mala com um código]**

> A resposta tem um nome: **correlation ID**. Pense na etiqueta de uma mala no aeroporto. Ela ganha um código único, e cada ponto da viagem registra "vi a mala XYZ". No fim, dá pra reconstruir a jornada inteira só filtrando por aquele código.

**[TELA: destaque no primeiro nó — "Gateway YARP — NÓ ZERO"]**

> E onde nasce esse código? No **Gateway YARP** — sim, o mesmo gateway que você construiu na Fase 2. Ele é o **nó zero**: toda request entra por ele, e é ele quem gera o correlation ID, injeta nas chamadas seguintes e devolve pro navegador. A partir daí, esse ID viaja, costurado, por todos os seis saltos — até o banco de dados.

---

## Bloco 3 — Como funciona (2:10–3:20)

**[TELA: três peças — App Insights, SignalR, a tela /flow]**

> Para transformar isso numa visualização, três peças se encaixam.

**[TELA: ícone do App Insights]**

> Primeira: o **Application Insights**. Todos os seis componentes mandam telemetria pra lá. Um serviço novo desta fase, o **FlowEvents**, consulta essa telemetria pelo correlation ID — usando o SDK oficial do Azure, o `Azure.Monitor.Query`, e Managed Identity, sem nenhuma senha embutida.

**[TELA: ícone do SignalR, setas saindo do servidor para o navegador]**

> Segunda: o **SignalR**. Em vez de o navegador ficar perguntando "já chegou? já chegou?", o servidor **empurra** os eventos em tempo real. E se o WebSocket falhar, tem um plano B automático: ele volta a perguntar a cada dois segundos. Nada quebra.

**[TELA: a tela /flow, a bolinha começando a andar pelos 6 nós]**

> Terceira: a tela **`/flow`**. Você clica numa compra, e uma bolinha animada percorre os seis nós, na ordem, cada um mostrando quanto tempo levou e se deu certo. É distributed tracing — na sua forma mais bonita.

---

## Bloco 4 — O alerta (3:20–4:15)

**[TELA: a palavra "APIM" aparecendo, e um grande X vermelho sobre ela]**

> Agora, um aviso importante — e preste atenção nele. Em muito material sobre Azure, você vai ver que o **APIM**, o API Management, é a porta de entrada que injeta o correlation ID. Isso é verdade... **em outras arquiteturas**.

**[TELA: "APIM" some, "Gateway YARP" aparece grande]**

> Aqui, no nosso workshop, **não existe APIM**. O nó zero é o **Gateway YARP** que **você** construiu na Fase 2. Se em algum momento você cruzar com a sigla "APIM" associada a este fluxo, é resquício de material antigo — corrija mentalmente para Gateway YARP.

> O código inteiro respeita isso. Tem até um teste automático que **falha** se alguém tentar colocar APIM como nó zero. Seis nós, Gateway YARP na frente. Sempre.

---

## Bloco 5 — Mão na massa + chamada à ação (4:15–5:00)

**[TELA: checklist dos passos do dia]**

> Então, o que você faz hoje? Você provisiona um Azure SignalR Service no plano gratuito, em modo **Default**. Deploya o serviço FlowEvents como Container App, com Managed Identity e permissão de leitura no Log Analytics. Configura a rota `/flow` no front. E aí vem o momento que fecha o workshop inteiro.

**[TELA: alguém faz uma compra, vai pro /flow, a bolinha percorre os 6 nós]**

> Você faz uma compra de verdade... vai pra tela `/flow`... e vê a bolinha percorrer os seis nós, em menos de trinta segundos, com o mesmo correlation ID do gateway até o SQL. O sistema inteiro, visível, pela primeira vez.

**[TELA: texto "Você chegou na final."]**

> Faça a leitura prévia antes da aula — ela explica observabilidade, tracing e como o correlation ID se costura pelos seis componentes. Você passou quarenta horas construindo algo real. Hoje você vai **ver** funcionar. Vamos fechar com chave de ouro. Te espero na aula.

**[FIM]**

---

> **Notas de produção:**
> - Duração-alvo: 5:00. Se estourar, encurte o Bloco 3 (as três peças podem ser ditas mais rápido).
> - O Bloco 4 (alerta APIM) é curto mas **inegociável** — é a correção de narrativa que define a fidelidade da fase.
> - Reforço de tom: celebrativo nos Blocos 1 e 5 (é a final), técnico e firme nos Blocos 2-4.
> - Termos que DEVEM aparecer corretos na narração e nas legendas: "Gateway YARP" (nunca APIM), "correlation ID", "Azure.Monitor.Query", "SignalR Service Mode Default", "Managed Identity", "Log Analytics Reader".
