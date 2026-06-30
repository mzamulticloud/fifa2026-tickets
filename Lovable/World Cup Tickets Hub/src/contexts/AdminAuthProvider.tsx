// =============================================================================
// Story 2.11 / Quartas (F3) — Provider do login ADMIN workforce (Entra ID).
//
// Dirige a instância MSAL workforce gerenciada manualmente (src/lib/authAdmin.ts)
// e publica o estado no AdminAuthContext. Escopado à área /admin em App.tsx — não
// envolve a app inteira (o cliente CIAM não precisa dele). Coexiste com o MsalProvider
// (CIAM) e o AuthProvider (v1) sem conflito: instância MSAL separada, contexto separado.
// =============================================================================

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { EventType, type AccountInfo } from '@azure/msal-browser';
import {
  adminMsalInstance,
  ensureAdminMsalInitialized,
  isAdminEntraConfigured,
  adminLoginRequest,
  getAdminAccount,
  accountHasAdminRole,
} from '@/lib/authAdmin';
import { AdminAuthContext } from '@/contexts/AdminAuthContext';

// Avaliado uma vez: o login workforce está configurado por env?
const configured = isAdminEntraConfigured();

export const AdminAuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [account, setAccount] = useState<AccountInfo | null>(null);
  // Sem config (ex.: Oitavas), nada a inicializar → "pronto" de imediato (gate cai no v1).
  const [isReady, setIsReady] = useState<boolean>(!configured);
  const [isWorkingLogin, setIsWorkingLogin] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!configured) {
      return;
    }
    let active = true;

    ensureAdminMsalInitialized()
      .then(() => {
        if (!active) return;
        setAccount(getAdminAccount());
        setIsReady(true);
      })
      .catch((e) => {
        if (!active) return;
        console.error('Falha ao inicializar MSAL admin (workforce):', e);
        setError('Falha ao inicializar o login administrativo (Entra).');
        setIsReady(true);
      });

    // Mantém o estado da conta em sincronia com eventos do MSAL workforce.
    const callbackId = adminMsalInstance.addEventCallback((message) => {
      if (
        message.eventType === EventType.LOGIN_SUCCESS ||
        message.eventType === EventType.ACQUIRE_TOKEN_SUCCESS ||
        message.eventType === EventType.LOGOUT_SUCCESS
      ) {
        setAccount(getAdminAccount());
      }
    });

    return () => {
      active = false;
      if (callbackId) {
        adminMsalInstance.removeEventCallback(callbackId);
      }
    };
  }, []);

  const login = useCallback(async () => {
    setError(null);
    setIsWorkingLogin(true);
    try {
      await ensureAdminMsalInitialized();
      // PKCE via loginPopup (SPA público, sem client secret). O popup NÃO disputa o
      // hash de redirect da página (diferente do loginRedirect).
      const result = await adminMsalInstance.loginPopup(adminLoginRequest);
      adminMsalInstance.setActiveAccount(result.account);
      setAccount(result.account);
    } catch (e) {
      console.error('Falha no login admin (Entra workforce):', e);
      setError('Falha no login administrativo (Entra). Tente novamente.');
    } finally {
      setIsWorkingLogin(false);
    }
  }, []);

  const logout = useCallback(async () => {
    const acc = getAdminAccount();
    try {
      await adminMsalInstance.logoutPopup({ account: acc ?? undefined });
    } catch (e) {
      console.error('Falha no logout admin (Entra workforce):', e);
    } finally {
      setAccount(null);
    }
  }, []);

  const value = useMemo(
    () => ({
      isConfigured: configured,
      isReady,
      account,
      isAdmin: accountHasAdminRole(account),
      isWorkingLogin,
      error,
      login,
      logout,
    }),
    [isReady, account, isWorkingLogin, error, login, logout]
  );

  return <AdminAuthContext.Provider value={value}>{children}</AdminAuthContext.Provider>;
};
