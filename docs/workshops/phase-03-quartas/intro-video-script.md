# INTRO VIDEO SCRIPT — Quartas de Final: Identidade dois-mundos + Migração v1→CIAM

> **Vídeo de abertura do lab "Quartas de Final"** · Duração-alvo: **~5 minutos** · Assistir ANTES da aula (junto com o [README](./README.md)).
> **Tom:** acolhedor, direto, sem hype. Público: devs polyglot com background cloud, que fizeram a F1 e a F2. Sem .NET/OIDC prévio exigido.
> **Formato:** apresentador em câmera + cortes para tela (diagramas/terminal). Marcações `[TELA: ...]` indicam o que mostrar.

---

## Estrutura e tempos

| Seção | Tempo | Conteúdo |
|---|---|---|
| 0. Cold open | 0:00–0:25 | O gancho: a porta de identidade da F2 ganha a chave |
| 1. Boas-vindas + o que é o lab | 0:25–1:00 | Onde estamos; dois mundos de identidade |
| 2. A desambiguação (Connect/Entra ID/External ID) | 1:00–2:00 | Os três produtos "Entra" e suas URLs |
| 3. O cliente CIAM e o "só muda a string" | 2:00–3:10 | External ID, OIDC/PKCE, gateway issuer-agnóstico |
| 4. O admin no workforce (dois mundos) | 3:10–3:55 | Cliente no CIAM, funcionário no workforce |
| 5. A migração e a lição do bcrypt | 3:55–4:40 | Coexistência aditiva; o bcrypt não viaja |
| 6. Sua tarefa antes da aula | 4:40–5:00 | Ler o README + F1/F2 no ar |

---

## ROTEIRO

### [0:00–0:25] Cold open — o gancho

**[TELA: trecho do Program.cs da F2 com o AddJwtBearer configurado mas rotas anônimas, comentário "// F3: aplicar RequireAuthorization"]**

> **Apresentador (em câmera):**
> "No fim da Fase 2, a gente deixou uma porta **instalada, mas destrancada**. O gateway tinha o código de validação de identidade configurado — mas qualquer um ainda passava, sem login. Foi de propósito; identidade não era o tema. Agora é. Hoje a porta ganha a chave. E aqui vem a surpresa: **a chave tem duas fechaduras**."

**[TELA: duas portas lado a lado — "Cliente" e "Admin"]**

> "Porque um app de ingressos tem dois tipos de gente entrando: o **cliente**, que compra; e o **admin**, que opera. E eles entram por **mundos de identidade diferentes**."

---

### [0:25–1:00] Boas-vindas + onde estamos

**[TELA: trilha do mundial, lab "Quartas de Final" destacado, F1 e F2 concluídas]**

> "Bem-vindo às **Quartas de Final** do Living Lab Azure-Native. Na F1 você construiu o fluxo assíncrono; na F2, o gateway YARP com rate-limit, cache e CORS. Tudo isso continua **intocado**. As Quartas adicionam **identidade** na frente do gateway — e é um lab longo, então prepare-se: a gente vai fundo."

**[TELA: diagrama — Cliente→CIAM e Admin→workforce, ambos chegando no Gateway]**

> "Você não precisa saber .NET, nem OAuth, nem OIDC de antemão. Se você já colocou um 'Login com Google' num app, ou usou Auth0, Cognito, Firebase Auth — vai reconhecer tudo, só com nomes diferentes."

---

### [2] (1:00–2:00) A desambiguação

**[TELA: três caixas — Entra Connect, Entra ID, Entra External ID]**

> "Primeiro, a coisa mais importante do dia — e a que mais confunde. A Microsoft tem **três** produtos cujo nome começa com 'Entra'. Vamos separar agora, antes da aula, pra você não tropeçar."

**[TELA: linha 1 — Entra Connect, com um X]**

> "**Entra Connect**: é uma ponte que sincroniza um Active Directory que roda **na sua empresa** para a nuvem. Não é login. Esquece — só tô citando pra você não confundir pelo nome parecido."

**[TELA: linha 2 — Entra ID, com login.microsoftonline.com]**

> "**Entra ID**, o antigo 'Azure AD': é o **crachá do funcionário**. Identidade corporativa, B2B. O login acontece em **`login.microsoftonline.com`**. É o que o nosso **admin** vai usar."

**[TELA: linha 3 — Entra External ID, com ciamlogin.com, destacado]**

> "E **Entra External ID**: é o **cadastro do cliente**. CIAM — Customer Identity. O login acontece em **`ciamlogin.com`**. É o que o nosso **cliente** vai usar. A regrinha pra decorar: viu **`ciamlogin`**, pensa **cliente**; viu **`microsoftonline`**, pensa **funcionário**."

---

### [3] (2:00–3:10) O cliente CIAM e o "só muda a string"

**[TELA: bilheteria de um estádio, fila de torcedores se cadastrando]**

> "O cliente — o torcedor — se cadastra sozinho, na 'bilheteria pública'. Isso é o **External ID**. Ele pode entrar com a **conta Google**, ou com **email e um código de uso único**. E aqui tem uma sutileza linda de engenharia."

**[TELA: a palavra "PKCE" + um cadeado]**

> "O nosso frontend roda no navegador — então ele **não pode guardar nenhum segredo**, qualquer um abre o DevTools e vê. A solução é um padrão chamado **PKCE**: o app gera um segredo **temporário e descartável** a cada login. E o melhor: você **não implementa isso** — a biblioteca **MSAL** faz tudo. Você só aponta a authority."

**[TELA: diff de uma linha — authority: login.microsoftonline.com → <tenant>.ciamlogin.com]**

> "E é aqui que mora o coração técnico das Quartas. Pra trocar o login do cliente do mundo corporativo pro mundo CIAM, você muda **uma string**: a authority deixa de apontar pro `microsoftonline.com` e passa a apontar pro **`ciamlogin.com`**. Só isso. No frontend e no gateway."

**[TELA: o gateway com um escudo, validando um token]**

> "Por quê tão simples? Porque o gateway é **issuer-agnóstico**: ele valida qualquer token **por descoberta** — busca a chave pública do emissor automaticamente. Aceitar um novo emissor é **configuração, não código novo**. O gateway é o **guardião**: ele valida o JWT que o External ID emitiu, antes de deixar a compra passar."

---

### [4] (3:10–3:55) O admin no workforce — dois mundos

**[TELA: portaria de serviço de um estádio, funcionário com crachá]**

> "Agora o **segundo mundo**. O admin não se cadastra como cliente — ele já tem o **crachá corporativo**. Entra pelo **workforce**, no `login.microsoftonline.com`. Você vai construir o login dele com uma **App Role** chamada `Admin` — o papel que diz 'esse usuário é operador'."

**[TELA: dois tenants → um gateway]**

> "E o gateway aceita os **dois** emissores — o do cliente e o do admin — com **a mesma mecânica**. Dois `AddJwtBearer`, cada um apontando pra uma authority. É a prova viva de que o gateway é issuer-agnóstico: cliente e funcionário, validados igual, só com strings diferentes."

> "Esse é o desenho **canônico** de um produto B2C: cliente externo no CIAM, funcionário interno no workforce. Dois mundos coexistindo."

---

### [5] (3:55–4:40) A migração e a lição do bcrypt

**[TELA: uma linha da tabela users, com password (bcrypt) à esquerda]**

> "E o ápice do lab: a **migração**. Você tem usuários antigos — do tipo 'caseiro', com a senha guardada em **bcrypt**, na sua própria tabela. Você vai migrar esses usuários pro CIAM. Mas atenção: a migração é **aditiva**. Ela **vincula**, não apaga."

**[TELA: a mesma linha, agora com entra_oid (GUID CIAM) ADICIONADO à direita]**

> "E aqui está a lição mais importante: a senha **bcrypt não viaja** pro CIAM. O External ID **não importa** esse hash. O usuário cria uma credencial nova lá — Google ou código por email. E o bcrypt? Continua **intacto**, do lado. Isso não é um problema a contornar — **é a lição**: no mundo gerenciado, a Microsoft cuida da credencial; você só guarda o `oid`. Você nem tem mais um hash pra proteger."

**[TELA: query SQL retornando "COEXISTE (v1 bcrypt + v2 CIAM)"]**

> "No fim, você roda uma query e vê, **na mesma linha do banco**: a senha bcrypt do mundo antigo, e o identificador CIAM do mundo novo, vivos lado a lado. Mesmo humano, duas identidades, dois paradigmas. **Modernizar não exige destruir.** Esse é o coração das Quartas."

---

### [6] (4:40–5:00) Sua tarefa antes da aula

**[TELA: checklist com 2 itens]**

> "Antes da aula, duas coisas. Primeiro: leia o **README** das Quartas — ele aprofunda os três produtos Entra, o OIDC com PKCE, e a comparação completa entre a identidade caseira e a gerenciada. Esse é o lab onde a leitura prévia rende **mais**, porque a confusão de nomes é a maior armadilha. Segundo: garanta que a sua **Function da F1** e o seu **gateway da F2** estão no ar — a identidade entra na frente deles."

> "Faça isso, e a gente se vê na aula pronto pra abrir as duas fechaduras dessa porta. Até já!"

**[TELA: card final — "Quartas · Identidade dois-mundos · Leia o README · F1 + F2 no ar"]**

---

## Notas de produção

- **Duração real:** mire 4:45–5:15. Se estourar, corte primeiro a seção 4 (admin) — o README e a aula cobrem em detalhe; o cliente CIAM (seção 3) é o conceito mais importante.
- **B-roll sugerido:** a bilheteria pública vs portaria de serviço do estádio (a analogia dos dois mundos); o diff de **uma linha** da authority (`microsoftonline → ciamlogin`); a query SQL retornando `COEXISTE` (o momento mais memorável).
- **A desambiguação (seção 2) é a parte mais valiosa do vídeo** — não a apresse. É a mitigação nº1 da confusão de nomes.
- **Não mostrar código em detalhe** — snippets curtos no máximo (a authority, o `AddJwtBearer`). O aprofundamento é a aula (hands-on). Aqui é conceito e motivação.
- **Consistência técnica (AC-19):** os termos batem **exatamente** com o real — `<tenant>.ciamlogin.com`, `login.microsoftonline.com`, claim `oid`, App Role `Admin`, `users.password` (bcrypt), `users.entra_oid`, MSAL. **Não improvise** nomes diferentes na narração. Em especial: é **`ciamlogin.com`**, nunca `b2clogin.com` (legado) nem `microsoftonline.com` para o cliente.
- **Equilíbrio homegrown vs gerenciado:** na seção 5, não pinte o bcrypt/v1 como "ruim". É a comparação didática viva — os dois coexistem de propósito.
- **Legendas:** gere legendas (acessibilidade + público que assiste sem som).
- **CTA final fixo:** "Leia o README · F1 + F2 no ar" deve ficar visível nos últimos 5 segundos.
