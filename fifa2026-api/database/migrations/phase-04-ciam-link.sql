-- =====================================================
-- Migration: phase-04-ciam-link.sql — vínculo durável usuário v1 ↔ CIAM oid
-- Story: 2.11 — Quartas de Final (migração users v1 → CIAM, ADE-007 v1.1 Inv 6)
-- =====================================================
-- Adiciona users.entra_oid: o Object ID (oid) do Microsoft Entra External ID (CIAM)
-- correspondente a ESTE registro de usuário v1. É o ALVO durável do vínculo da
-- migração de cadastro (passo hands-on do Bloco 4 das Quartas).
--
-- DOIS EIXOS DE IDENTIDADE (ADE-007 v1.1 Inv 3 — distinção essencial):
--   - purchases.entra_oid (phase-03.sql) = eixo COMPRA, transacional. "o oid de quem
--     fez AQUELA compra v2". Repete por compra. Índice FILTRADO, NÃO-unique. Preenchido
--     pelo pipeline oid → X-Entra-OID → entra_oid (issuer-agnóstico; zero-DDL no eixo
--     provedor de identidade — só muda a authority do gateway de workforce p/ CIAM).
--   - users.entra_oid (ESTE arquivo) = eixo CADASTRO, durável. "o oid CIAM vinculado a
--     ESTE registro de usuário v1". Único por usuário. Índice FILTRADO, UNIQUE.
--     Preenchido pela migração de cadastro hands-on (phase-04-ciam-link-migration-script.sql).
--     Sobrevive mesmo que o usuário NUNCA compre no v2 — por isso não pode morar em
--     purchases (transacional). Ver migration-v1-ciam-design.md §1 (decisão de schema).
--
-- DDL pronta em migration-v1-ciam-design.md §1 (@data-engineer / Dara, 2026-06-25).
--
-- Restrições de schema respeitadas (ADE-000 Inv 2 — aditivo apenas):
--   - Somente ALTER TABLE ADD COLUMN + CREATE INDEX. Nenhum DROP/ALTER COLUMN.
--   - NÃO apaga users, NÃO toca password (bcrypt v1 intacto — ADE-007 Inv 4 / Inv 6).
--   - entra_oid : UNIQUEIDENTIFIER NULL (quem nunca migrou fica NULL; coexistência v1).
--
-- Por que índice UNIQUE filtrado (e não NÃO-unique como em purchases)?
--   Cada oid CIAM mapeia para NO MÁXIMO um usuário v1 (relação 1:1 de cadastro).
--   UNIQUE filtrado aplica a unicidade só onde entra_oid IS NOT NULL (vários NULL
--   convivem — quem não migrou). Isto é o que garante a IDEMPOTÊNCIA do link: re-rodar
--   o UPDATE com o mesmo oid não cria 2º vínculo; um oid não pode ser colado em 2
--   usuários. (Em purchases o índice é NÃO-unique de propósito: um usuário faz N compras.)
--
-- IMPORTANTE (operacional):
--   IDEMPOTENTE (IF NOT EXISTS): rodar 2x não duplica coluna nem índice.
--   Executar PRÉ-WORKSHOP (cria a coluna VAZIA); o PREENCHIMENTO é o passo hands-on
--   da aula (phase-04-ciam-link-migration-script.sql). NÃO aplicar no banco real a
--   partir do @dev — execução é do @devops/instrutor.
--
-- Anti-hallucination (AC-19): tabela `users`, coluna `email` UNIQUE (chave de match
--   v1↔CIAM) e tipo UNIQUEIDENTIFIER validados contra fifa2026-api/database/schema.sql.
--   Claim `oid` validado contra docs Microsoft Identity Platform (id-token-claims-reference).
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
-- Cada oid CIAM mapeia para NO MÁXIMO um usuário v1 (1:1). UNIQUE filtrado: aplica a
-- unicidade só onde entra_oid IS NOT NULL (vários NULL convivem — quem não migrou).
-- Isto é o que garante a IDEMPOTÊNCIA do link: re-rodar com o mesmo oid não cria 2º
-- vínculo; um oid não pode ser colado em 2 usuários.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE Name = N'UQ_users_entra_oid' AND object_id = Object_ID(N'dbo.users')
)
    CREATE UNIQUE INDEX UQ_users_entra_oid
        ON dbo.users(entra_oid)
        WHERE entra_oid IS NOT NULL;
GO

-- ============ Validação ============
SELECT
    c.name        AS column_name,
    t.name        AS data_type,
    c.is_nullable AS is_nullable
FROM sys.columns c
JOIN sys.types   t ON c.user_type_id = t.user_type_id
WHERE c.object_id = Object_ID(N'dbo.users')
  AND c.name = N'entra_oid';

SELECT i.name AS index_name, i.is_unique, i.has_filter
FROM sys.indexes i
WHERE i.object_id = Object_ID(N'dbo.users')
  AND i.name = N'UQ_users_entra_oid';

PRINT 'phase-04-ciam-link.sql aplicada/verificada — esperado: coluna users.entra_oid (UNIQUEIDENTIFIER NULL) + índice UNIQUE filtrado UQ_users_entra_oid.';
GO
