# INTRO VIDEO SCRIPT — F3: Identidade Moderna (App Registration + MSAL.js + JWT no Gateway)

> **Vídeo de abertura da Fase 3** · Duração-alvo: **~5 minutos** · Assistir ANTES da aula (junto com o [README](./README.md)).
> **Tom:** acolhedor, direto, sem hype. Público: devs polyglot com background cloud, que fizeram F1 e F2. Sem OAuth2/.NET prévio exigido.
> **Formato:** apresentador em câmera + cortes para tela (diagramas/terminal/Portal). Marcações `[TELA: ...]` indicam o que mostrar.

---

## Estrutura e tempos

| Seção | Tempo | Conteúdo |
|---|---|---|
| 0. Cold open | 0:00–0:25 | O gancho: a porta da F2 está destrancada |
| 1. Boas-vindas + o que é a F3 | 0:25–1:05 | Onde estamos; vamos virar a chave |
| 2. Local vs federado | 1:05–2:05 | Você é dono do hotel (v1) vs confere o crachá (v2) |
| 3. OIDC + PKCE em 60s | 2:05–3:05 | Login com Google/Microsoft, sem secret no browser |
| 4. O guardião único | 3:05–4:05 | O gateway valida o JWT; propaga o oid |
| 5. App Registration, não External ID | 4:05–4:35 | A nota honesta sobre o que simplificamos |
| 6. Sua tarefa antes da aula | 4:35–5:00 | Ler o README + gateway F2 e Function F1 no ar |

---

## ROTEIRO

### [0:00–0:25] Cold open — o gancho

**[TELA: o `Program.cs` da F2 com a linha `// F3: aplicar .RequireAuthorization()` em destaque; rotas anônimas]**

> **Apresentador (em câmera):**
> "No fim da Fase 2, vocês construíram um gateway profissional e instalaram nele uma porta de segurança — o `AddJwtBearer`. Mas deixaram ela **destrancada**: as rotas eram anônimas, qualquer um passava sem token. Foi de propósito — identidade era o tema de hoje."

**[TELA: uma chave girando numa fechadura]**

> "Hoje a gente vira a chave. E vocês vão ver que adicionar identidade de verdade significa, surpreendentemente, escrever **menos** código de segurança."

---

### [0:25–1:05] Boas-vindas + onde estamos

**[TELA: trilha das 6 fases, F3 destacada, F1 e F2 marcadas como concluídas]**

> "Bem-vindo à **Fase 3** do Living Lab Azure-Native. Na F1 vocês fizeram o fluxo assíncrono; na F2, o gateway YARP na frente das Functions. Tudo isso continua **intocado**. A F3 é cumulativa: a gente liga a **identidade**."

**[TELA: três engrenagens — App Registration, MSAL.js, AddJwtBearer]**

> "São três peças que se encaixam: uma **App Registration** no seu tenant Entra, o **MSAL.js** no front fazendo o login moderno, e o **gateway** validando o token de verdade. Não precisa saber OAuth2 nem .NET de antemão. Se você já clicou em 'Login com Google' em algum site, já viveu isso como usuário — hoje você vê por dentro."

---

### [1:05–2:05] Local vs federado — a comparação que dá nome à fase

**[TELA: split screen — à esquerda "v1 local", à direita "v2 federado"]**

> "Tem dois jeitos de fazer login num sistema, e os dois convivem no nosso projeto de propósito. O **v1** — o backend Node que vocês já têm — faz tudo na mão: guarda a senha com bcrypt, emite o próprio token JWT, valida com a própria chave secreta."

**[TELA: ícone de hotel com recepção, cofre, segurança — tudo aceso]**

> "É como ser dono do hotel inteiro: recepção, cofre de chaves, segurança, lista de hóspedes. Funciona, mas é muita responsabilidade e muito risco. Se vazar seu banco, vazam as senhas."

**[TELA: ícone do hotel agora com a recepção terceirizada para o "Entra"]**

> "O **v2** — o que a gente liga hoje — faz o contrário: **delega** o login ao Entra, ao Google, ao GitHub. Você não guarda senha, não emite token, não implementa MFA nem reset. Você só **confere o crachá** que eles emitiram. Menos código de segurança, mais segurança. Essa é a grande lição da fase."

---

### [2:05–3:05] OIDC + PKCE em 60 segundos

**[TELA: "OAuth2 = o que você pode fazer" / "OIDC = quem você é"]**

> "O padrão que torna isso possível se chama **OIDC** — OpenID Connect. Ele fica em cima do OAuth2 e responde duas coisas: o que você pode fazer, e quem você é. É o que roda quando você faz 'Login com Google'."

**[TELA: diagrama do Authorization Code Flow + PKCE, passo a passo]**

> "Como o nosso front é um SPA — roda inteiro no browser — ele tem um desafio: **não tem onde esconder um segredo**. Tudo que ele guarda, o usuário também vê. Então usamos um truque chamado **PKCE**."

**[TELA: animação — code_verifier (cadeado) gerado, code_challenge (hash) enviado]**

> "O app gera um segredo descartável a cada login: manda só o **hash** dele pro Entra no começo, e o **valor original** só na hora de trocar o código por tokens. Mesmo que alguém intercepte o código no meio do caminho, não consegue trocar por tokens — falta a parte que nunca saiu do seu browser. Por isso um SPA público pode fazer OAuth2 com segurança, **sem nenhum client secret**. E o melhor: o **MSAL.js** faz toda essa criptografia por você. Você só chama `loginPopup`."

---

### [3:05–4:05] O guardião único

**[TELA: diagrama — Browser → Gateway (com cadeado) → Function → SQL]**

> "Quando o usuário loga, o MSAL pega um **access token** e o anexa em cada chamada como `Authorization: Bearer`. Esse token chega ao **gateway** que vocês construíram na F2. E é aqui que mora a frase do dia:"

**[TELA: texto grande — "O gateway é o guardião único da identidade."]**

> "A validação do token acontece em **um só lugar** — o gateway YARP. Não é o APIM, que a gente nem usa mais. Não é a Function. É o seu código C#, o `AddJwtBearer`. Ele confere quatro coisas: a **assinatura** — usando as chaves públicas do Entra, que você não tem como forjar; o **issuer**, pra garantir que veio do seu tenant; a **audience**, pra garantir que o token era pra sua app; e a **expiração**. Qualquer falha: **401**."

**[TELA: o transform do X-Entra-OID, com o `Headers.Remove` destacado]**

> "Passou na validação? O gateway extrai a identidade do usuário — o claim `oid` — e a repassa pra Function num header, o `X-Entra-OID`. E faz uma coisa esperta: **apaga qualquer `X-Entra-OID` que o cliente tenha tentado mandar**, antes de colocar o de verdade. Ou seja: você não consegue forjar a sua própria identidade. Só o gateway, depois de validar o token, escreve quem você é. A Function confia nisso — porque ninguém alcança ela sem passar pela portaria."

---

### [4:05–4:35] App Registration, não External ID — a nota honesta

**[TELA: "tenant workforce" vs "Entra External ID (CIAM)"]**

> "Uma honestidade importante: num produto B2C de verdade, com milhões de clientes externos, o provedor certo seria o **Microsoft Entra External ID** — um tenant separado, feito pra isso. Aqui no workshop, a gente usa o **tenant workforce** — aquele que já vem com a sua subscription Azure — pra **reduzir o atrito** de setup. Sem criar tenant novo, sem configurar user flows."

> "Mas atenção: os **conceitos são idênticos**. OIDC, PKCE, claims, scopes, App Roles — tudo igual. Quem domina aqui sabe migrar pro External ID em produção sem reaprender nada."

---

### [4:35–5:00] Sua tarefa antes da aula

**[TELA: checklist do README]**

> "Pra aproveitar as seis horas, faça três coisas. Um: **leia o README** desta fase — ele detalha o OIDC, o PKCE e os claims, e traz a tabela completa comparando o login local e o federado. Dois: confirme que o seu **gateway da F2** e a sua **Function da F1** estão no ar. Três: confirme que você consegue abrir o **Entra ID no Portal** e ver o seu Tenant ID."

**[TELA: logo do workshop + "F3 — Identidade Moderna"]**

> "Com isso, a gente chega na aula pronto pra construir: criar a App Registration, ligar o MSAL, e virar a chave do gateway. Nos vemos lá."

---

## Notas de produção

- **Duração real:** mire em 4:45–5:15. Se passar de 5:30, corte a seção 5 (External ID) para duas frases — ela é reforçada no README e no Bloco 1.
- **Recursos visuais-chave:** (1) a chave girando na fechadura (gancho), (2) o split hotel local vs recepção terceirizada, (3) a animação do PKCE (hash vai, verifier fica), (4) o `Headers.Remove` do anti-spoofing.
- **Continuidade de tom com F1/F2:** mesma voz, mesma honestidade sobre trade-offs (workforce ≠ External ID; menos código não é gambiarra, é delegação).
- **Não revelar valores reais:** ao mostrar o Portal, borre Tenant ID / Client ID reais (são por-aluno; o vídeo é genérico).
- **Fidelidade ao código:** o trecho do `Program.cs` e o `loginPopup`/`acquireTokenSilent` mostrados devem ser os reais (`src/Fifa2026.V2.Gateway/Program.cs`, `src/lib/authV2.ts`). Nada de pseudo-código inventado.
</content>
