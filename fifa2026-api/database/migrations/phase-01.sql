-- =====================================================
-- Migration: phase-01.sql — Schema delta para Fase 1 (EPIC-002)
-- Story: 2.1 — F1 Mensageria Desacoplada (Service Bus + Functions .NET)
-- =====================================================
-- Adiciona suporte ao fluxo de compra v2 (microsserviço .NET paralelo):
--   - coluna `source`         : rastreabilidade do produtor da linha (ADE-000 Inv 3)
--   - coluna `correlation_id` : correlação ponta-a-ponta (ADE-000 Inv 5 — SQL hop)
--   - UNIQUE filtered index   : idempotência robusta no consumer (ADE-000 Inv 4)
--
-- IMPORTANTE (operacional):
--   Esta migration é IDEMPOTENTE (IF NOT EXISTS) e deve ser executada em
--   PRÉ-WORKSHOP, NÃO durante a aula (evita atrito didático — AC-5).
--
-- Restrições de schema respeitadas (ADE-000 Inv 2 — aditivo apenas):
--   - Somente ALTER TABLE ADD COLUMN + CREATE INDEX. Nenhum DROP/ALTER COLUMN.
--   - `source`  : NOT NULL DEFAULT 'v1' (linhas v1 históricas ficam coerentes).
--   - `correlation_id` : NULL (linhas v1 históricas não têm correlação).
--
-- Por que UNIQUE FILTERED (e não UNIQUE constraint regular)?
--   `correlation_id` é NULL para todas as compras v1 já existentes. Uma UNIQUE
--   constraint regular trataria múltiplos NULL como colisão e falharia. O filtered
--   index aplica unicidade APENAS onde correlation_id IS NOT NULL — ou seja, só nas
--   compras v2 — garantindo o pattern INSERT-catch (SqlException 2627) do consumer.
--
-- Anti-hallucination (AC-13): tabela `purchases` e colunas validadas contra
--   fifa2026-api/database/schema.sql (tabela existe, lowercase, schema dbo).
-- =====================================================

SET NOCOUNT ON;

-- ============ purchases.source ============
-- Rastreabilidade do produtor: 'v1' (Node/Express original) ou 'v2' (Functions .NET).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'source' AND Object_ID = Object_ID(N'dbo.purchases')
)
    ALTER TABLE dbo.purchases ADD source NVARCHAR(20) NOT NULL CONSTRAINT DF_purchases_source DEFAULT ('v1');
GO

-- ============ purchases.correlation_id ============
-- Identificador de correlação gerado pela PurchaseEntryFunction (GUID).
-- NULL para compras v1; preenchido para todas as compras v2.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'correlation_id' AND Object_ID = Object_ID(N'dbo.purchases')
)
    ALTER TABLE dbo.purchases ADD correlation_id UNIQUEIDENTIFIER NULL;
GO

-- ============ UQ_purchases_correlation_id (UNIQUE filtered) ============
-- Idempotência robusta (ADE-000 Inv 4): o DB é o source-of-truth da unicidade.
-- O consumer faz INSERT direto e captura SqlException 2627 como duplicata — NUNCA
-- SELECT-then-INSERT (evita race condition TOCTOU em consumers paralelos).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE Name = N'UQ_purchases_correlation_id' AND object_id = Object_ID(N'dbo.purchases')
)
    CREATE UNIQUE INDEX UQ_purchases_correlation_id
        ON dbo.purchases(correlation_id)
        WHERE correlation_id IS NOT NULL;
GO

-- ============ Validação ============
SELECT
    c.name        AS column_name,
    t.name        AS data_type,
    c.is_nullable AS is_nullable
FROM sys.columns c
JOIN sys.types   t ON c.user_type_id = t.user_type_id
WHERE c.object_id = Object_ID(N'dbo.purchases')
  AND c.name IN (N'source', N'correlation_id')
ORDER BY c.name;

SELECT i.name AS index_name, i.is_unique, i.has_filter
FROM sys.indexes i
WHERE i.object_id = Object_ID(N'dbo.purchases')
  AND i.name = N'UQ_purchases_correlation_id';

PRINT 'phase-01.sql aplicada/verificada — esperado: colunas source + correlation_id e index UNIQUE filtered UQ_purchases_correlation_id.';
GO
