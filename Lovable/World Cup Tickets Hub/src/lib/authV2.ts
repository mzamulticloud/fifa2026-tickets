// =============================================================================
// Story 2.11 / Quartas — Identidade do CLIENTE via Microsoft Entra External ID
// (CIAM) com MSAL.js (Authorization Code Flow + PKCE).
//
// ADE-007 Inv 1/2 (SUPERSEDE ADE-005): a identidade do cliente final migra do tenant
// workforce (login.microsoftonline.com) para o tenant CIAM (Entra External ID). A
// ÚNICA mudança de string no código de identidade é a authority/issuer (Inv 2):
//
//   workforce (ADE-005, antigo):  https://login.microsoftonline.com/<tenantId>
//   CIAM (ADE-007, Quartas):      https://<tenant>.ciamlogin.com/   ← este módulo
//
// Para o Entra External ID / CIAM a authority NÃO é login.microsoftonline.com; é o
// endpoint ciamlogin.com do tenant CIAM, e o MSAL EXIGE knownAuthorities listando o
// host CIAM (cenário B2C/CIAM — a discovery de instância padrão do AAD não cobre
// ciamlogin.com). Discovery OIDC do CIAM:
//   https://<tenant>.ciamlogin.com/<tenantId>/v2.0/.well-known/openid-configuration
//
// O fluxo v1 (bcrypt + JWT HS256 local, em src/lib/api.ts) permanece INTOCADO — este
// módulo é paralelo, para comparação didática v1 vs v2 (ADE-000 Inv 1).
//
// Sem client secret no browser: PKCE protege o code exchange (SPA público — Inv 1).
// Valores de configuração vêm de variáveis Vite (VITE_CIAM_*) — nunca hardcoded (AC-19).
//
// Anti-hallucination (AC-19, validado em build):
//   - APIs PublicClientApplication, loginPopup, acquireTokenSilent, acquireTokenPopup
//     e os tipos Configuration/RedirectRequest: docs oficiais @azure/msal-browser.
//   - knownAuthorities: Array<string> — confirmado em @azure/msal-browser 3.30.0
//     (BrowserAuthOptions; ClientConfiguration: "Used in B2C scenarios"). É o
//     mecanismo oficial para registrar a authority CIAM/External ID.
//   - Formato da authority CIAM (https://<tenant>.ciamlogin.com/) + knownAuthorities
//     com o host: padrão oficial Microsoft Entra External ID para SPA. Se o tenant do
//     instrutor exigir uma authority diferente (ex.: com tenantId no path), ela é
//     CONFIGURÁVEL via VITE_CIAM_AUTHORITY (valor completo) — não há string inventada.
// =============================================================================

import {
  PublicClientApplication,
  type Configuration,
  type AccountInfo,
  type RedirectRequest,
  InteractionRequiredAuthError,
} from '@azure/msal-browser';

const clientId = import.meta.env.VITE_CIAM_CLIENT_ID ?? '';

// Authority COMPLETA do tenant CIAM (Entra External ID), ex.:
//   https://contoso.ciamlogin.com/
// O instrutor pluga o valor exato do tenant CIAM (handoff §3 — pré-provisionado).
// Mantemos o valor configurável por env (não derivamos um subdomínio fixo) para não
// inventar o nome do tenant — é o instrutor quem o conhece (AC-19 / Art. IV).
const ciamAuthority = import.meta.env.VITE_CIAM_AUTHORITY ?? '';

/**
 * Deriva o host da authority CIAM (ex.: 'contoso.ciamlogin.com') a partir da URL
 * completa, para registrá-lo em knownAuthorities. O MSAL exige que o host da authority
 * CIAM/B2C esteja em knownAuthorities (a discovery de instância padrão do AAD não o
 * cobre). Retorna '' se a authority não estiver configurada (módulo fica inerte).
 */
function deriveKnownAuthorityHost(authority: string): string {
  if (!authority) {
    return '';
  }
  try {
    return new URL(authority).host;
  } catch {
    // Authority malformada → não registra host (login ficará indisponível, fail-safe).
    return '';
  }
}

const knownAuthorityHost = deriveKnownAuthorityHost(ciamAuthority);

const redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI ?? window.location.origin;

// Scope da API exposta pela App Registration SPA no tenant CIAM (ex.:
// api://<client-id>/purchase.write). Fallback para o formato padrão se
// VITE_ENTRA_SCOPE não estiver definido (reaproveitado da convenção de 2.3).
const apiScope =
  import.meta.env.VITE_ENTRA_SCOPE ??
  (clientId ? `api://${clientId}/purchase.write` : 'openid');

/**
 * True quando as variáveis mínimas de identidade v2 (CIAM) estão configuradas.
 * Agora baseado nas vars CIAM (VITE_CIAM_*) — ADE-007 Inv 1. Inclui o host derivado
 * para garantir que knownAuthorities será preenchido (login CIAM exige isso).
 */
export const isEntraConfigured = (): boolean =>
  Boolean(clientId && ciamAuthority && knownAuthorityHost);

const msalConfig: Configuration = {
  auth: {
    clientId,
    // Authority CIAM (ciamlogin.com), não login.microsoftonline.com (ADE-007 Inv 2).
    authority: ciamAuthority,
    // knownAuthorities: obrigatório para CIAM/External ID — o MSAL valida a authority
    // CIAM contra esta lista em vez da discovery de instância padrão do AAD.
    knownAuthorities: knownAuthorityHost ? [knownAuthorityHost] : [],
    redirectUri,
    // Não pede consentimento de novo a cada navegação.
    navigateToLoginRequestUrl: false,
  },
  cache: {
    // sessionStorage: token não persiste entre abas/fechamento — mais seguro p/ SPA.
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
};

/**
 * Instância única do MSAL. Deve ser inicializada (await msalInstance.initialize())
 * antes do primeiro uso — feito no bootstrap (main.tsx / MsalProvider).
 */
export const msalInstance = new PublicClientApplication(msalConfig);

/** Scopes solicitados no login v2 CIAM (escopo da API + OIDC básico). */
export const loginRequest: RedirectRequest = {
  scopes: [apiScope],
};

/**
 * AC-8/AC-11 — obtém um access token v2 (CIAM) silenciosamente (acquireTokenSilent);
 * se a sessão exigir interação (token expirado sem refresh, consent), cai para popup.
 * Retorna null se não houver conta logada. MSAL é agnóstico ao issuer: o mesmo código
 * funciona para CIAM e workforce — só muda a authority (ADE-007 Inv 2).
 */
export async function getV2AccessToken(): Promise<string | null> {
  const account: AccountInfo | undefined = msalInstance.getAllAccounts()[0];
  if (!account) {
    return null;
  }

  try {
    const result = await msalInstance.acquireTokenSilent({
      ...loginRequest,
      account,
    });
    return result.accessToken;
  } catch (error) {
    // Token expirado/sem refresh válido → interação explícita (cenário didático).
    if (error instanceof InteractionRequiredAuthError) {
      const result = await msalInstance.acquireTokenPopup(loginRequest);
      return result.accessToken;
    }
    throw error;
  }
}
