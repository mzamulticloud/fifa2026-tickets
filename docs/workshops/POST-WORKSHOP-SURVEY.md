# POST-WORKSHOP SURVEY — Template de pesquisa de satisfação (NPS)

> **Artefato transversal** · Story [2.7](../stories/2.7.story.md) (AC-10) · Owner: **@analyst (Atlas)**
> **Para:** captura de NPS e feedback qualitativo ao fim do workshop "Living Lab Azure-Native" (SC-6 do EPIC-002).
> **Formato:** template de **Google Form** (copie as perguntas abaixo para um novo formulário). Tempo de resposta-alvo: ≤ 4 min.

---

## Configuração do formulário

- **Título:** Living Lab Azure-Native — Sua opinião
- **Descrição:** Leva ~4 minutos. Suas respostas são anônimas e moldam as próximas edições. Obrigado por construir junto.
- **Coleta de e-mail:** desligada (anônimo).
- **Barra de progresso:** ligada.

---

## Seção 1 — NPS (a métrica-chave, SC-6)

**P1. Em uma escala de 0 a 10, qual a probabilidade de você recomendar este workshop a um colega?**
- Tipo: **Escala linear** (0 = nada provável · 10 = extremamente provável)
- Obrigatória: sim
- > Cálculo do NPS: % Promotores (9-10) − % Detratores (0-6). Passivos = 7-8.

**P2. Qual o principal motivo da sua nota?**
- Tipo: Resposta longa (parágrafo)
- Obrigatória: sim

---

## Seção 2 — Conteúdo e ritmo

**P3. Como você avalia a profundidade técnica do workshop?**
- Tipo: Múltipla escolha
- Opções: Rasa demais · Um pouco rasa · Na medida · Um pouco densa · Densa demais

**P4. Como você avalia o ritmo (tempo por fase)?**
- Tipo: Múltipla escolha
- Opções: Muito lento · Lento · Na medida · Rápido · Muito rápido

**P5. Qual fase foi a mais valiosa para você?**
- Tipo: Múltipla escolha
- Opções: F1 — Mensageria (Service Bus + Functions) · F2 — Gateway YARP · F3 — Identidade (App Registration + MSAL.js) · F4 — Orquestração (n8n) · F5 — Chatbot (MCP + Gemini) · F6 — Flow Visualizer

**P6. Qual fase foi a mais difícil?**
- Tipo: Múltipla escolha (mesmas opções da P5)

**P7. Algum conceito ficou confuso ou faltou explicação?**
- Tipo: Resposta longa
- Obrigatória: não

---

## Seção 3 — Materiais didáticos

**P8. Quão úteis foram os materiais de apoio? (avalie cada um de 1 a 5)**
- Tipo: **Grade de múltipla escolha** (linhas = materiais · colunas = 1 a 5)
- Linhas: README (leitura prévia) · PORTAL-GUIDE (passo a passo) · Slides · Vídeo de introdução · Glossário
- Colunas: 1 (inútil) · 2 · 3 · 4 · 5 (essencial)

**P9. A pré-leitura (README) antes de cada aula ajudou?**
- Tipo: Múltipla escolha
- Opções: Sim, li e ajudou muito · Li parcialmente · Não li · Li mas não ajudou

---

## Seção 4 — Setup e pré-requisitos

**P10. O setup pré-workshop (Azure SQL Database, tooling) foi tranquilo?**
- Tipo: Múltipla escolha
- Opções: Sim, sem problemas · Tive dificuldades menores · Tive dificuldades sérias · Não consegui completar sozinho

**P11. Se teve dificuldade no setup, onde foi?**
- Tipo: Caixas de seleção (múltipla)
- Opções: Azure SQL Database / pré-condição de dados · Subscription/quotas Azure · Tooling local (.NET, Node, CLI) · App Registration / identidade · Outro

---

## Seção 5 — Aberto

**P12. O que você MAIS gostou?**
- Tipo: Resposta longa

**P13. O que você mudaria?**
- Tipo: Resposta longa

**P14. Você se sente capaz de aplicar algo deste workshop no seu trabalho? O quê?**
- Tipo: Resposta longa

---

## Pós-coleta — como ler os resultados

- **NPS (SC-6):** P1. Meta do epic registrada em EPIC-002. Acompanhe a distribuição, não só a média.
- **Sinais de ritmo/profundidade:** P3 + P4 — se concentrar nos extremos, recalibre a fase apontada em P6.
- **Materiais:** P8 + P9 — notas baixas em algum material alimentam a próxima iteração desta story (2.7).
- **Setup:** P10 + P11 — fricção recorrente em "Azure SQL Database" valida o peso da pré-condição bloqueante do [PRE-WORKSHOP-CHECKLIST](./PRE-WORKSHOP-CHECKLIST.md).
