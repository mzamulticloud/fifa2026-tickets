# INTRO VIDEO SCRIPT — F1: Mensageria com Service Bus + Functions .NET

> **Vídeo de abertura da Fase 1** · Duração-alvo: **~5 minutos** · Assistir ANTES da aula (junto com o [README](./README.md)).
> **Tom:** acolhedor, direto, sem hype. Público: devs polyglot com background cloud, sem .NET prévio.
> **Formato:** apresentador em câmera + cortes para tela (diagramas/terminal). Marcações `[TELA: ...]` indicam o que mostrar.

---

## Estrutura e tempos

| Seção | Tempo | Conteúdo |
|---|---|---|
| 0. Cold open | 0:00–0:25 | O gancho (pico de vendas da Copa) |
| 1. Boas-vindas + o que é a F1 | 0:25–1:05 | Onde estamos na jornada |
| 2. O problema (síncrono) | 1:05–1:55 | Por que o v1 sofre no pico |
| 3. A solução (assíncrono) | 1:55–3:00 | "Aceite rápido, processe depois" |
| 4. O conceito que pega todo mundo | 3:00–4:00 | At-least-once → idempotência |
| 5. O que você vai construir | 4:00–4:35 | Arquitetura v2 em 1 frase |
| 6. Sua tarefa antes da aula | 4:35–5:00 | Rodar a migration + ler o README |

---

## ROTEIRO

### [0:00–0:25] Cold open — o gancho

**[TELA: animação simples de milhares de cliques "Comprar" pipocando numa tela]**

> **Apresentador (em câmera):**
> "Imagina o dia em que abrem as vendas dos ingressos da Copa de 2026. Cinquenta mil pessoas clicam em 'comprar' no mesmo segundo. Cada clique espera o banco de dados responder antes de liberar a tela. O que acontece?"

**[TELA: spinner girando, depois 'timeout']**

> "Spinner eterno. Timeout. Compra perdida. Hoje a gente vai resolver isso — e o nome da solução é **mensageria**."

---

### [0:25–1:05] Boas-vindas + onde estamos

**[TELA: trilha das 6 fases, F1 destacada]**

> "Bem-vindo à **Fase 1** do Living Lab Azure-Native. Esta é a porta de entrada da jornada: tudo o que a gente construir aqui — Service Bus, Azure Functions em .NET, idempotência, dead-letter queue — vira o **molde** que as Fases 2 a 6 vão herdar."

> "Não precisa saber .NET de antemão. Não precisa ser especialista em Azure. Se você programa e já mexeu em nuvem, está pronto. A gente vai construir um fluxo de compra **paralelo** ao que já existe — sem tocar no backend original. Lado a lado: o jeito antigo e o jeito novo."

---

### [1:05–1:55] O problema — comunicação síncrona

**[TELA: diagrama Browser → API → SQL, com a palavra "ESPERA" piscando]**

> "Hoje, comprar um ingresso é **síncrono**. O navegador manda o pedido e fica **esperando** a API gravar tudo no banco: validar, calcular preço, inserir. Só então recebe a resposta."

> "Funciona no dia a dia. Mas no pico, cada pedido segura uma conexão até o banco terminar. O pool esgota. E se o banco estiver lento ou em manutenção? A compra falha **na cara do usuário**, sem segunda chance."

**[TELA: destaque "acoplamento temporal"]**

> "Isso se chama acoplamento temporal: todo mundo precisa estar online e rápido, ao mesmo tempo. Frágil."

---

### [1:55–3:00] A solução — comunicação assíncrona

**[TELA: a frase grande "ACEITE RÁPIDO, PROCESSE DEPOIS"]**

> "A ideia central da mensageria cabe em quatro palavras: **aceite rápido, processe depois.**"

**[TELA: diagrama Browser → Entry Function → fila → Consumer → SQL]**

> "Em vez de fazer o usuário esperar, a gente aceita o pedido na hora e devolve um **recibo** — em menos de cem milissegundos. Esse recibo é um identificador único, o `correlationId`. A mensagem vai para uma **fila**, e do outro lado, no nosso próprio ritmo, um consumidor processa e grava no banco."

> "A fila funciona como um **amortecedor**. Chegou um pico de cinquenta mil pedidos? A fila segura. Banco caiu por um minuto? As mensagens esperam na fila e são processadas quando ele voltar. Nada se perde."

**[TELA: pergunta "e como sei se deu certo?"]**

> "'Mas se eu só recebo um recibo, como sei se a compra concluiu?' Por isso existe um segundo endpoint, o de **status**: você consulta com o `correlationId` e recebe `queued`, `processing`, `completed` ou `failed`. Simples."

---

### [3:00–4:00] O conceito que pega todo mundo

**[TELA: "AT-LEAST-ONCE"]**

> "Agora o conceito mais importante do dia — e o que mais confunde quem está começando. O Service Bus entrega cada mensagem **pelo menos uma vez**. 'Pelo menos uma vez' significa que a mesma mensagem **pode chegar mais de uma vez**."

**[TELA: animação — consumer grava no SQL, depois cai antes de confirmar, mensagem volta]**

> "Como assim? Imagine: o consumidor recebe a mensagem, grava a compra no banco... e cai um milissegundo antes de confirmar. O Service Bus pensa 'ninguém processou isso' e **reentrega**. Resultado: a compra é gravada **duas vezes**."

> "Isso não é um bug. É o preço de um sistema confiável: melhor entregar de novo do que perder. E isso leva a uma conclusão da qual não dá pra fugir..."

**[TELA: "at-least-once ⟹ idempotência"]**

> "...o seu consumidor **tem que ser idempotente**. Processar a mesma mensagem duas vezes precisa ter o mesmo efeito que processar uma. Na aula, você vai ver o jeito **errado** de fazer isso — que parece certo mas tem uma race condition — e o jeito **certo**, em que o banco de dados é o juiz da unicidade. Esse é o pulo do gato da Fase 1."

---

### [4:00–4:35] O que você vai construir

**[TELA: diagrama v2 completo, com as 3 Functions]**

> "Em resumo, hoje você vai construir três Azure Functions em .NET 8: uma que **recebe** a compra e publica na fila; uma que **consome** a fila e grava no banco com idempotência; e uma de **status**. Mais o Service Bus provisionado pelo Portal, a dead-letter queue para mensagens problemáticas, e um pipeline de CI/CD que faz deploy automático."

> "Tudo isso grava na **mesma** tabela do sistema antigo — a gente só adiciona duas colunas. O backend original continua intocado. É modernização incremental, do jeito que empresas de verdade fazem."

---

### [4:35–5:00] Sua tarefa antes da aula

**[TELA: checklist com 2 itens]**

> "Antes da aula, duas coisas. Primeiro: leia o **README** da Fase 1 — ele aprofunda tudo o que vimos aqui. Segundo, e essencial: **rode a migration** `phase-01.sql`. É um script idempotente que adiciona as duas colunas e o índice de unicidade no banco. Levamos isso para fora da aula de propósito, pra não gastar tempo de hands-on com isso."

> "O README tem o passo a passo e como confirmar que deu certo. Faça isso, e a gente se vê na aula pronto pra construir. Até já!"

**[TELA: card final — "F1 · Service Bus + Functions .NET · Leia o README · Rode a migration"]**

---

## Notas de produção

- **Duração real:** mire 4:45–5:15. Se estourar, corte primeiro a seção 2 (problema síncrono) — o README cobre.
- **B-roll sugerido:** diagramas animados das transições (síncrono → assíncrono; reentrega de mensagem). Terminal real com o `curl` retornando 202 é opcional mas reforça concretude.
- **Não mostrar código C# em detalhe** no vídeo — fica para a aula (live coding). Aqui é só conceito e motivação.
- **Consistência técnica:** os contratos citados (`/api/v2/purchase`, `correlationId`, status `queued|processing|completed|failed`) batem exatamente com o código. Não improvise nomes diferentes na narração.
- **Legendas:** gere legendas (acessibilidade + público que assiste sem som).
- **CTA final fixo:** "Leia o README · Rode a migration" deve ficar visível nos últimos 5 segundos.
