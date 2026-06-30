# CONTENT TEMPLATE — Template editorial canônico do Workshop "Living Lab Azure-Native"

> **Artefato transversal** · Story [2.7](../stories/2.7.story.md) (AC-1 a AC-8) · Owner editorial: **@analyst (Atlas)**
> **O que é:** o contrato de voz, tom e estrutura que TODOS os 30 artefatos didáticos (6 fases × 5 artefatos) seguem. O molde de referência é a **Fase 1** (`phase-01/`), a primeira entregue e validada.
> **Princípio reitor:** o workshop tem **um único autor narrativo**. Mesmo que vários agentes produzam conteúdo, a voz deve ser indistinguível entre fases — como se um humano só tivesse escrito tudo.

---

## 0. Por que este template existe

A jornada de 40h é vendida como **produto editorial único**, não como 6 fases desconectadas. Para o aluno e o instrutor, a experiência precisa ser homogênea: a mesma voz, os mesmos callouts, a mesma profundidade, a mesma cadência "por quê antes do como". Este documento codifica os padrões já estabelecidos em F1-F3 (e herdados por F4-F6) para que qualquer artefato novo ou revisado seja imediatamente coerente com o conjunto.

---

## 1. Voz e tom (AC-1)

### 1.1 Voz

- **2ª pessoa singular, sempre:** "você cria", "você verá", "anote a URL", "o gateway que **você** construiu na F2".
- O instrutor/autor fala COM o aluno, não SOBRE ele. Nunca "o aluno deve" — sempre "você vai".
- Em SPEAKER-NOTES, a 2ª pessoa muda de alvo: o leitor é o **facilitador**, e o "você" passa a ser o instrutor ("retome a tabela", "peça pra turma adivinhar").

### 1.2 Tom

- **Didático firme:** explica o **"por quê"** antes do **"como"**. Cada conceito-chave abre com a motivação ("o problema que estamos resolvendo") antes do mecanismo.
- **Direto:** evite frases longas com 3+ subordinadas. Uma ideia por frase.
- **Honestidade técnica é didática:** nunca esconda trade-offs. "Mensageria não é grátis — paga-se em complexidade." "Não é 'YARP melhor que APIM' — é 'para aprender, construir; para muita produção, comprar'." A honestidade sobre limites é parte do ensino.
- **Sem hype:** acolhedor e direto, sem superlativos de marketing.

### 1.3 Dispositivos narrativos canônicos (herdar de F1-F6)

| Dispositivo | Como usar | Exemplo (do material real) |
|---|---|---|
| **Frase âncora da fase** | Uma frase curta que resume o ouro didático; repetida ao longo da fase | "Aceite rápido, processe depois." (F1) · "O LLM raciocina; o MCP Server tem os fatos." (F5) |
| **Continuidade cumulativa** | O header de cada README/fase relembra o que veio antes e o que esta fase acrescenta | "parte cumulativa da F1, F2, F3 — o n8n se pendura no consumer F1 que você já construiu" (F4) |
| **"O jeito errado que parece certo"** | Mostre a intuição ingênua, explique por que falha, então o jeito certo | SELECT-then-INSERT (TOCTOU) vs UNIQUE + INSERT-catch (F1) |
| **Regra de bolso** | Após uma tabela densa, destile em 1 frase memorizável por linha | "Trabalho a fazer → Service Bus; fila barata → Storage Queue; reagir a evento → Event Grid" (F1) |

---

## 2. Emoji policy e callouts (AC-1)

### 2.1 Frequência

- **Baixa:** 1-2 emojis por seção, no máximo. Emoji é **sinalização funcional**, nunca decoração.
- Proibido emoji em título de seção `##` (exceto o callout ⚠️ quando o título inteiro É um alerta — ex.: "## ⚠️ NÃO existe APIM aqui").
- Proibido emoji em prosa corrida ("isso é ótimo 🎉" — não).

### 2.2 Os 6 callouts canônicos (sincronizados com EPIC-001 / Story 1.5)

| Callout | Uso | Exemplo |
|---|---|---|
| 📋 | **Anote** — info que o aluno precisa salvar (URL, key, IP, connection string) | "📋 Anote o scope completo: `api://<client-id>/purchase.write`" |
| ✅ | **"Pronto quando:"** — bloco de validação ao fim de cada Step | "✅ Checkpoint: o RG aparece na lista de Resource Groups." |
| 💰 | **Custo** — valor real, nunca estimativa vaga | "💰 Standard tier ~US$0 em volume de workshop" |
| 🔐 | **Segredo** — onde armazenar (App Setting, Key Vault, nunca no repo) | "🔐 A connection string vive no Key Vault, referenciada via `@Microsoft.KeyVault(...)`" |
| ⚠️ | **Atenção** — armadilha conhecida (gotcha) | "⚠️ lock duration < tempo de processamento = reentrega infinita" |
| 💡 | **Explicação** — o "por quê" pedagógico de uma decisão | "💡 Por que UNIQUE filtrado? As compras v1 têm `correlation_id = NULL`" |

> Os callouts são renderizados como blockquote (`>`) iniciando pelo emoji + rótulo em **negrito**. Mantenha o padrão `> ⚠️ **Armadilha (...):** texto`.

---

## 3. Numbered options protocol (AC-1)

Em **qualquer escolha oferecida ao leitor**, apresente opções numeradas (1, 2, 3...), nunca prosa ambígua. Vale para:

- Caminhos alternativos: "Opção A — sqlcmd (CLI) / Opção B — Azure Data Studio".
- Ações dentro de um Step: passos numerados (1, 2, 3...).
- Provedores: "escolha Google **ou** GitHub" com passos numerados para o escolhido.

Isso dá ao aluno um ponto de decisão explícito e ao instrutor um roteiro determinístico.

---

## 4. Nomenclatura do re-escopo (AC-1 / AC-14 — OBRIGATÓRIA)

Toda referência aos componentes re-escopados (2026-06-03, ADE-004/ADE-005) DEVE usar a forma canônica. Detalhe completo em [`GLOSSARY.md`](./GLOSSARY.md) e na tabela de nomenclatura abaixo.

| Componente | Como escrever | NUNCA escrever (como componente ativo do fluxo v2) |
|---|---|---|
| Gateway F2 | "Gateway YARP" ou "Gateway em código (.NET + YARP)" | "APIM" / "Azure API Management" como o gateway do v2 |
| Identidade F3 | "App Registration (tenant workforce) + MSAL.js" | "External ID" / "CIAM tenant" / "tenant externo" |
| Claim de identidade | "claim `oid` (Object ID do Entra)" e coluna "`entra_oid`" | "GUID↔int mapping" / "ADE-001" |

### 4.1 Menções LEGÍTIMAS de APIM / External ID (PRESERVAR)

Estas formas NÃO são violações — são parte do currículo e devem ser mantidas:

1. **Build-vs-buy (F2):** "o equivalente gerenciado é o Azure API Management (APIM)" — a comparação build-vs-buy é o coração pedagógico de F2. A tabela de paridade APIM→YARP, o trade-off de custo/provisioning e a frase "para aprender, construir; para muita produção, comprar" são conteúdo essencial.
2. **Nota cultural (F3):** "em produtos B2C reais, o equivalente seria o Entra External ID — aqui usamos o tenant workforce para reduzir atrito." O aluno merece saber que External ID existe.
3. **Correção de narrativa (F6):** "NÃO existe APIM aqui; o nó zero é o Gateway YARP." Correções explícitas reforçam a fidelidade.
4. **Blade do Portal (F3):** "External Identities → All identity providers" é o **nome real do blade** do Azure Portal para federar Google/GitHub num tenant workforce — não é o produto "Entra External ID". É navegação correta.

> A regra: APIM/External ID podem aparecer como **comparação, nota cultural, correção ou nome de blade** — nunca como o componente que o fluxo v2 efetivamente usa.

---

## 5. Padrão por tipo de artefato

Cada fase produz 5 artefatos não-branch. Abaixo, a estrutura comum de cada um, com a F1 como exemplo de referência.

### 5.1 README pattern (AC-2)

Leitura prévia obrigatória do aluno (~25-50min). Estrutura:

1. **Header:** título da fase + "Leitura prévia obrigatória" + tempo de leitura + "Faça ANTES da aula" + link da story + ADEs relevantes + linha de **continuidade cumulativa** (o que vem antes / o que esta fase acrescenta).
2. **Seção 0 — "Por que você está lendo isto antes da aula":** motivação + lista numerada do que a leitura cobre + bloco "Pré-requisitos de conhecimento".
3. **Corpo conceitual:** seções numeradas, cada conceito abrindo pelo **por quê**; tabelas comparativas destiladas em regra de bolso; uma **frase âncora** marcada em blockquote.
4. **"O que vamos construir":** delta arquitetural com **diagrama ASCII** (componentes corretos pós re-escopo) + contratos exatos (endpoints, payloads).
5. **Glossário rápido:** 3-12 termos-chave da fase (tabela termo → significado curtíssimo).
6. **Checklist antes de entrar na aula:** caixas `[ ]` cobrindo os conceitos + pré-tarefa obrigatória + link do próximo artefato.

### 5.2 PORTAL-GUIDE pattern (AC-3)

Demo guiada no Azure Portal (o instrutor projeta, o aluno replica). Estrutura:

1. **Header:** "Bloco N do roteiro (Xmin)" + objetivo (1 frase do que se sai tendo) + story/ADE.
2. **Pré-requisitos** + **convenção de nomes** (tabela recurso → padrão → exemplo) + região do workshop.
3. **Steps numerados**, cada um com:
   - **Tempo estimado** no título (`## Step N — ... (Xmin)`).
   - Uma frase explicando o que o recurso é/faz.
   - **Numbered actions** (1, 2, 3...) com rótulos de UI em `**negrito**` ou `` `código` ``.
   - **`[PRINT N: descrição]`** como placeholder de screenshot (blockquote). Descrição clara = alt text futuro.
   - **✅ Checkpoint:** ("Pronto quando:") ao fim de cada Step.
   - **⚠️ Armadilhas** marcadas inline onde o aluno tropeça.

### 5.3 SPEAKER-NOTES pattern (AC-4)

Notas do facilitador, organizadas em **blocos** que somam ~6h (ou 8h nas fases longas F5). Estrutura:

1. **Header:** "Notas do facilitador" + nº de blocos + duração total + "use junto com" (links cruzados).
2. **Visão geral do dia:** tabela (# | Bloco | Tempo | Modo | Marco do aluno) + **mindset do facilitador** + **pré-checagem**.
3. **Por bloco:**
   - Título com `## BLOCO N — ... (Xmin · modo)`.
   - **Objetivo do bloco** (1 frase).
   - **Pontos a enfatizar** (3-5 bullets).
   - **Perguntas pra turma** (escolher 1-2).
   - **Armadilhas** (⚠️; sincronizado com o troubleshooting da fase).
   - **Se sobrar tempo (+Xmin)** / **Se faltar tempo (-Xmin)**.
   - **Transição** para o próximo bloco.

### 5.4 Slides pattern (AC-5)

Markdown convertível (Reveal.js / Marp), **~30-50 slides** por fase. Estrutura-mestre:

- **cover** → **agenda** → **conceito** (1 ideia por slide) → **demo** → **hands-on** → **validation** → **retro** → **próxima fase**.
- Separador de slide: `---` (linha horizontal). Notas de orador via `<small>` ou comentário.
- Acessível com teclado (navegação Reveal.js padrão).
- Diagramas com componentes **corretos pós re-escopo** (Gateway YARP como nó de entrada; em F6, Gateway YARP é o nó zero).

### 5.5 Intro-video-script pattern (AC-6)

Roteiro de narração em markdown puro, **~5min**. Estrutura de tempos:

| Seção | Tempo-alvo | Conteúdo |
|---|---|---|
| Cold open / hook | ~0:00–0:25 | O gancho (cenário real, ex.: pico de vendas da Copa) |
| Boas-vindas + onde estamos | ~0:25–1:05 | Posição na jornada das 6 fases |
| O problema / motivação | ~1:05–2:00 | Por que esta fase importa |
| A solução / preview hands-on | ~2:00–4:00 | O que veremos + preview do que se constrói |
| O que você vai construir | ~4:00–4:35 | Arquitetura da fase em 1 frase |
| Call to action | ~4:35–5:00 | Ler o README + pré-tarefa |

- Marcações **`[TELA: ...]`** indicam o corte de imagem.
- Texto da narração em blockquote (`>`), pronto para locução.
- Tom acolhedor, direto, sem hype — continuidade de voz com as fases anteriores.

---

## 6. Anti-hallucination (AC-12 / Art. IV — No Invention)

Toda afirmação técnica em qualquer artefato DEVE ser rastreável a uma destas fontes:

1. **Blueprint atualizado:** `docs/workshops/2026-blueprint-living-lab-azure.md`.
2. **ADEs:** `docs/architecture/ade-000..005`.
3. **Source code real:** `src/Fifa2026.V2.Functions/`, `src/Fifa2026.V2.Gateway/`, `src/Fifa2026.V2.FlowEvents/`, `Lovable/World Cup Tickets Hub/src/`.
4. **Documentação oficial:** Azure/Microsoft, n8n, Google (Gemini), MCP, etc.

Regras:
- NÃO invente features, comandos, paths, flags de CLI, nomes de tipo ou números.
- Se a turma perguntar "onde isso está no código?", o artefato deve permitir apontar o arquivo.
- Mantenha a **premissa de fidelidade** no topo dos SPEAKER-NOTES das fases com forte acoplamento ao código (modelo: F6).

---

## 7. Checklist de conformidade (use ao revisar/criar um artefato)

- [ ] Voz em 2ª pessoa (aluno no README/PORTAL/slides/vídeo; facilitador no SPEAKER-NOTES)
- [ ] "Por quê" antes do "como" em cada conceito-chave
- [ ] Frase âncora presente e repetida
- [ ] Emoji só em callouts; frequência baixa; nenhum decorativo
- [ ] Os 6 callouts (📋 ✅ 💰 🔐 ⚠️ 💡) usados conforme a tabela
- [ ] Numbered options em toda escolha
- [ ] Nomenclatura do re-escopo correta (Gateway YARP, App Registration + MSAL.js, `oid`/`entra_oid`)
- [ ] Menções a APIM/External ID apenas como comparação/nota/correção/blade (nunca componente ativo)
- [ ] Estrutura do tipo de artefato seguida (seção 5)
- [ ] Toda afirmação técnica rastreável (seção 6)
- [ ] Links cruzados entre artefatos da fase funcionando
