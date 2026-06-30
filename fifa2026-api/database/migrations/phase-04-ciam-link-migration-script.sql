-- =====================================================
-- Script de migração HANDS-ON: vincular usuário v1 → oid CIAM
-- Story: 2.11 — Quartas de Final (Task 6.1, ADE-007 v1.1 Inv 6)
-- Mecanismo: migration-v1-ciam-design.md §4 (Opção C — self-service + link SQL por email)
-- =====================================================
-- ⚠️ ESTE SCRIPT É O PASSO HANDS-ON DA AULA (Bloco 4). NÃO pré-aplicar pré-workshop.
--    A DDL da coluna users.entra_oid vem ANTES, em phase-04-ciam-link.sql (essa SIM é
--    pré-aula, cria a coluna vazia). ESTE script PREENCHE a coluna — é o que o aluno
--    executa ao vivo depois de fazer o sign-up self-service no CIAM.
--
-- O QUE FAZ:
--   Liga o oid do Microsoft Entra External ID (CIAM) ao registro de usuário v1 de
--   MESMO email, gravando-o em users.entra_oid. A senha bcrypt (users.password)
--   permanece INTACTA — o usuário estabeleceu credencial nova no CIAM (Google/OTP).
--   bcrypt NÃO é exportável para o CIAM (External ID não aceita hash bcryptjs) — e
--   isso É a lição (migration-v1-ciam-design.md §3): no mundo gerenciado a Microsoft
--   cuida da credencial; você guarda só o oid.
--
-- IDEMPOTÊNCIA (três mecanismos — migration-v1-ciam-design.md §5):
--   1. WHERE entra_oid IS NULL : a 2ª execução encontra 0 linhas elegíveis → no-op.
--   2. UQ_users_entra_oid (UNIQUE filtrado, criado por phase-04-ciam-link.sql):
--      o banco impede que o mesmo oid seja colado em 2 usuários (1:1).
--   3. Sign-up CIAM nativo não duplica: email já existente é bloqueado pelo user flow.
--
-- CHAVE DE MATCH: users.email (UNIQUE em schema.sql) — única chave natural confiável
--   entre os dois mundos (o email também é claim do token CIAM).
--
-- Anti-hallucination (AC-19): users.email (UNIQUE) e users.password (bcrypt, NÃO
--   password_hash) validados contra fifa2026-api/database/schema.sql. Claim `oid`
--   validado contra docs Microsoft Identity Platform.
-- =====================================================

SET NOCOUNT ON;

-- -----------------------------------------------------------------------------
-- PASSO 1 (read-only) — Listar os alvos da migração (quem ainda NÃO migrou).
-- O aluno roda isto primeiro e vê as contas de demonstração com entra_oid = NULL.
-- -----------------------------------------------------------------------------
SELECT id, name, email, entra_oid
FROM dbo.users
WHERE entra_oid IS NULL
ORDER BY id;
GO

-- -----------------------------------------------------------------------------
-- PASSO 2 (o coração da migração) — Vincular o oid CIAM ao usuário v1 de mesmo email.
--
-- Substitua @oid pelo Object ID emitido pelo CIAM (capturado no passo 3 do runbook:
--   - via app: o gateway extrai o claim oid e injeta X-Entra-OID; token decodificado
--     em jwt.ms / DevTools mostra o oid completo; OU
--   - via Portal: Entra External ID → Users → selecionar o usuário → copiar Object ID).
-- Substitua @email pelo email IDÊNTICO ao usado no sign-up CIAM (= email do users v1).
--
-- IDEMPOTENTE: só atua em quem ainda não tem vínculo (entra_oid IS NULL). Re-rodar
-- com o mesmo par → 0 linhas afetadas (no-op). Repetir para cada conta de demonstração.
-- -----------------------------------------------------------------------------
DECLARE @oid   UNIQUEIDENTIFIER = 'a1b2c3d4-0000-0000-0000-000000000000'; -- ← oid do CIAM (passo 3)
DECLARE @email NVARCHAR(255)    = N'demo@contoso.com';                    -- ← email do users v1

UPDATE dbo.users
SET    entra_oid = @oid          -- grava o vínculo durável de cadastro
WHERE  email = @email            -- casa por email (chave natural UNIQUE)
  AND  entra_oid IS NULL;        -- guard de idempotência

PRINT CONCAT('Linhas vinculadas: ', @@ROWCOUNT, ' (0 = já migrado / email inexistente).');
GO

-- -----------------------------------------------------------------------------
-- PASSO 3 (a prova didática — o ápice das Quartas) — Verificar a COEXISTÊNCIA v1/v2.
-- Prova, na MESMA linha de users, que o mesmo usuário tem a credencial homegrown
-- (bcrypt v1) INTACTA E o vínculo CIAM (entra_oid) PREENCHIDO.
-- (migration-v1-ciam-design.md §6 — query de verificação.)
--
-- Resultado esperado pós-migração: status_migracao = 'COEXISTE (v1 bcrypt + v2 CIAM)'.
-- NÃO expõe o hash em texto (só prova que existe) — boa higiene de PII.
-- -----------------------------------------------------------------------------
DECLARE @verify_email NVARCHAR(255) = N'demo@contoso.com'; -- ← o usuário recém migrado

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
WHERE u.email = @verify_email;
GO

-- -----------------------------------------------------------------------------
-- ROLLBACK (aditivo ⇒ trivial — migration-v1-ciam-design.md §5).
-- Desfaz só o vínculo; não destrói nada (bcrypt/users nunca foi tocado). Descomente:
-- -----------------------------------------------------------------------------
-- UPDATE dbo.users SET entra_oid = NULL WHERE email = N'demo@contoso.com';
-- GO
