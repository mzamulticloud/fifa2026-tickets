import React from 'react';
import { Outlet, Navigate, Link, useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { useAdminAuth } from '@/contexts/AdminAuthContext';
import {
  LayoutDashboard,
  Calendar,
  MapPin,
  Users,
  Ticket,
  LogOut,
  Trophy,
  ChevronLeft,
  Menu,
  UserRound,
  ShieldCheck,
  ShieldAlert,
  Loader2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useState } from 'react';

const menuItems = [
  { href: '/admin', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/admin/matches', label: 'Jogos', icon: Calendar },
  { href: '/admin/stadiums', label: 'Estádios', icon: MapPin },
  { href: '/admin/users', label: 'Usuários', icon: Users },
  { href: '/admin/sales', label: 'Vendas', icon: Ticket },
];

// Casca centralizada usada pelas telas de gate (login admin / acesso negado / loader).
const GateShell: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="min-h-screen flex items-center justify-center bg-background p-4">
    <div className="w-full max-w-md rounded-xl border border-border bg-card p-8 shadow-lg text-center">
      {children}
    </div>
  </div>
);

// Quartas (F3) — tela de login ADMIN via Entra workforce (App Role "Admin").
const AdminEntraLogin: React.FC<{
  onLogin: () => void;
  working: boolean;
  error: string | null;
}> = ({ onLogin, working, error }) => (
  <GateShell>
    <div className="mx-auto mb-4 w-12 h-12 rounded-full bg-gradient-gold flex items-center justify-center">
      <ShieldCheck className="w-6 h-6 text-primary-foreground" />
    </div>
    <h1 className="font-display text-2xl text-gradient mb-2">Área Administrativa</h1>
    <p className="text-sm text-muted-foreground mb-6">
      O acesso administrativo exige login corporativo (Microsoft Entra ID) com a função
      <span className="font-medium"> Admin</span>.
    </p>
    <Button className="w-full gap-2" onClick={onLogin} disabled={working}>
      {working ? <Loader2 className="w-4 h-4 animate-spin" /> : <ShieldCheck className="w-4 h-4" />}
      Entrar como Admin (Entra)
    </Button>
    {error && <p className="mt-4 text-sm text-destructive">{error}</p>}
    <Link to="/" className="mt-6 inline-block text-xs text-muted-foreground hover:text-foreground">
      Voltar ao site
    </Link>
  </GateShell>
);

// Quartas (F3) — conta workforce autenticada, porém SEM a App Role "Admin" → bloqueio.
const AdminAccessDenied: React.FC<{
  name?: string;
  onLogout: () => void;
}> = ({ name, onLogout }) => (
  <GateShell>
    <div className="mx-auto mb-4 w-12 h-12 rounded-full bg-destructive/10 flex items-center justify-center">
      <ShieldAlert className="w-6 h-6 text-destructive" />
    </div>
    <h1 className="font-display text-2xl mb-2">Acesso negado</h1>
    <p className="text-sm text-muted-foreground mb-6">
      {name ? <><span className="font-medium">{name}</span>, sua </> : 'Sua '}
      conta está autenticada, mas não possui a função <span className="font-medium">Admin</span>
      {' '}necessária para a área administrativa.
    </p>
    <Button variant="outline" className="w-full gap-2" onClick={onLogout}>
      <LogOut className="w-4 h-4" />
      Sair e usar outra conta
    </Button>
    <Link to="/" className="mt-6 inline-block text-xs text-muted-foreground hover:text-foreground">
      Voltar ao site
    </Link>
  </GateShell>
);

const AdminLayout: React.FC = () => {
  const { isAuthenticated, user, logout } = useAuth();
  const admin = useAdminAuth();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);

  // === Quartas (F3) — gate da área /admin =====================================
  // Quando o login workforce está CONFIGURADO (VITE_ADMIN_*/VITE_ENTRA_* presentes),
  // a área administrativa passa a exigir conta Entra workforce COM a App Role "Admin"
  // (validada de ponta a ponta pela policy AdminOnly do gateway dual-issuer). Sem a
  // role → bloqueio (não entra). Quando NÃO configurado (ex.: lab Oitavas), mantém o
  // comportamento legado (gate v1/bcrypt) — mudança ADITIVA, retrocompatível.
  if (admin.isConfigured) {
    if (!admin.isReady) {
      return (
        <GateShell>
          <Loader2 className="w-8 h-8 mx-auto animate-spin text-primary" />
        </GateShell>
      );
    }
    if (!admin.account) {
      return (
        <AdminEntraLogin
          onLogin={admin.login}
          working={admin.isWorkingLogin}
          error={admin.error}
        />
      );
    }
    if (!admin.isAdmin) {
      return (
        <AdminAccessDenied
          name={admin.account.name ?? admin.account.username}
          onLogout={admin.logout}
        />
      );
    }
    // Autenticado + App Role "Admin" → segue para o shell administrativo.
  } else if (!isAuthenticated) {
    // Legado (sem workforce): mantém o gate v1/bcrypt.
    return <Navigate to="/login?redirect=/admin" replace />;
  }

  // Identidade exibida no rodapé / ação de logout conforme o mundo ativo.
  const displayName = admin.isConfigured
    ? (admin.account?.name ?? admin.account?.username)
    : user?.name;
  const displayEmail = admin.isConfigured ? admin.account?.username : user?.email;
  const handleLogout = admin.isConfigured ? admin.logout : logout;

  return (
    <div className="min-h-screen flex bg-background">
      {/* Sidebar */}
      <aside
        className={cn(
          "fixed left-0 top-0 h-full bg-card border-r border-border transition-all duration-300 z-50 flex flex-col",
          collapsed ? "w-16" : "w-64"
        )}
      >
        {/* Header */}
        <div className="h-16 flex items-center justify-between px-4 border-b border-border">
          {!collapsed && (
            <Link to="/admin" className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-full bg-gradient-gold flex items-center justify-center">
                <Trophy className="w-4 h-4 text-primary-foreground" />
              </div>
              <span className="font-display text-lg text-gradient">Admin</span>
            </Link>
          )}
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setCollapsed(!collapsed)}
            className={collapsed ? "mx-auto" : ""}
          >
            {collapsed ? <Menu className="w-5 h-5" /> : <ChevronLeft className="w-5 h-5" />}
          </Button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 py-4 px-2 space-y-1 overflow-y-auto">
          {menuItems.map((item) => {
            const isActive = location.pathname === item.href || 
              (item.href !== '/admin' && location.pathname.startsWith(item.href));
            
            return (
              <Link
                key={item.href}
                to={item.href}
                className={cn(
                  "flex items-center gap-3 px-3 py-2.5 rounded-lg transition-all duration-200",
                  isActive
                    ? "bg-primary text-primary-foreground"
                    : "text-muted-foreground hover:text-foreground hover:bg-secondary",
                  collapsed && "justify-center px-2"
                )}
                title={collapsed ? item.label : undefined}
              >
                <item.icon className="w-5 h-5 flex-shrink-0" />
                {!collapsed && <span className="font-medium">{item.label}</span>}
              </Link>
            );
          })}
        </nav>

        {/* Footer */}
        <div className="p-4 border-t border-border">
          {!collapsed && (
            <div className="mb-3 px-2">
              <p className="text-sm font-medium truncate">{displayName}</p>
              <p className="text-xs text-muted-foreground truncate">{displayEmail}</p>
            </div>
          )}
          <div className="flex gap-2">
            <Link to="/" className="flex-1" title="Modo Usuário">
              <Button variant="outline" size="sm" className={cn("w-full", collapsed && "px-2")}>
                {collapsed ? (
                  <UserRound className="w-4 h-4" />
                ) : (
                  <>
                    <UserRound className="w-4 h-4 mr-2" />
                    Modo Usuário
                  </>
                )}
              </Button>
            </Link>
            {!collapsed && (
              <Button variant="ghost" size="sm" onClick={handleLogout} title="Sair">
                <LogOut className="w-4 h-4" />
              </Button>
            )}
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <main
        className={cn(
          "flex-1 transition-all duration-300",
          collapsed ? "ml-16" : "ml-64"
        )}
      >
        <div className="p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
};

export default AdminLayout;
