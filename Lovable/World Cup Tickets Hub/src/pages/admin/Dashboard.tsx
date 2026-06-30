import React, { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  Ticket,
  Users,
  DollarSign,
  TrendingUp,
  Calendar,
  MapPin,
  Trophy,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import api from '@/lib/api';
// Quartas / "admin 100% workforce": stats e sales vêm do gateway com token WORKFORCE.
// matches/stadiums/teams continuam públicos (api.*, sem auth).
import * as apiAdmin from '@/lib/apiAdminV2';

interface AdminStats {
  total_users: number;
  total_sales: number;
  total_revenue: number;
  total_tickets_sold: number;
  total_matches: number;
  total_stadiums: number;
}

interface SaleRow {
  id: number;
  user_name: string;
  user_email: string;
  match_id: number;
  home_team: string;
  home_team_code: string;
  away_team: string;
  away_team_code: string;
  quantity: number;
  unit_price: number;
  total_price: number;
  status: string;
  created_at: string;
}

const Dashboard: React.FC = () => {
  const { data: statsData, isLoading: statsLoading, isError: statsError } = useQuery({
    queryKey: ['admin', 'stats'],
    queryFn: () => apiAdmin.getAdminStats(),
  });

  const { data: salesData, isLoading: salesLoading, isError: salesError } = useQuery({
    queryKey: ['admin', 'sales'],
    queryFn: () => apiAdmin.getSales(),
  });

  const { data: matchesData, isError: matchesError } = useQuery({
    queryKey: ['matches'],
    queryFn: () => api.getMatches(),
  });

  const { data: stadiumsData, isError: stadiumsError } = useQuery({
    queryKey: ['stadiums'],
    queryFn: () => api.getStadiums(),
  });

  const { data: teamsData, isError: teamsError } = useQuery({
    queryKey: ['teams'],
    queryFn: () => api.getTeams(),
  });

  // Toast de erro por query (TD-3)
  useEffect(() => {
    if (statsError) toast.error('Não foi possível carregar as estatísticas. Tente recarregar.');
  }, [statsError]);
  useEffect(() => {
    if (salesError) toast.error('Não foi possível carregar as vendas. Tente recarregar.');
  }, [salesError]);
  useEffect(() => {
    if (matchesError) toast.error('Não foi possível carregar os jogos. Tente recarregar.');
  }, [matchesError]);
  useEffect(() => {
    if (stadiumsError) toast.error('Não foi possível carregar os estádios. Tente recarregar.');
  }, [stadiumsError]);
  useEffect(() => {
    if (teamsError) toast.error('Não foi possível carregar as seleções. Tente recarregar.');
  }, [teamsError]);

  const stats = statsData?.data?.stats as AdminStats | undefined;
  const allSales = React.useMemo(() => (salesData?.data?.sales || []) as SaleRow[], [salesData]);
  const matches = matchesData?.data?.matches || [];
  const stadiums = stadiumsData?.data?.stadiums || [];
  const teams = teamsData?.data?.teams || [];

  // Últimas 5 vendas (backend já ordena por created_at DESC)
  const recentSales = allSales.slice(0, 5);

  // Top 4 jogos por quantidade vendida (agregado client-side)
  const topMatches = React.useMemo(() => {
    const aggregated = new Map<number, { match: string; sold: number }>();
    for (const s of allSales) {
      const key = s.match_id;
      const label = `${s.home_team_code || s.home_team} x ${s.away_team_code || s.away_team}`;
      const cur = aggregated.get(key) || { match: label, sold: 0 };
      cur.sold += s.quantity;
      aggregated.set(key, cur);
    }
    const totalTicketsSold = stats?.total_tickets_sold || 1;
    return Array.from(aggregated.values())
      .sort((a, b) => b.sold - a.sold)
      .slice(0, 4)
      .map((m) => ({
        ...m,
        percentage: Math.min(100, Math.round((m.sold / totalTicketsSold) * 100)),
      }));
  }, [allSales, stats]);

  const groupPhaseMatches = matches.filter((m) => m.stage === 'Fase de Grupos').length;
  const classifiedTeams = teams.filter((t) => t.group_name).length;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-display text-3xl">Dashboard</h1>
        <p className="text-muted-foreground">Visão geral do sistema de ingressos</p>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Vendas Totais
            </CardTitle>
            <DollarSign className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {statsLoading ? (
              <Skeleton className="h-8 w-32" />
            ) : (
              <div className="text-2xl font-bold">
                ${Number(stats?.total_revenue ?? 0).toLocaleString('pt-BR', {
                  minimumFractionDigits: 2,
                })}
              </div>
            )}
            <p className="text-xs text-muted-foreground mt-1">
              {stats ? `${stats.total_sales} vendas concluídas` : '—'}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Ingressos Vendidos
            </CardTitle>
            <Ticket className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {statsLoading ? (
              <Skeleton className="h-8 w-20" />
            ) : (
              <div className="text-2xl font-bold">{stats?.total_tickets_sold ?? 0}</div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Usuários Cadastrados
            </CardTitle>
            <Users className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {statsLoading ? (
              <Skeleton className="h-8 w-20" />
            ) : (
              <div className="text-2xl font-bold">{stats?.total_users ?? 0}</div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total de Jogos
            </CardTitle>
            <TrendingUp className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {statsLoading ? (
              <Skeleton className="h-8 w-20" />
            ) : (
              <div className="text-2xl font-bold">{stats?.total_matches ?? 0}</div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card className="bg-gradient-to-br from-primary/10 to-primary/5 border-primary/20">
          <CardContent className="pt-6">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-full bg-primary/20 flex items-center justify-center">
                <Calendar className="w-6 h-6 text-primary" />
              </div>
              <div>
                <p className="text-2xl font-bold">{groupPhaseMatches}</p>
                <p className="text-sm text-muted-foreground">Jogos Fase de Grupos</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-gradient-to-br from-green-500/10 to-green-500/5 border-green-500/20">
          <CardContent className="pt-6">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-full bg-green-500/20 flex items-center justify-center">
                <MapPin className="w-6 h-6 text-green-500" />
              </div>
              <div>
                <p className="text-2xl font-bold">{stadiums.length}</p>
                <p className="text-sm text-muted-foreground">Estádios</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-gradient-to-br from-gold/10 to-gold/5 border-gold/20">
          <CardContent className="pt-6">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-full bg-gold/20 flex items-center justify-center">
                <Trophy className="w-6 h-6 text-gold" />
              </div>
              <div>
                <p className="text-2xl font-bold">{classifiedTeams}</p>
                <p className="text-sm text-muted-foreground">Seleções Classificadas</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Tables Section */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent Sales */}
        <Card>
          <CardHeader>
            <CardTitle>Vendas Recentes</CardTitle>
          </CardHeader>
          <CardContent>
            {salesLoading ? (
              <div className="space-y-4">
                {[1, 2, 3].map((i) => (
                  <Skeleton key={i} className="h-12 w-full" />
                ))}
              </div>
            ) : recentSales.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nenhuma venda registrada ainda.</p>
            ) : (
              <div className="space-y-4">
                {recentSales.map((sale) => (
                  <div
                    key={sale.id}
                    className="flex items-center justify-between py-2 border-b border-border last:border-0"
                  >
                    <div>
                      <p className="font-medium">{sale.user_name}</p>
                      <p className="text-sm text-muted-foreground">
                        {sale.home_team_code} x {sale.away_team_code}
                      </p>
                    </div>
                    <div className="text-right">
                      <p className="font-medium">${Number(sale.total_price).toFixed(2)}</p>
                      <p className="text-sm text-muted-foreground">{sale.quantity} ingressos</p>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Top Matches */}
        <Card>
          <CardHeader>
            <CardTitle>Jogos Mais Vendidos</CardTitle>
          </CardHeader>
          <CardContent>
            {salesLoading ? (
              <div className="space-y-4">
                {[1, 2, 3].map((i) => (
                  <Skeleton key={i} className="h-8 w-full" />
                ))}
              </div>
            ) : topMatches.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nenhum jogo com vendas ainda.</p>
            ) : (
              <div className="space-y-4">
                {topMatches.map((item, index) => (
                  <div key={index} className="space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="font-medium">{item.match}</span>
                      <span className="text-sm text-muted-foreground">{item.sold} vendidos</span>
                    </div>
                    <div className="h-2 bg-secondary rounded-full overflow-hidden">
                      <div
                        className="h-full bg-primary rounded-full transition-all"
                        style={{ width: `${item.percentage}%` }}
                      />
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
};

export default Dashboard;
