// =============================================================================
// Story 2.3 / F3 — Botão "Login v2" (identidade Entra via MSAL.js).
//
// AC-5 — login OIDC (Authorization Code + PKCE) usando loginPopup; mostra o nome
// da conta logada e permite logout. É um fluxo SEPARADO do login v1 (bcrypt+JWT)
// — ambos coexistem para comparação didática. Renderiza um aviso discreto se a
// App Registration ainda não estiver configurada (VITE_ENTRA_* ausentes).
// =============================================================================

import React from 'react';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { Button } from '@/components/ui/button';
import { ShieldCheck, LogOut } from 'lucide-react';
import { loginRequest, isEntraConfigured } from '@/lib/authV2';

export const LoginV2Button: React.FC = () => {
  const { instance, accounts } = useMsal();
  const isV2Authenticated = useIsAuthenticated();

  if (!isEntraConfigured()) {
    // F1/Oitavas: sem App Registration (VITE_ENTRA_* ausentes), o login v2 fica
    // OCULTO — só aparece quando F3 (Entra) estiver configurada. Antes renderizava
    // um aviso "Login v2 (Entra) não configurado" que não deve aparecer nesta fase.
    return null;
  }

  const handleLogin = async () => {
    try {
      // PKCE: loginPopup faz o Authorization Code Flow sem client secret.
      await instance.loginPopup(loginRequest);
    } catch (error) {
      console.error('Falha no Login v2 (Entra):', error);
    }
  };

  const handleLogout = async () => {
    await instance.logoutPopup({ account: accounts[0] });
  };

  if (isV2Authenticated) {
    return (
      <div className="hidden lg:flex items-center gap-2">
        <span className="text-xs text-muted-foreground" title="Sessão v2 (Entra OIDC)">
          v2: {accounts[0]?.name ?? accounts[0]?.username}
        </span>
        <Button variant="ghost" size="icon" onClick={handleLogout} title="Logout v2">
          <LogOut className="w-4 h-4" />
        </Button>
      </div>
    );
  }

  return (
    <Button
      variant="outline"
      size="sm"
      className="hidden lg:flex gap-2"
      onClick={handleLogin}
      title="Login federado via Entra ID (OIDC + PKCE) — fluxo v2"
    >
      <ShieldCheck className="w-4 h-4" />
      Login v2
    </Button>
  );
};
