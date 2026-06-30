# F5 — Roteiro do vídeo de introdução (~5 min)

> **Vídeo de abertura** · Workshop "Living Lab Azure-Native" · Fase 5 de 6
> **Objetivo:** em ~5 min, fazer o aluno **querer** construir o chatbot e entender as 3 ideias-chave (function calling, MCP, key no proxy) antes da aula.
> **Tom:** direto, entusiasmado, sem jargão gratuito. Tudo dito aqui bate com o código real.
> **Story:** [2.5](../../stories/2.5.story.md) · **Leitura prévia:** [README](./README.md)

---

## Estrutura (5 blocos · ~5 min)

| Bloco | Tempo | Conteúdo |
|---|---|---|
| 1 | 0:00–0:45 | Gancho: o sistema ganha voz |
| 2 | 0:45–2:00 | A ideia: o LLM pede, o seu banco responde |
| 3 | 2:00–3:15 | MCP: o protocolo das ferramentas + as 3 tools |
| 4 | 3:15–4:15 | A key fica no servidor (segurança) + portabilidade |
| 5 | 4:15–5:00 | O que você vai fazer hoje + chamada para a ação |

---

## Bloco 1 — Gancho (0:00–0:45)

**[TELA: o app FIFA 2026 Tickets, navegando telas para achar um jogo]**

> Nas últimas quatro fases, você construiu um sistema de verdade: compra de ingressos, um gateway profissional, identidade segura e automação visual. Mas repare numa coisa: para saber se **tem ingresso pra Brasil x Argentina**, o usuário precisa navegar, filtrar, ler tabelas.

**[TELA: corta para um campo de chat, alguém digita "Tem ingresso pra Brasil x Argentina?"]**

> E se ele só pudesse... **perguntar**? Hoje, na Fase 5, o seu sistema vai ganhar **voz**. Você vai construir um chatbot que entende português e responde com **dados reais do seu banco**. Não com texto inventado — com os fatos do **seu** SQL.

---

## Bloco 2 — A ideia central (0:45–2:00)

**[TELA: animação simples — um LLM com um balão de pensamento]**

> Aqui está a primeira armadilha. Se você pergunta a um modelo de IA "puro" se tem ingresso pra Brasil x Argentina, ele **inventa** uma resposta. Ele não conhece o seu banco. Isso se chama alucinação — e num produto de verdade, é inaceitável.

**[TELA: o loop desenhado, passo a passo aparecendo]**

> A solução se chama **function calling**. Você entrega ao modelo, junto com a pergunta, um **catálogo de ferramentas**. E aí acontece a mágica:

> O modelo lê a pergunta, olha as ferramentas e **decide**: "preciso de um dado que não tenho — vou pedir a ferramenta `consultar_disponibilidade`". Mas atenção: ele **não executa** nada. Ele só **devolve a intenção**.

**[TELA: destaque "o SEU código executa"]**

> Quem executa de verdade é o **seu código** — que consulta o SQL e devolve o resultado ao modelo. Só então o modelo redige a resposta natural: "Sim, há 12 ingressos VIP a tanto...".

> Pense assim: o modelo é o **gerente** — entende a pergunta e escreve a resposta. A ferramenta é o **operário** — busca o fato no banco. O modelo delega o trabalho braçal e fica com o intelectual.

---

## Bloco 3 — MCP e as 3 tools (2:00–3:15)

**[TELA: logo do MCP / texto "Model Context Protocol"]**

> Agora, como descrever essas ferramentas de um jeito que qualquer modelo entenda? Com um protocolo aberto chamado **MCP — Model Context Protocol**. Pense nele como o **USB-C das ferramentas de IA**: um padrão de conector. Ele usa um formato simples, o JSON-RPC, com dois comandos que importam: **`tools/list`** — me diga quais ferramentas você tem — e **`tools/call`** — execute esta ferramenta.

**[TELA: três cartões, um por tool]**

> Você vai construir um **MCP Server** em .NET com **três ferramentas** sobre o seu banco:

> `consultar_disponibilidade` — tem ingresso pra esse jogo, e por quanto?
> `verificar_ingresso` — esse ID é válido, de quem é?
> `consultar_bracket` — quem está nas oitavas, nas quartas?

**[TELA: três linhas de código C#]**

> E o melhor: você **não escreve o protocolo à mão**. O SDK oficial — pinado na versão exata 1.4.0 — faz o trabalho pesado. Você só **decora um método C# com um atributo**, e ele vira uma ferramenta. Menos código, menos chance de inventar coisa que não existe na especificação.

---

## Bloco 4 — Segurança + portabilidade (3:15–4:15)

**[TELA: DevTools do navegador aberto, destacando o bundle JS]**

> Uma decisão de segurança que vale para a vida toda: a **chave da API da IA nunca pode ir no navegador**. Se você embutir a key no código do front, ela vai parar no bundle que **todo mundo** baixa — e qualquer um abre o DevTools e rouba.

**[TELA: diagrama front → proxy → provider]**

> Então a key fica num **proxy no servidor**. O front fala com o proxy — sem nunca conhecer a key — e o proxy injeta a chave e fala com o provedor oficial. Segredo de servidor mora **no servidor**. O front só conhece a URL.

**[TELA: env var trocando de gemini para groq, badge mudando]**

> E o bônus mais legal da fase: **portabilidade**. Porque o MCP desacopla o modelo dos dados, você troca de IA mudando **uma única variável de ambiente** — Gemini, Groq, Mistral. A mesma pergunta, o mesmo banco, cérebros diferentes. E isso é também o seu plano de resiliência: se um modelo cair na demo, você troca a variável e segue.

---

## Bloco 5 — Chamada para a ação (4:15–5:00)

**[TELA: checklist da aula aparecendo]**

> Então, hoje, em oito horas, você vai:

> Construir o **MCP Server** com as três ferramentas. Montar o **chatbot** em React. Conectar tudo ao **Gemini 2.0 Flash**, passando — como sempre — pelo **gateway** com identidade Entra. Proteger a key no **proxy**. E fechar com a demo de **portabilidade** entre três modelos.

**[TELA: capa do README / PORTAL-GUIDE]**

> Antes da aula, faça **duas coisas**: leia o **README** da Fase 5 — ele explica o loop com calma — e crie suas contas gratuitas, **sem cartão**, no Google AI Studio, no Groq e no Mistral.

**[TELA: frase âncora grande]**

> Lembre da frase da fase: **"O LLM raciocina; o MCP Server tem os fatos."** Nos vemos na aula. Vamos dar voz ao seu sistema.

**[FIM ~5:00]**

---

## Notas de produção

- **Duração-alvo:** 5:00. Se estourar, corte o detalhe do JSON-RPC no Bloco 3 (fica no README).
- **B-roll sugerido:** navegação no app (B1), animação do loop function calling (B2), três cartões de tools (B3), DevTools + diagrama do proxy (B4), troca de env var com badge mudando (B4), checklist (B5).
- **Termos a NÃO usar no vídeo** (deixe para README/aula): "Streamable HTTP", "secretref", "PIVOT", "WithToolsFromAssembly". O vídeo vende a ideia; a aula traz a precisão.
- **Fidelidade:** nomes de tools, "SDK 1.4.0", "Gemini 2.0 Flash", "proxy server-side", "env var de portabilidade" — todos reais. Não citar versões/recursos que não existam no código.
