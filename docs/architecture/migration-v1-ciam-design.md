# Design técnico — Migração de identidade `users` v1 → Microsoft Entra External ID (CIAM)

> **Tipo:** Documento de design técnico (mecanismo + decisão de schema da migração). Insumo BLOQUEANTE do build da story 2.11 (Task 6.1).
> **Status:** ✅ Recomendação fechada por @data-engineer (Dara)
> **Date:** 2026-06-25
> **Author:** Dara (@data-engineer)
> **Scope:** ADE-007 Invariante 6 (migração `users` v1 → CIAM, aditiva e hands-on). Materializa-se no script de migração do lab "Quartas de Final" + no PORTAL-GUIDE/SPEAKER-NOTES (estes últimos são do @analyst — este doc é só o **como** técnico).
> **Delegação (matriz de autoridade):** ADE-007 §Authority e story 2.11 Task 6.1 delegam o **mecanismo exato** da migração a @data-engineer. O **o quê** (importar/vincular `users` v1 → CIAM, aditivo) foi fixado pelo owner (handoff §3); este doc fixa o **como**.
> **Rastreabilidade (Art. IV):** toda decisão deriva de ADE-007 (Inv 2/3/4/6), ADE-000 (Inv 1/2), do schema/código real verificado (`schema.sql`, `phase-01.sql`, `phase-03.sql`, `PurchaseRepository.cs`, `auth.js`) e do handoff §3. Itens sem fonte estão marcados `[AUTO-DECISION]` com justificativa.

---

## 0. Verificação do código real (o que a Dara confirmou de fato, não por ouvir dizer)

Antes de decidir, conferi o schema e o pipeline reais. Achados (com evidência):

| Fato verificado | Evidência | Implicação |
|---|---|---|
| `entra_oid` está em **`purchases`** (não em `users`) | `phase-03.sql` linha 49: `ALTER TABLE dbo.purchases ADD entra_oid UNIQUEIDENTIFIER NULL` | A AC-9/AC-10 estão corretas: a coluna já existe em `purchases`. |
| Índice é **filtrado, NÃO-unique** | `phase-03.sql` linhas 59-61 (`CREATE INDEX ... WHERE entra_oid IS NOT NULL`) | Um mesmo `oid` repete entre várias compras do mesmo usuário — **correto e intencional** (comentário linhas 29-32). |
| `purchases.entra_oid` é **transacional** (por compra), preenchido só quando uma compra **v2 nova** acontece | `PurchaseRepository.cs` linhas 59-75 (`INSERT INTO dbo.purchases ... @EntraOid`) | **Ponto crítico** (ver §1): a coluna registra "quem fez ESTA compra", não "este usuário tem identidade CIAM". |
| `users` tem `id INT IDENTITY`, `email NVARCHAR(255)` **UNIQUE**, `password NVARCHAR(255)` (bcrypt), `role` | `schema.sql` linhas 16-29 | A chave natural de match v1↔CIAM é **`email`** (UNIQUE). A coluna do hash chama-se **`password`** (não `password_hash` como a tabela comparativa da story assume — divergência menor, §6). |
| bcrypt = `bcryptjs`, **10 rounds** | `auth.js` linha 42 (`bcrypt.hash(password, 10)`) | Hash bcrypt **não é exportável** para o CIAM (§3). |
| `users` **NÃO tem** coluna `entra_oid` hoje | `schema.sql` users (16-29) + grep | Se quiséssemos `users.entra_oid`, seria DDL **nova** (a tensão que o @po levantou). |
| McpServer só **lê** o `oid` do header para log mascarado; nunca persiste mapping | `EntraOidContext.cs` (todo o arquivo) | Não há outra tabela de mapping no caminho. `oid` é a chave direta (ADE-007 Inv 3). |

> **Divergência detectada e reportada (Art. IV):** o pipeline grava `entra_oid` **na compra**, então um usuário v1 que **migra mas ainda não comprou no v2** não tem onde seu `oid` aterrissar de forma durável em `purchases`. A story AC-16 diz "mesmo usuário tem `user_id` (v1) E `entra_oid` (CIAM) na mesma `purchases` **(ou na tabela `users`, conforme o mecanismo definido por @data-engineer)`**" — ou seja, a própria story **já previu** que eu pudesse precisar de `users.entra_oid`. Isso **não** é contradição com a ADE-007; é exatamente o ponto que ela delegou. Trato isso de frente em §1.

---

## 1. DECISÃO DE SCHEMA (a mais importante)

### A pergunta

`entra_oid` fica **só em `purchases`** (zero delta, como AC-9/AC-10 afirmam) OU ganha uma coluna **`users.entra_oid`** (nova migration, que o @po alertou que quebraria o "schema delta zero" anunciado)?

### A tensão técnica real

As duas colunas modelam coisas **semanticamente diferentes**:

- **`purchases.entra_oid`** = "o `oid` CIAM de quem executou **esta compra v2**". É um atributo do **evento de compra**. Repete por compra. É o que o pipeline `oid → X-Entra-OID → entra_oid` grava (Inv 2/3). **Não é** uma propriedade do cadastro do usuário.
- **`users.entra_oid`** (não existe hoje) = "o `oid` CIAM que corresponde a este **registro de usuário v1**". Seria um atributo da **identidade/cadastro**. Único por usuário. É o que uma **migração de cadastro** naturalmente produz.

A migração do lab (Inv 6) **migra cadastros** (`users` v1 → CIAM), não compras. O artefato natural de uma migração de cadastro é um **vínculo durável usuário↔oid**, e o lugar canônico desse vínculo é a tabela de usuários — não a de compras.

### RECOMENDAÇÃO

**`entra_oid` em `purchases` permanece como está (zero delta — confirmado). E ADICIONO `users.entra_oid` via nova migration aditiva e idempotente (`phase-04-ciam-link.sql`).** As duas coexistem com papéis distintos: `purchases.entra_oid` continua transacional (não muda); `users.entra_oid` é o **alvo do vínculo da migração de cadastro**.

> **Justificativa em 1 frase:** a migração de **cadastro** (Inv 6) precisa de um vínculo **durável e único por usuário** que sobreviva independentemente de o usuário ter comprado no v2 — e isso é uma propriedade do `users`, não do evento `purchases`; manter o `oid` só em `purchases` faria a "coexistência do mesmo usuário" depender de uma compra v2 ter acontecido, descaracterizando o ápice didático da AC-16.

### Por que NÃO "só `purchases`" (rejeitando a leitura literal de zero-delta)

Se ficássemos só com `purchases.entra_oid`:
- Um usuário v1 migrado que **ainda não comprou no v2** não teria `oid` em lugar nenhum durável → a query da AC-16 ("mesmo usuário: bcrypt + oid lado a lado") **não retornaria nada** para ele. A prova didática dependeria de forçar uma compra v2 logo após migrar — frágil e confuso em sala.
- Para "ligar o `oid` ao usuário certo" via `purchases`, eu teria que **fazer UPDATE em linhas de compra históricas** por join de email — ou seja, **reescrever dados transacionais antigos** com um `oid` que aquelas compras **não tinham** quando ocorreram. Isso falsifica o histórico (uma compra v1 de 2026-05 não foi feita com identidade CIAM) e contraria o próprio comentário de `phase-01/03` ("linhas v1 históricas não têm identidade Entra"). **Inaceitável** — viola a honestidade do dado.

A coluna `users.entra_oid` resolve ambos: vínculo durável, único por usuário, sem tocar histórico transacional.

### Por que isso NÃO quebra a ADE-007 nem a ADE-000 (resposta ao alerta do @po)

1. **ADE-007 Inv 3** diz "nenhuma DDL nova é introduzida **por esta ADE**". Verdade — a ADE-007 trata da **troca de provedor de identidade** (workforce→CIAM), e para *isso* o schema é zero-delta (a coluna `entra_oid` de `purchases` já existe e não muda). A **migração de cadastro hands-on** (Inv 6) é um eixo **diferente**, e a própria Inv 6 + Task 6.1 da story **delegaram explicitamente a mim** decidir se há coluna adicional ("`...se @data-engineer definir assim`" — story Task 6.1/6.2/7.1, e AC-16 "`ou na tabela users, conforme o mecanismo definido por @data-engineer`"). Logo, adicionar `users.entra_oid` é uma decisão **dentro do mandato delegado**, não uma violação da Inv 3.
2. **ADE-000 Inv 2** (schema aditivo e idempotente) é **respeitada**: a nova migration é `ALTER TABLE ADD COLUMN` + `CREATE INDEX` com `IF NOT EXISTS`, nada destrutivo. É exatamente o mesmo padrão de `phase-01.sql` e `phase-03.sql`.
3. **ADE-000 Inv 1** (coexistência v1/v2 lado a lado) é **reforçada**, não quebrada: o usuário v1 mantém `id`+`password` (bcrypt) e ganha `entra_oid` ao lado, **na mesma linha de `users`**. É a comparação lado-a-lado em sua forma mais limpa.

### Impacto honesto no "schema delta zero" anunciado

O slogan "schema delta ZERO" da AC-9/AC-10 era verdadeiro **para o eixo de provedor de identidade** (troca workforce→CIAM no fluxo de compra). Ele **deixa de ser literalmente verdadeiro para o lab inteiro** quando incluímos a migração de cadastro hands-on, porque essa migração introduz **uma** coluna aditiva. **Não escondo isso:** §6 lista exatamente quais ACs o @sm precisa ajustar. O delta é mínimo (1 coluna + 1 índice filtrado, aditivo/idempotente), e é a escolha tecnicamente honesta.

### Migration recomendada (idempotente, aditiva — segue o padrão de `phase-03.sql`)

```sql
-- =====================================================
-- Migration: phase-04-ciam-link.sql — vínculo durável usuário v1 ↔ CIAM oid
-- Story: 2.11 — Quartas (migração users v1 → CIAM, ADE-007 Inv 6)
-- =====================================================
-- Adiciona users.entra_oid: o Object ID (oid) do CIAM correspondente a ESTE
-- registro de usuário v1. É o ALVO durável do vínculo da migração de cadastro.
--
-- DIFERENÇA vs purchases.entra_oid (phase-03.sql):
--   - purchases.entra_oid = oid de quem fez AQUELA compra v2 (transacional, repete).
--   - users.entra_oid     = oid do CADASTRO CIAM vinculado a este usuário (único
--                           por usuário). Sobrevive mesmo sem compra v2.
--
-- ADE-000 Inv 2 (aditivo apenas): só ADD COLUMN + CREATE INDEX. Nenhum DROP/ALTER.
-- NÃO apaga users, NÃO toca password (bcrypt v1 intacto — Inv 4 / Inv 6).
-- IDEMPOTENTE (IF NOT EXISTS): rodar 2x não duplica coluna nem índice.
-- Executar PRÉ-WORKSHOP (cria a coluna vazia); o PREENCHIMENTO é o passo hands-on.
-- =====================================================
SET NOCOUNT ON;

-- ============ users.entra_oid (NULL — quem nunca migrou fica NULL) ============
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'entra_oid' AND Object_ID = Object_ID(N'dbo.users')
)
    ALTER TABLE dbo.users ADD entra_oid UNIQUEIDENTIFIER NULL;
GO

-- ============ UQ_users_entra_oid (UNIQUE filtered) ============
-- Cada oid CIAM mapeia para NO MÁXIMO um usuário v1 (1:1). UNIQUE filtrado:
-- aplica a unicidade só onde entra_oid IS NOT NULL (vários NULL convivem — quem
-- não migrou). Isto é o que garante a IDEMPOTÊNCIA do link (§4): re-rodar com o
-- mesmo oid não cria 2º vínculo; um oid não pode ser colado em 2 usuários.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE Name = N'UQ_users_entra_oid' AND object_id = Object_ID(N'dbo.users')
)
    CREATE UNIQUE INDEX UQ_users_entra_oid
        ON dbo.users(entra_oid)
        WHERE entra_oid IS NOT NULL;
GO

-- ============ Validação ============
SELECT c.name AS column_name, t.name AS data_type, c.is_nullable
FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = Object_ID(N'dbo.users') AND c.name = N'entra_oid';

SELECT i.name AS index_name, i.is_unique, i.has_filter
FROM sys.indexes i
WHERE i.object_id = Object_ID(N'dbo.users') AND i.name = N'UQ_users_entra_oid';

PRINT 'phase-04-ciam-link.sql aplicada/verificada — users.entra_oid (UNIQUEIDENTIFIER NULL) + UQ_users_entra_oid filtrado/unique.';
GO
```

> **Nota sobre o índice:** em `purchases` o índice de `entra_oid` é **NÃO-unique** (um usuário faz N compras). Em `users` é **UNIQUE** filtrado (um oid ↔ um usuário). Essa diferença é deliberada e é o que dá a idempotência do vínculo de cadastro.

---

## 2. MECANISMO de migração (escolhido + alternativas rejeitadas)

### Restrições que mandam na escolha (realidade de sala de aula)

- **Aluno executando ao vivo, ~1,5–2h** para o bloco 4 (story §Roteiro).
- **bcrypt v1 NÃO é importável** para o CIAM: o External ID **não aceita** hashes bcrypt como credencial (o import de senha do Entra/Graph aceita apenas formatos específicos de password hash que **não incluem bcrypt** do `bcryptjs`). Logo, **a senha v1 não viaja** — o usuário migrado **precisa** estabelecer credencial nova no CIAM (sign-up self-service / Google / OTP). Isto é tratado de frente (§3).
- **Poucos usuários** no dataset de lab (o seed é de compras 100k, mas o conjunto de **contas de demonstração** que o aluno migra é pequeno — handfuls; a migração é didática, não de produção).
- **Idempotência obrigatória** (ADE-000 Inv 2): rodar 2x não pode duplicar nada.
- **Aditiva**: bcrypt/`users` intactos.

### Avaliação das opções

| Critério | Opção A — Graph bulk import (criar usuários no CIAM a partir de `users`) | Opção B — Self-service sign-up + link manual do `oid` | Opção C — **Híbrido (recomendado)** |
|---|---|---|---|
| Realista p/ aluno ao vivo | ⚠️ médio: exige app registration com permissão `User.ReadWrite.All`, consentimento admin, montar JSON de usuários, chamar `POST /users` no Graph. Cerimônia alta p/ sala. | ✅ alto: aluno já sabe fazer sign-up (fez no bloco 2). Zero permissão Graph elevada. | ✅ alto: usa o sign-up que o aluno já domina + 1 passo SQL guiado. |
| Senha bcrypt | ❌ não migra (Graph não aceita bcrypt) → usuário criado teria que resetar senha de qualquer forma. | ✅ irrelevante: usuário cria credencial nova no sign-up (Google/email+OTP). | ✅ idem B. |
| Quantos usuários | bom p/ muitos (centenas) | bom p/ poucos (lab) | bom p/ poucos (lab) |
| Idempotência | ⚠️ precisa checar "usuário já existe no CIAM por email" antes de criar (Graph `GET /users?$filter=...`) p/ não duplicar | ✅ sign-up nativo não duplica (email já existe → CIAM bloqueia). Link SQL é `UPDATE ... WHERE entra_oid IS NULL` (idempotente). | ✅ idem B |
| Vínculo ao registro v1 | precisa de UPDATE posterior por email mesmo assim | UPDATE por email (chave natural UNIQUE) | UPDATE por email |
| Rollback | apagar usuários criados no CIAM (Graph DELETE) + limpar coluna | só `UPDATE users SET entra_oid=NULL` (CIAM fica, é trial descartável) | idem B |
| Valor didático | ensina Graph (bom), mas dilui a lição central (coexistência) em cerimônia de API | foca na lição central: "o mesmo humano agora tem 2 identidades, e eu LIGO as duas" | combina: mostra Graph **opcionalmente** + foca no link |

### ESCOLHA: **Opção C — Híbrido, com o caminho primário = self-service + link SQL por email**

> **Mecanismo em 1 frase:** o aluno faz **sign-up self-service no CIAM** (reusando o que aprendeu no Bloco 2) para as contas de demonstração — usando o **mesmo email** do `users` v1 — e depois executa um **`UPDATE users SET entra_oid = <oid> WHERE email = <email> AND entra_oid IS NULL`** que liga o `oid` do CIAM ao registro v1 existente; opcionalmente (faixa avançada / turmas com tempo) demonstra-se a **Opção A via Graph `POST /users`** para falar de import em massa, mas o caminho avaliado/verificado da AC é o self-service.

**Por quê:**
- É o caminho **mais realista para um aluno ao vivo** e **reaproveita** a competência recém-adquirida (sign-up CIAM do Bloco 2), reduzindo carga cognitiva no bloco mais pesado.
- Contorna o bloqueio do bcrypt **honestamente**: como a senha não migra, é **mais didático** o aluno **viver** o sign-up (e ver que agora a senha é gerida pela Microsoft) do que esconder isso atrás de um `POST /users`.
- A idempotência cai naturalmente do `WHERE entra_oid IS NULL` + índice UNIQUE filtrado (§4).
- O link por **email** usa a única chave natural confiável entre os dois mundos (`users.email` é UNIQUE; o email é claim do token CIAM).

### Alternativas rejeitadas

- **A pura (Graph bulk import como caminho único):** rejeitada como **caminho primário** por cerimônia de permissões elevadas (`User.ReadWrite.All` + consentimento admin) que estoura o tempo e desvia da lição central; **mantida como demonstração opcional** para falar de migração em massa.
- **B "pura" sem coluna `users.entra_oid` (link só em `purchases`):** rejeitada — ver §1 (falsificaria histórico transacional ou dependeria de compra v2 para a prova didática).
- **Importar a senha bcrypt para o CIAM:** **impossível** — o External ID não aceita hash bcrypt; não há caminho. (Reportado, não escondido.)

---

## 3. bcrypt v1 não migra — como o design trata isso (explícito, não escondido)

**Fato:** o hash em `users.password` é `bcryptjs` 10 rounds (`auth.js:42`). O Microsoft Entra External ID **não aceita** bcrypt como credencial importável. **A senha v1 não pode ir para o CIAM.**

**Decisão de design:** o usuário migrado **estabelece credencial nova no CIAM** no momento do sign-up — via **conta Google** (social IdP pré-configurado) ou **email + OTP** (fallback). O `users.password` (bcrypt) **permanece intacto** e continua válido para login pelo caminho **v1** (Express/`auth.js`). Resultado: **o mesmo humano passa a ter duas credenciais independentes** — bcrypt no v1, identidade gerenciada no CIAM — que é **exatamente** o contraste didático das Quartas (homegrown vs gerenciado). O bcrypt não ser exportável **não é um problema a contornar; é a própria lição**: "veja, com identidade gerenciada você nem tem mais um hash para gerenciar".

> **Speaker-note sugerida ao @analyst (não escrevo o artefato, só o conteúdo técnico):** "A senha bcrypt do v1 **não** vai para o CIAM — e isso é de propósito. No mundo gerenciado, a Microsoft cuida da credencial; você só guarda o `oid`. O bcrypt continua aqui ao lado, intacto, para você comparar."

---

## 4. Passo-a-passo hands-on (o "como" que o @analyst vira runbook)

> Pré-condição (instrutor, pré-aula): tenant CIAM trial provisionado com user flow + Google IdP; migration `phase-04-ciam-link.sql` **já aplicada** (coluna `users.entra_oid` criada vazia — criar coluna não é hands-on; **preencher** é). Dataset com ~3–5 contas de demonstração em `users` (com email conhecido).

1. **Listar os alvos da migração (SQL, read-only).** Aluno roda e vê quem ainda não migrou:
   ```sql
   SELECT id, name, email, entra_oid
   FROM dbo.users
   WHERE entra_oid IS NULL
   ORDER BY id;
   ```
2. **Sign-up no CIAM com o MESMO email do v1.** Para cada conta de demonstração, o aluno faz sign-up self-service no SPA (Bloco 2) usando **o email idêntico** ao de `users` (Google se o email for Google; senão email+OTP). *(Reusa a competência do Bloco 2 — sem cerimônia nova.)*
3. **Capturar o `oid` emitido pelo CIAM.** Duas vias equivalentes (o PORTAL-GUIDE mostra ambas):
   - **Via app (recomendada em sala):** logar com a conta recém-criada → o SPA chama o gateway → o gateway extrai o claim `oid` e o injeta como `X-Entra-OID`; o aluno vê o `oid` (mascarado em log, completo no token decodificado em jwt.ms / DevTools).
   - **Via Portal:** Entra External ID → Users → selecionar o usuário → copiar **Object ID**.
4. **Vincular o `oid` ao registro v1 (o coração da migração) — idempotente:**
   ```sql
   -- Liga o oid CIAM ao usuário v1 de mesmo email. Idempotente: só atua em quem
   -- ainda não tem vínculo (entra_oid IS NULL). Re-rodar não muda nada (0 rows).
   UPDATE dbo.users
   SET    entra_oid = @oid          -- ex.: 'a1b2c3d4-....'  (oid do passo 3)
   WHERE  email = @email            -- ex.: 'demo@contoso.com'
     AND  entra_oid IS NULL;        -- guard de idempotência
   ```
   *(Repetir para cada conta. Em turmas com tempo, o instrutor mostra a Opção A: `POST https://graph.microsoft.com/v1.0/users` em lote — mas o caminho avaliado da AC é este UPDATE.)*
5. **Verificar a coexistência (a prova didática — §5).** Aluno roda a query de verificação e **vê na mesma linha**: `password` (bcrypt v1) preenchido E `entra_oid` (CIAM) preenchido.
6. **(Opcional) Provar que o v1 ainda funciona.** Aluno faz login pelo caminho v1 (Express) com a senha antiga → funciona. Depois login pelo CIAM → funciona. **Mesmo humano, dois mundos, ambos vivos.**

---

## 5. Idempotência, rollback e coexistência

### Idempotência (rodar 2x sem efeito colateral) — garantida por três mecanismos

1. **`WHERE entra_oid IS NULL`** no UPDATE: a segunda execução encontra 0 linhas elegíveis → **no-op**.
2. **`UQ_users_entra_oid` (UNIQUE filtrado)**: impede que o mesmo `oid` seja colado em dois usuários diferentes, e que um usuário receba dois `oid` distintos por engano — o banco é o guarda da unicidade do vínculo (mesma filosofia do `UQ_purchases_correlation_id` em `phase-01`).
3. **Sign-up CIAM nativo não duplica**: tentar sign-up com email já existente no CIAM é bloqueado pelo próprio user flow → não cria conta duplicada.

> **Como ligar o `oid` ao usuário CERTO sem duplicar:** a chave de match é **`email`** (`users.email` UNIQUE + email como identidade no CIAM). O UPDATE casa por email; o índice UNIQUE garante 1:1. Não há ambiguidade porque email é único nos dois lados.

### Rollback (aditivo ⇒ trivial e seguro)

- **Desfazer o vínculo:** `UPDATE dbo.users SET entra_oid = NULL WHERE email = @email;` (ou `WHERE entra_oid = @oid`). Não destrói nada — só limpa o ponteiro.
- **Reverter a migration inteira (raro):** `DROP INDEX UQ_users_entra_oid ON dbo.users; ALTER TABLE dbo.users DROP COLUMN entra_oid;` — seguro porque a coluna é aditiva e nada mais depende dela. O bcrypt/`users` nunca foi tocado.
- **CIAM:** o tenant é trial descartável (recriável por turma) — não requer rollback formal; basta deletar o usuário no Portal se desejado.
- **NUNCA** criar backup table no SQL (regra do projeto). Snapshot, se necessário, via `pg_dump`/`bcp` fora de banda — mas para um vínculo aditivo isso é desnecessário.

### Coexistência — o que acontece com quem NUNCA migrar

Usuários v1 que não migram ficam com `users.entra_oid = NULL` e seu `password` bcrypt intacto. **Continuam logando normalmente pelo v1.** Nada quebra: a coluna é NULLable, o índice é filtrado (ignora NULLs), e nenhum caminho v1 conhece a coluna. Isto é a própria ADE-000 Inv 1 (coexistência) materializada — migração é **opt-in por usuário**, não um big-bang.

---

## 6. Query de verificação (base do AC-17 / prova da AC-16)

> Prova, na **mesma linha**, que o mesmo usuário tem a credencial homegrown (bcrypt v1) **intacta** E o vínculo CIAM (`entra_oid`) **preenchido**. Esta é a query que o aluno roda no clímax.

```sql
-- Prova de coexistência v1 (bcrypt) + v2 (CIAM) no MESMO registro de usuário.
-- NÃO expõe o hash em texto (só prova que existe) — boa higiene de PII.
SELECT
    u.id                                   AS user_id_v1,        -- chave v1 (int)
    u.email,
    CASE WHEN u.password LIKE '$2%'                              -- bcrypt começa com $2a/$2b/$2y
         THEN 'bcrypt-presente' ELSE 'sem-bcrypt' END AS credencial_v1,
    u.entra_oid                            AS oid_ciam_v2,       -- vínculo CIAM (GUID)
    CASE
        WHEN u.password IS NOT NULL AND u.entra_oid IS NOT NULL
            THEN 'COEXISTE (v1 bcrypt + v2 CIAM)'                -- ✅ o ápice didático
        WHEN u.entra_oid IS NULL
            THEN 'so v1 (nao migrou)'
        ELSE 'estado inesperado'
    END                                    AS status_migracao
FROM dbo.users u
WHERE u.email = @email;                    -- o usuário que o aluno acabou de migrar
```

Resultado esperado pós-migração para o usuário migrado: uma linha com `credencial_v1 = bcrypt-presente`, `oid_ciam_v2 = <guid>`, `status_migracao = COEXISTE (v1 bcrypt + v2 CIAM)`.

> **Variante opcional (liga ao fluxo de compra, se o aluno também comprou no v2):** um `LEFT JOIN dbo.purchases p ON p.entra_oid = u.entra_oid` mostra que o mesmo `oid` que agora vive em `users.entra_oid` é o que aparece nas compras v2 daquele usuário — fechando o círculo `users.entra_oid` (cadastro) ↔ `purchases.entra_oid` (transação). Útil para o @analyst ilustrar a ponte cadastro↔compra, **não obrigatória** para a AC-16.

---

## 7. Impacto nos ACs da story 2.11 (o que o @sm precisa saber)

### ACs CONFIRMADOS (este design satisfaz sem ajuste)

| AC | Status | Nota |
|---|---|---|
| **AC-9** (`entra_oid` em `purchases`, coexiste com v1 na mesma `purchases`) | ✅ **CONFIRMADO** | `purchases.entra_oid` **não muda** — zero delta nesse eixo. Verdadeiro como escrito. |
| **AC-10** (encadeamento Gateway→Function→SQL intacto, issuer-agnóstico) | ✅ **CONFIRMADO** | A migração de cadastro **não toca** o pipeline de compra. `PurchaseRepository.cs` inalterado. |
| **AC-15** (migração v1→CIAM aditiva e idempotente, bcrypt intacto) | ✅ **CONFIRMADO** | Mecanismo C é aditivo (só `UPDATE users.entra_oid`) e idempotente (§4/§5). bcrypt nunca tocado. |
| **AC-16** (coexistência demonstrada pós-migração) | ✅ **CONFIRMADO** | A própria AC já previa "`ou na tabela users, conforme @data-engineer`" — é o caminho escolhido. Query do §6 entrega a prova. |
| **AC-17** (B2C legado não provisionado) | ✅ **CONFIRMADO** | Mecanismo usa só External ID + SQL; nenhum recurso Azure AD B2C. |

### ACs que precisam de AJUSTE do @sm (registrados honestamente)

| AC | Problema | Ajuste sugerido ao @sm |
|---|---|---|
| **AC-10** (frase "**Nenhuma nova migration é necessária — schema delta é zero**") | A afirmação é verdadeira **para o eixo de provedor de identidade**, mas o lab inteiro passa a ter **1 migration aditiva nova** (`phase-04-ciam-link.sql`, coluna `users.entra_oid`). | Reescrever a cláusula final da AC-10 para: "Nenhuma mudança no pipeline de compra (issuer-agnóstico). A migração de **cadastro** (AC-15) introduz **uma** migration aditiva e idempotente (`users.entra_oid`), que não afeta o fluxo de compra." |
| **AC-9** (afirma o vínculo do mesmo usuário "na mesma tabela `purchases`") | Para a **prova de cadastro** (usuário que migrou mas não comprou v2), o vínculo durável fica em **`users.entra_oid`**, não em `purchases`. AC-9 segue correta para o **fluxo de compra**, mas a **prova de coexistência de cadastro** (AC-16) usa `users`. | Manter AC-9 como está (descreve o fluxo de compra). Garantir que AC-16 referencie `users.entra_oid` como o local do vínculo de cadastro (a AC-16 **já** abre essa porta — só confirmar a redação final apontando `users`). |
| **Task 7.1 / Task 6.1** (já anteciparam "se @data-engineer definir coluna em `users`, criar nova migration") | Nenhum problema — **a decisão saiu: SIM, `users.entra_oid`**. | Materializar `phase-04-ciam-link.sql` (DDL em §1 deste doc) no build; marcar Task 6.1 como resolvida apontando este doc. |
| **Tabela comparativa do Dev Notes** ("`bcrypt em users.password_hash`") | Divergência menor: a coluna real chama-se **`password`**, não `password_hash`. | Corrigir "`users.password_hash`" → "`users.password`" na tabela comparativa v1/v2 (Dev Notes da story + README do @analyst). |

### Resumo para o handoff

- **1 migration nova** a adicionar ao build: `fifa2026-api/database/migrations/phase-04-ciam-link.sql` (DDL pronta no §1, aditiva/idempotente).
- **Mecanismo** para o @analyst transformar em PORTAL-GUIDE/SPEAKER-NOTES: §4 (passo-a-passo) + §3 (tratamento do bcrypt) + §6 (query da prova).
- **Slogan "schema delta zero"** deixa de ser literal para o lab inteiro — ajuste de redação em AC-10 (não muda escopo, só honestidade do texto).

---

## Change Log

| Date | Author | Description |
|---|---|---|
| 2026-06-25 | Dara (@data-engineer) | Design da migração v1→CIAM criado. **Decisão de schema: ADICIONAR `users.entra_oid`** (migration aditiva/idempotente `phase-04-ciam-link.sql`), mantendo `purchases.entra_oid` inalterado. **Mecanismo: híbrido C** (self-service sign-up + link SQL por email; Graph opcional). bcrypt não-exportável tratado explicitamente (credencial nova no CIAM, bcrypt intacto no v1). Idempotência por `WHERE entra_oid IS NULL` + UNIQUE filtrado. Impacto: AC-9/10/15/16/17 confirmados; ajustes de redação em AC-10 e tabela comparativa sinalizados ao @sm. Divergência reportada (Art. IV): `purchases.entra_oid` é transacional ⇒ vínculo de cadastro precisa de `users.entra_oid`. |
