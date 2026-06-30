// =============================================================================
// Story 2.11 / Quartas (F3) — Contexto React do login ADMIN workforce (Entra ID).
//
// Paralelo ao AuthContext (v1 bcrypt/JWT, intocado): aqui o estado vem da instância
// MSAL workforce gerenciada manualmente (src/lib/authAdmin.ts), exposta via um contexto
// próprio porque NÃO usamos MsalProvider/useMsal para o 2º mundo (decisão documentada
// em authAdmin.ts — useMsal só enxerga a instância CIAM já montada em App.tsx).
// =============================================================================

import { createContext, useContext } from 'react';
import type { AccountInfo } from '@azure/msal-browser';

export interface AdminAuthContextType {
  /** VITE_ADMIN_* (ou VITE_ENTRA_* de fallback) presentes — login workforce habilitado. */
  isConfigured: boolean;
  /** Instância MSAL workforce já inicializada (initialize() resolvido). */
  isReady: boolean;
  /** Conta workforce logada, ou null. */
  account: AccountInfo | null;
  /** Conta logada E com a App Role "Admin" na claim `roles` (libera /admin). */
  isAdmin: boolean;
  /** loginPopup em andamento (para desabilitar o botão / mostrar spinner). */
  isWorkingLogin: boolean;
  /** Última mensagem de erro de login/init, ou null. */
  error: string | null;
  /** Dispara o loginPopup workforce (App Role Admin). */
  login: () => Promise<void>;
  /** Encerra a sessão workforce (logoutPopup). */
  logout: () => Promise<void>;
}

export const AdminAuthContext = createContext<AdminAuthContextType | undefined>(undefined);

export const useAdminAuth = (): AdminAuthContextType => {
  const context = useContext(AdminAuthContext);
  if (!context) {
    throw new Error('useAdminAuth must be used within an AdminAuthProvider');
  }
  return context;
};
