// =============================================================================
// Story 2.11 / Quartas (F3) — Identidade do ADMIN/OPERADOR via Microsoft Entra ID
// WORKFORCE (login.microsoftonline.com) com App Role "Admin", em MSAL.js.
//
// É o SEGUNDO mundo de identidade do front (ADE-007 Inv 5): o CLIENTE final entra
// pelo tenant CIAM (ciamlogin.com — src/lib/authV2.ts, INTOCADO); o ADMIN entra pelo
// tenant workforce e só acessa /admin se o token trouxer a App Role "Admin" na claim
// `roles`. O gateway YARP valida os dois issuers (dual-issuer selector) e protege a
// rota administrativa com a policy AdminOnly (RequireRole("Admin")).
//
// ── DECISÃO MSAL MULTI-INSTÂNCIA (por que NÃO usar MsalProvider/useMsal aqui) ──
// @azure/msal-react atrela MsalProvider/useMsal a UMA ÚNICA PublicClientApplication
// (a instância CIAM já montada em App.tsx). Montar um 2º MsalProvider para o workforce
// criaria contextos React MSAL conflitantes (useMsal pega o provider mais próximo) e
// arrisca quebrar o fluxo do cliente. Por isso a instância workforce é GERENCIADA
// MANUALMENTE — fora do MsalProvider —: criamos a PublicClientApplication aqui, a
// inicializamos sob demanda e chamamos loginPopup/acquireTokenSilent/logoutPopup
// diretamente, expondo o estado via um contexto React próprio (AdminAuthProvider).
// As duas instâncias coexistem sem colisão de cache: o MSAL namespaceia as chaves do
// sessionStorage por clientId, então CIAM e workforce não se sobrescrevem. Ambas usam
// POPUP (não redirect), então não há disputa pelo hash de redirect da página.
//
// Anti-hallucination (AC-19): PublicClientApplication, initialize, loginPopup,
// acquireTokenSilent, acquireTokenPopup, logoutPopup, getAllAccounts, idTokenClaims e
// os tipos Configuration/PopupRequest/AccountInfo são da API oficial @azure/msal-browser
// 3.30.0 (mesma versão já usada em authV2.ts). Valores de config vêm de env Vite —
// nunca hardcoded.
// =============================================================================

import {
  PublicClientApplication,
  InteractionRequiredAuthError,
  type Configuration,
  type AccountInfo,
  type PopupRequest,
} from '@azure/msal-browser';

// Reutiliza as vars VITE_ENTRA_* (resíduo workforce da F3) como fallback, mas dá
// preferência às vars explícitas VITE_ADMIN_* (mais claras p/ o lab das Quartas).
const adminClientId =
  import.meta.env.VITE_ADMIN_CLIENT_ID ?? import.meta.env.VITE_ENTRA_CLIENT_ID ?? '';
const adminTenantId =
  import.meta.env.VITE_ADMIN_TENANT_ID ?? import.meta.env.VITE_ENTRA_TENANT_ID ?? '';

// Authority workforce v2.0. Para o admin é login.microsoftonline.com/<tenantId>/v2.0
// (NÃO ciamlogin.com — esse é o mundo do cliente). Permitimos override completo via
// VITE_ADMIN_AUTHORITY caso o instrutor precise de uma authority diferente.
const adminAuthorityOverride = import.meta.env.VITE_ADMIN_AUTHORITY ?? '';
const adminAuthority = adminAuthorityOverride
  ? adminAuthorityOverride
  : adminTenantId
    ? `https://login.microsoftonline.com/${adminTenantId}/v2.0`
    : '';

const redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI ?? window.location.origin;

// Scope para obter um ACCESS TOKEN cujo audience = a App Registration admin (o gateway
// valida ValidAudiences = { AdminClientId, api://AdminClientId }). `.default` representa
// todas as permissões estáticas do app e faz o Entra emitir a App Role atribuída ao
// usuário na claim `roles`. Configurável via VITE_ADMIN_SCOPE se o tenant do instrutor
// expuser um scope nomeado (ex.: api://<id>/access_as_admin).
const adminScope =
  import.meta.env.VITE_ADMIN_SCOPE ??
  (adminClientId ? `api://${adminClientId}/.default` : 'openid');

/** App Role esperada no token workforce para liberar a área administrativa. */
export const ADMIN_ROLE = 'Admin';

/** True quando as vars mínimas do login admin workforce estão configuradas. */
export const isAdminEntraConfigured = (): boolean =>
  Boolean(adminClientId && adminAuthority);

const adminMsalConfig: Configuration = {
  auth: {
    clientId: adminClientId,
    // Workforce (login.microsoftonline.com) — discovery padrão do AAD cobre este host,
    // então NÃO precisa de knownAuthorities (diferente do CIAM/B2C em authV2.ts).
    authority: adminAuthority,
    redirectUri,
    navigateToLoginRequestUrl: false,
  },
  cache: {
    // sessionStorage namespaceado por clientId — não colide com a instância CIAM.
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
};

/**
 * Instância MSAL do WORKFORCE (admin). Gerenciada manualmente, FORA do MsalProvider
 * (ver decisão no cabeçalho). Deve ser inicializada antes do 1º uso —
 * ensureAdminMsalInitialized() abaixo garante isso de forma idempotente.
 */
export const adminMsalInstance = new PublicClientApplication(adminMsalConfig);

/** Scopes do login admin workforce (access token com aud=app + claim roles). */
export const adminLoginRequest: PopupRequest = {
  scopes: [adminScope],
};

// initialize() do msal-browser v3+ é obrigatório e deve rodar UMA vez. Memoiza a
// promise para que múltiplos chamadores (provider + login) compartilhem a mesma init.
let initPromise: Promise<void> | null = null;
export function ensureAdminMsalInitialized(): Promise<void> {
  if (!initPromise) {
    initPromise = adminMsalInstance.initialize();
  }
  return initPromise;
}

/** Conta workforce logada (primeira), ou null. */
export function getAdminAccount(): AccountInfo | null {
  return adminMsalInstance.getAllAccounts()[0] ?? null;
}

// Forma das claims que nos interessam do id token (App Role na claim `roles`).
interface AdminTokenClaims {
  roles?: string[];
}

/**
 * AC — a conta tem a App Role "Admin"? Lê o claim `roles` do id token da conta
 * (o Entra emite App Roles atribuídas tanto no id token quanto no access token).
 */
export function accountHasAdminRole(account: AccountInfo | null): boolean {
  if (!account) {
    return false;
  }
  const claims = account.idTokenClaims as AdminTokenClaims | undefined;
  return Array.isArray(claims?.roles) && claims.roles.includes(ADMIN_ROLE);
}

/**
 * Access token workforce para chamar o gateway (rota AdminOnly). Silencioso primeiro;
 * cai para popup se a sessão exigir interação. Retorna null se não houver conta admin.
 */
export async function getAdminAccessToken(): Promise<string | null> {
  await ensureAdminMsalInitialized();
  const account = getAdminAccount();
  if (!account) {
    return null;
  }

  try {
    const result = await adminMsalInstance.acquireTokenSilent({
      ...adminLoginRequest,
      account,
    });
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      const result = await adminMsalInstance.acquireTokenPopup(adminLoginRequest);
      return result.accessToken;
    }
    throw error;
  }
}
