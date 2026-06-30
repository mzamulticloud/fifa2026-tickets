const express = require('express');
const { query } = require('../config/database');
const { gatewayTrustMiddleware } = require('../middleware/gatewayTrust');

const router = express.Router();

// Todas as rotas requerem privilégios de admin. O middleware ADITIVO confia no gateway
// (workforce via X-Gateway-Key, sem login v1) OU cai no fluxo legado v1
// (authMiddleware → adminMiddleware) — ver middleware/gatewayTrust.js.
// MVP: /stats, /sales, /sales/:id são as únicas rotas deste router.
router.use(gatewayTrustMiddleware);

// GET /api/admin/sales - Lista paginada de vendas
// Query params: page, pageSize, status, search, start_date, end_date
router.get('/sales', async (req, res) => {
  try {
    const page = Math.max(1, parseInt(req.query.page) || 1);
    const pageSize = Math.min(200, Math.max(1, parseInt(req.query.pageSize) || 15));
    const offset = (page - 1) * pageSize;
    const { status, start_date, end_date, search } = req.query;

    const baseFrom = `
      FROM purchases p
      JOIN users u ON p.user_id = u.id
      JOIN ticket_categories tc ON p.ticket_category_id = tc.id
      JOIN matches m ON tc.match_id = m.id
      LEFT JOIN teams ht ON m.home_team_id = ht.id
      LEFT JOIN teams at ON m.away_team_id = at.id
      LEFT JOIN stadiums s ON m.stadium_id = s.id
      WHERE 1=1
    `;

    const params = [];
    let whereExtra = '';

    if (status && status !== 'all') {
      whereExtra += ` AND p.status = @param${params.length}`;
      params.push(status);
    }
    if (start_date) {
      whereExtra += ` AND p.created_at >= @param${params.length}`;
      params.push(start_date);
    }
    if (end_date) {
      whereExtra += ` AND p.created_at <= @param${params.length}`;
      params.push(end_date);
    }
    if (search) {
      whereExtra += ` AND (u.name LIKE @param${params.length} OR u.email LIKE @param${params.length} OR ht.name LIKE @param${params.length} OR at.name LIKE @param${params.length} OR CAST(p.id AS VARCHAR) = @param${params.length})`;
      params.push(`%${search}%`);
    }

    // Total
    const countQuery = `SELECT COUNT(*) AS total ${baseFrom}${whereExtra}`;
    const countResult = await query(countQuery, params);
    const total = countResult.recordset[0].total;

    // Página
    const dataQuery = `
      SELECT
        p.id, p.quantity, p.unit_price, p.total_price, p.status, p.created_at,
        u.id as user_id, u.name as user_name, u.email as user_email,
        tc.category,
        m.id as match_id, m.date as match_date, m.time as match_time, m.stage,
        ht.name as home_team, ht.code as home_team_code,
        at.name as away_team, at.code as away_team_code,
        s.name as stadium_name, s.city as stadium_city
      ${baseFrom}${whereExtra}
      ORDER BY p.created_at DESC
      OFFSET ${offset} ROWS FETCH NEXT ${pageSize} ROWS ONLY
    `;
    const result = await query(dataQuery, params);
    res.json({
      sales: result.recordset,
      pagination: { page, pageSize, total, totalPages: Math.ceil(total / pageSize) },
    });
  } catch (err) {
    console.error('Erro ao buscar vendas:', err);
    res.status(500).json({ error: 'Erro ao buscar vendas' });
  }
});

// GET /api/admin/sales/:id - Detalhes de uma venda
router.get('/sales/:id', async (req, res) => {
  try {
    const result = await query(`
      SELECT 
        p.id, p.quantity, p.unit_price, p.total_price, p.status, p.created_at,
        u.id as user_id, u.name as user_name, u.email as user_email,
        tc.category,
        m.id as match_id, m.date as match_date, m.time as match_time, m.stage,
        ht.name as home_team, ht.code as home_team_code, ht.flag as home_team_flag,
        at.name as away_team, at.code as away_team_code, at.flag as away_team_flag,
        s.name as stadium_name, s.city as stadium_city, s.country as stadium_country
      FROM purchases p
      JOIN users u ON p.user_id = u.id
      JOIN ticket_categories tc ON p.ticket_category_id = tc.id
      JOIN matches m ON tc.match_id = m.id
      LEFT JOIN teams ht ON m.home_team_id = ht.id
      LEFT JOIN teams at ON m.away_team_id = at.id
      LEFT JOIN stadiums s ON m.stadium_id = s.id
      WHERE p.id = @param0
    `, [req.params.id]);

    if (result.recordset.length === 0) {
      return res.status(404).json({ error: 'Venda não encontrada' });
    }

    res.json({ sale: result.recordset[0] });
  } catch (err) {
    console.error('Erro ao buscar venda:', err);
    res.status(500).json({ error: 'Erro ao buscar venda' });
  }
});

// GET /api/admin/stats - Estatísticas gerais
router.get('/stats', async (req, res) => {
  try {
    const statsResult = await query(`
      SELECT 
        (SELECT COUNT(*) FROM users) as total_users,
        (SELECT COUNT(*) FROM purchases WHERE status = 'completed') as total_sales,
        (SELECT ISNULL(SUM(total_price), 0) FROM purchases WHERE status = 'completed') as total_revenue,
        (SELECT ISNULL(SUM(quantity), 0) FROM purchases WHERE status = 'completed') as total_tickets_sold,
        (SELECT COUNT(*) FROM matches) as total_matches,
        (SELECT COUNT(*) FROM stadiums) as total_stadiums
    `);

    res.json({ stats: statsResult.recordset[0] });
  } catch (err) {
    console.error('Erro ao buscar estatísticas:', err);
    res.status(500).json({ error: 'Erro ao buscar estatísticas' });
  }
});

module.exports = router;
