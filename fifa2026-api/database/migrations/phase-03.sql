-- =====================================================
-- Migration: phase-03.sql — Schema delta para Fase 3 (EPIC-002)
-- Story: 2.3 — F3 Identidade (App Registration workforce + MSAL.js + Easy Auth)
-- =====================================================
-- Adiciona o vínculo de identidade Entra ao fluxo de compra v2:
--   - coluna `entra_oid` : Object ID (GUID estável) do usuário no tenant Entra
--                          workforce — claim `oid` do access token, propagado pelo
--                          gateway YARP como header X-Entra-OID (ADE-005 Inv 3/4).
--   - índice filtrado    : acelera lookup por identidade; só indexa linhas v2 que
--                          têm oid (entra_oid IS NOT NULL).
--
-- DECISÃO DE DESIGN (AUTO-DECISION, ver story 2.3 AC-8 + Dev Notes):
--   coluna em `purchases` (NÃO em `users`) — mantém a comparação lado-a-lado v1/v2
--   na mesma tabela (ADE-000 Inv 1). v1 segue com user_id int + bcrypt; v2 grava
--   entra_oid (GUID) ao lado. Se @architect preferir users.entra_oid no QG, o ajuste
--   é simples e não invalida a story. Não há tabela de mapping (ADE-001 aposentada
--   por ADE-005).
--
-- IMPORTANTE (operacional):
--   Esta migration é IDEMPOTENTE (IF NOT EXISTS) e deve ser executada em
--   PRÉ-WORKSHOP, NÃO durante a aula (evita atrito didático).
--   NÃO aplicar no banco real a partir do @dev — execução é do @devops/instrutor.
--
-- Restrições de schema respeitadas (ADE-000 Inv 2 — aditivo apenas):
--   - Somente ALTER TABLE ADD COLUMN + CREATE INDEX. Nenhum DROP/ALTER COLUMN.
--   - `entra_oid` : UNIQUEIDENTIFIER NULL (NÃO NOT NULL — compras v1/antigas sem
--     identidade Entra continuam válidas; CodeRabbit focus area da story 2.3).
--
-- Por que índice NÃO-UNIQUE (e não UNIQUE)?
--   Um mesmo usuário Entra (oid) faz VÁRIAS compras → entra_oid repete legitimamente
--   entre linhas. UNIQUE quebraria a 2ª compra do mesmo usuário. A unicidade da
--   idempotência v2 já é garantida por UQ_purchases_correlation_id (phase-01.sql).
--
-- Anti-hallucination (AC-14): tabela `purchases` e tipo UNIQUEIDENTIFIER validados
--   contra fifa2026-api/database/schema.sql e contra phase-01.sql (mesmo padrão
--   usado para correlation_id). Claim `oid` validado contra docs Microsoft Identity
--   Platform (id-token-claims-reference).
-- =====================================================

SET NOCOUNT ON;

-- ============ purchases.entra_oid ============
-- Object ID (oid) do usuário no tenant Entra workforce. NULL para compras sem
-- identidade Entra (v1, ou v2 anônimo antes de F3).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'entra_oid' AND Object_ID = Object_ID(N'dbo.purchases')
)
    ALTER TABLE dbo.purchases ADD entra_oid UNIQUEIDENTIFIER NULL;
GO

-- ============ IX_purchases_entra_oid (filtered, NÃO-unique) ============
-- Acelera consultas "compras deste usuário Entra" (base para F5 chatbot / F6
-- visualizer saberem quem iniciou o fluxo). Filtrado: só indexa linhas v2 com oid.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE Name = N'IX_purchases_entra_oid' AND object_id = Object_ID(N'dbo.purchases')
)
    CREATE INDEX IX_purchases_entra_oid
        ON dbo.purchases(entra_oid)
        WHERE entra_oid IS NOT NULL;
GO

-- ============ Validação ============
SELECT
    c.name        AS column_name,
    t.name        AS data_type,
    c.is_nullable AS is_nullable
FROM sys.columns c
JOIN sys.types   t ON c.user_type_id = t.user_type_id
WHERE c.object_id = Object_ID(N'dbo.purchases')
  AND c.name = N'entra_oid';

SELECT i.name AS index_name, i.is_unique, i.has_filter
FROM sys.indexes i
WHERE i.object_id = Object_ID(N'dbo.purchases')
  AND i.name = N'IX_purchases_entra_oid';

PRINT 'phase-03.sql aplicada/verificada — esperado: coluna entra_oid (UNIQUEIDENTIFIER NULL) e index filtrado IX_purchases_entra_oid.';
GO
