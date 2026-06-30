# INTRO VIDEO SCRIPT — F2: Gateway Profissional em Código com YARP

> **Vídeo de abertura da Fase 2** · Duração-alvo: **~5 minutos** · Assistir ANTES da aula (junto com o [README](./README.md)).
> **Tom:** acolhedor, direto, sem hype. Público: devs polyglot com background cloud, que fizeram a F1. Sem .NET prévio exigido.
> **Formato:** apresentador em câmera + cortes para tela (diagramas/terminal). Marcações `[TELA: ...]` indicam o que mostrar.

---

## Estrutura e tempos

| Seção | Tempo | Conteúdo |
|---|---|---|
| 0. Cold open | 0:00–0:25 | O gancho: a Function da F1 está exposta |
| 1. Boas-vindas + o que é a F2 | 0:25–1:05 | Onde estamos; o gateway entra na frente |
| 2. O que é um gateway | 1:05–2:05 | Reverse proxy, ponto único, a portaria |
| 3. A grande decisão: construir vs comprar | 2:05–3:15 | YARP em código vs APIM gerenciado |
| 4. O que você vai construir (os 6 níveis) | 3:15–4:15 | Rate limit, cache, transform, JWT — em C# |
| 5. O placeholder de segurança | 4:15–4:40 | JWT configurado, anônimo (F3 ativa) |
| 6. Sua tarefa antes da aula | 4:40–5:00 | Ler o README + Function F1 no ar |

---

## ROTEIRO

### [0:00–0:25] Cold open — o gancho

**[TELA: terminal fazendo um curl anônimo direto na Function da F1, retornando 202]**

> **Apresentador (em câmera):**
> "No fim da Fase 1, a gente deixou uma porta aberta. Qualquer pessoa, em qualquer lugar da internet, consegue chamar a sua Function direto — sem login, sem limite, sem controle nenhum. Veja: um `curl` anônimo e a compra entra."

**[TELA: destaque "Anonymous" piscando na chamada]**

> "Isso foi de propósito — segurança não era o tema da F1. Mas agora é. Hoje a gente coloca um **gateway profissional** na frente disso. E o melhor: a gente vai **construí-lo**, não comprá-lo."

---

### [0:25–1:05] Boas-vindas + onde estamos

**[TELA: trilha das 6 fases, F2 destacada, F1 marcada como concluída]**

> "Bem-vindo à **Fase 2** do Living Lab Azure-Native. Na F1 você construiu o fluxo assíncrono: Service Bus, Functions, idempotência. Tudo isso continua **intocado**. A F2 é **cumulativa**: a gente adiciona uma camada **na frente** das suas Functions — o **gateway**."

**[TELA: diagrama — Browser → [Gateway] → Function F1]**

> "Não precisa saber .NET de antemão, nem ser especialista em reverse proxies. Se você já configurou um Nginx, um Traefik ou um Ingress do Kubernetes, vai reconhecer os conceitos. Se nunca configurou, melhor ainda — você vai aprender vendo o código."

---

### [2] (1:05–2:05) O que é um gateway

**[TELA: animação de uma portaria de prédio, visitas passando]**

> "Pensa num **gateway** como a **portaria de um prédio**. Toda visita passa por ela. Identifica-se, respeita as regras da casa, ganha um crachá de rastreio, e só então é encaminhada ao apartamento certo. Os apartamentos não precisam ter, cada um, a sua própria portaria."

**[TELA: diagrama — cliente chama /purchase no gateway; gateway reescreve para /api/v2/purchase na Function]**

> "Tecnicamente, é um **reverse proxy**: um servidor que fica na frente dos seus backends e encaminha as requisições. O cliente fala com o gateway e **nunca vê a URL real** da Function. Você chama `/purchase`; por trás, o gateway reescreve para `/api/v2/purchase` e encaminha. Trocou a Function de lugar? O cliente nem percebe."

> "E como é o **ponto único de entrada**, é ali — em um lugar só — que moram as preocupações de borda: limite de chamadas, CORS, rastreio, e a validação de identidade. Em vez de espalhadas por cada serviço."

---

### [3] (2:05–3:15) A grande decisão: construir vs comprar

**[TELA: balança — de um lado "APIM (comprar)", do outro "YARP (construir)"]**

> "Aqui chega a pergunta de engenharia mais valiosa da fase — e da sua carreira: **vale construir, ou vale comprar?**"

> "O Azure tem um produto gerenciado pronto pra isso: o **API Management**, o APIM. É robusto, padrão de mercado. Mas, para este workshop, ele cobra um preço alto em dois sentidos."

**[TELA: dois números grandes — "~US$50-80" e "30-45 min"]**

> "Custa entre cinquenta e oitenta dólares, e leva de **trinta a quarenta e cinco minutos** só pra provisionar uma instância — inviável numa aula ao vivo. E o pior pra quem quer aprender: ele esconde tudo atrás de policies em XML e de um portal. Você não vê o mecanismo."

**[TELA: bloco de código C# curto, limpo]**

> "Então a gente vai pelo outro caminho: construir o gateway em **código C#**, usando uma biblioteca open-source da Microsoft chamada **YARP** — Yet Another Reverse Proxy. Custo? Praticamente **zero**. Deploy? **Segundos**. E você **lê e escreve** cada regra."

**[TELA: a frase "construir vs comprar — saber decidir é a habilidade"]**

> "Importante: isso **não** quer dizer que YARP é melhor que APIM. Em produção corporativa, o equivalente gerenciado deste gateway é justamente o APIM — e ele é a escolha certa em muitos casos. A gente constrói aqui pra **ver o que um gateway faz por dentro**. Saber **decidir** entre construir e comprar é a habilidade que você leva pra qualquer ferramenta."

---

### [4] (3:15–4:15) O que você vai construir — os 6 níveis

**[TELA: lista dos 6 níveis, com 0-2 destacados]**

> "Um gateway tem seis níveis de capacidade. Hoje você constrói os três primeiros em código e conversa sobre os outros."

**[TELA: ícones para cada capacidade — limite, cache, header, escudo]**

> "No coração da fase, o **Nível 2**: as preocupações transversais. Você vai escrever, em C#, quatro coisas que normalmente parecem mágica."

> "Um: **rate limiting**. Cinco chamadas por minuto; na sexta, o gateway responde quatro-vinte-nove, 'too many requests' — antes mesmo de incomodar a Function. Dois: **cache** de resposta, com um header `X-Cache` mostrando se veio do cache ou não. Três: o **CORS**, restrito ao domínio do seu frontend. E quatro: a injeção de um **`X-Correlation-ID`** — aquele identificador de rastreio que nasceu na F1, agora propagado desde a borda."

**[TELA: o curl do loop, mostrando 202 202 202 202 202 429]**

> "E sim, na aula você vai disparar seis chamadas seguidas e ver a sexta levar um 429 ao vivo. Isso é o `rate-limit-by-key` que o APIM venderia — escrito por você em cerca de dez linhas."

---

### [5] (4:15–4:40) O placeholder de segurança

**[TELA: uma porta instalada mas destrancada]**

> "Tem uma quarta capacidade que a gente **instala mas não liga ainda**: a validação de **JWT**, o token de identidade. O código `AddJwtBearer` vai estar lá, configurado apontando para o Entra ID — mas as rotas continuam **anônimas** nesta fase. Tem até um comentário no código dizendo: 'Fase 3: ativar a autorização aqui'."

> "Pensa assim: a porta da identidade está **instalada e destrancada**. A **Fase 3** é quem vira a chave. É a mesma lógica de um placeholder desabilitado no APIM — preparação proposital."

---

### [6] (4:40–5:00) Sua tarefa antes da aula

**[TELA: checklist com 2 itens]**

> "Antes da aula, duas coisas. Primeiro: leia o **README** da Fase 2 — ele aprofunda o que é um gateway, os seis níveis, e a tabela completa de paridade entre APIM e YARP. Segundo: garanta que a sua **Function da Fase 1 está no ar** e que você sabe a URL pública dela — o gateway precisa de um backend pra encaminhar."

> "Faça isso, e a gente se vê na aula pronto pra abrir o capô e construir o gateway por dentro. Até já!"

**[TELA: card final — "F2 · Gateway YARP em código · Leia o README · Function F1 no ar"]**

---

## Notas de produção

- **Duração real:** mire 4:45–5:15. Se estourar, corte primeiro a seção 2 (o que é gateway) — o README cobre em detalhe.
- **B-roll sugerido:** animação da portaria do prédio; a balança "construir vs comprar"; o terminal real do loop de 6 chamadas terminando em 429 (reforça concretude e é o momento mais memorável).
- **Não mostrar código C# em detalhe** no vídeo — mostre snippets curtos no máximo. O aprofundamento é a aula (live coding). Aqui é conceito e motivação.
- **Consistência técnica:** os termos citados batem **exatamente** com o código real — rota `/purchase` reescrita para `/api/v2/purchase`, rate limit 5/min → 429, header `X-Cache`, header `X-Correlation-ID`, `AddJwtBearer` anônimo, YARP/`Yarp.ReverseProxy`. Não improvise nomes diferentes na narração.
- **Equilíbrio APIM vs YARP:** na seção 3, mantenha a honestidade — não pinte o APIM como vilão. "Para aprender, construir; para muita produção, comprar."
- **Legendas:** gere legendas (acessibilidade + público que assiste sem som).
- **CTA final fixo:** "Leia o README · Function F1 no ar" deve ficar visível nos últimos 5 segundos.
