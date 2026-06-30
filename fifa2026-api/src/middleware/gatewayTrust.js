const crypto = require('crypto');
const { authMiddleware, adminMiddleware } = require('./auth');

// =============================================================================
// Quartas / "admin 100% workforce" (MVP) — middleware ADITIVO de confiança no gateway.
//
// O gateway YARP é o guardião único: nas rotas admin ele valida o token WORKFORCE
// (policy AdminOnly = App Role "Admin") e proxia pro backend injetando dois headers:
//   - X-Entra-OID    (identidade do operador; já existia no fluxo v2)
//   - X-Gateway-Key  (shared secret — prova que a request veio do gateway, não spoof)
//
// Se X-Gateway-Key bate com GATEWAY_SHARED_SECRET (e o secret está configurado), o
// backend CONFIA na request e a trata como admin — SEM exigir o login v1 (bcrypt/JWT).
// Caso contrário, cai no fluxo LEGADO v1 (authMiddleware → adminMiddleware), que
// permanece INTOCADO. Assim v1 (bcrypt) e CIAM (cliente) continuam funcionando.
//
// GATEWAY_SHARED_SECRET vazio/ausente (default) = comportamento legado v1-only: nenhuma
// request é confiada via header, todo acesso admin exige o JWT v1. É o fail-safe seguro.
// =============================================================================

const GATEWAY_KEY_HEADER = 'X-Gateway-Key';
const ENTRA_OID_HEADER = 'X-Entra-OID';

/**
 * Comparação em tempo constante (anti timing-attack). Retorna false se algum lado
 * não for string ou se os tamanhos diferirem (timingSafeEqual exige buffers iguais).
 */
function safeEqual(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string') {
    return false;
  }
  const bufA = Buffer.from(a);
  const bufB = Buffer.from(b);
  if (bufA.length !== bufB.length) {
    return false;
  }
  return crypto.timingSafeEqual(bufA, bufB);
}

/**
 * Middleware aditivo. Confia no gateway quando o shared secret casa; senão delega ao
 * par legado authMiddleware → adminMiddleware (sem alterá-los).
 */
const gatewayTrustMiddleware = (req, res, next) => {
  const sharedSecret = process.env.GATEWAY_SHARED_SECRET;
  const incomingKey = req.header(GATEWAY_KEY_HEADER);

  // Só confia se o secret está CONFIGURADO (não-vazio) E o header bate exatamente.
  if (sharedSecret && safeEqual(incomingKey, sharedSecret)) {
    req.user = {
      role: 'admin',
      source: 'gateway',
      entra_oid: req.header(ENTRA_OID_HEADER) || null,
    };
    return next();
  }

  // Fallback v1 (legado): authMiddleware envia 401 e NÃO chama next() em falha, então
  // a cadeia para sozinha; em sucesso, encadeamos o adminMiddleware (403 se não-admin).
  return authMiddleware(req, res, () => adminMiddleware(req, res, next));
};

module.exports = { gatewayTrustMiddleware };
