import React, { useState, useEffect, useCallback, useRef } from 'react';
import {
  Search,
  Download,
  Calendar,
  DollarSign,
  Ticket,
  Filter,
  Eye,
  Loader2,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/hooks/use-toast';
// Quartas / "admin 100% workforce": sales e stats vêm do gateway com token WORKFORCE
// (apiAdmin), sem o token v1 do localStorage.
import * as apiAdmin from '@/lib/apiAdminV2';
import PaginationBar from '@/components/admin/PaginationBar';

interface Sale {
  id: number;
  quantity: number;
  unit_price: number;
  total_price: number;
  status: string;
  created_at: string;
  user_id: number;
  user_name: string;
  user_email: string;
  category: string;
  match_id: number;
  match_date: string;
  match_time: string;
  stage: string;
  home_team: string;
  home_team_code: string;
  home_team_flag?: string;
  away_team: string;
  away_team_code: string;
  away_team_flag?: string;
  stadium_name: string;
  stadium_city: string;
  stadium_country?: string;
}

interface Stats {
  total_users: number;
  total_sales: number;
  total_revenue: number;
  total_tickets_sold: number;
  total_matches: number;
  total_stadiums: number;
}

const AdminSales: React.FC = () => {
  const [sales, setSales] = useState<Sale[]>([]);
  const [pagination, setPagination] = useState({ page: 1, pageSize: 15, total: 0, totalPages: 0 });
  const [stats, setStats] = useState<Stats | null>(null);
  const statsRef = useRef(stats);
  useEffect(() => { statsRef.current = stats; }, [stats]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedSale, setSelectedSale] = useState<Sale | null>(null);
  const { toast } = useToast();

  // Debounce de busca
  const [debouncedSearch, setDebouncedSearch] = useState(search);
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 400);
    return () => clearTimeout(t);
  }, [search]);

  // Reset para página 1 ao mudar filtros
  useEffect(() => {
    setPagination((p) => ({ ...p, page: 1 }));
  }, [debouncedSearch, statusFilter]);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);

    const [salesRes, statsRes] = await Promise.all([
      apiAdmin.getSales({
        page: pagination.page,
        pageSize: pagination.pageSize,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        search: debouncedSearch || undefined,
      }),
      // Stats só carrega 1x na sessão; statsRef evita refetch sem virar dep do callback
      statsRef.current
        ? Promise.resolve({ data: { stats: statsRef.current }, error: undefined })
        : apiAdmin.getAdminStats(),
    ]);

    const errMsg = salesRes.error || statsRes.error;
    if (errMsg) {
      setSales([]);
      setError(errMsg);
      toast({ title: 'Erro', description: errMsg, variant: 'destructive' });
      setLoading(false);
      return;
    }

    setSales(salesRes.data?.sales ?? []);
    if (salesRes.data?.pagination) {
      setPagination((p) => ({
        ...p,
        total: salesRes.data!.pagination!.total,
        totalPages: salesRes.data!.pagination!.totalPages,
      }));
    }
    if (statsRes.data?.stats) setStats(statsRes.data.stats);
    setLoading(false);
  }, [pagination.page, pagination.pageSize, debouncedSearch, statusFilter, toast]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Filtragem agora é server-side
  const filteredSales = sales;

  const handleExport = () => {
    const csvContent = [
      ['ID', 'Cliente', 'Email', 'Jogo', 'Categoria', 'Quantidade', 'Valor Total', 'Status', 'Data'].join(','),
      ...filteredSales.map(sale => [
        sale.id,
        `"${sale.user_name}"`,
        sale.user_email,
        `"${sale.home_team} vs ${sale.away_team}"`,
        sale.category,
        sale.quantity,
        sale.total_price,
        sale.status,
        new Date(sale.created_at).toLocaleString('pt-BR')
      ].join(','))
    ].join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `vendas_${new Date().toISOString().split('T')[0]}.csv`;
    link.click();

    toast({
      title: "Exportado",
      description: "O relatório foi baixado com sucesso.",
    });
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'completed':
        return <Badge className="bg-green-500">Concluída</Badge>;
      case 'pending':
        return <Badge variant="secondary">Pendente</Badge>;
      case 'cancelled':
        return <Badge variant="destructive">Cancelada</Badge>;
      default:
        return <Badge variant="outline">{status}</Badge>;
    }
  };

  // Initial mount loading: só mostra fullscreen spinner se ainda não temos stats nem sales
  if (loading && !stats && sales.length === 0) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-8 h-8 animate-spin" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="space-y-4">
        <div>
          <h1 className="font-display text-3xl">Relatório de Vendas</h1>
          <p className="text-muted-foreground">Não foi possível carregar as vendas.</p>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Erro ao buscar dados</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">{error}</p>
            <div className="flex gap-2">
              <Button onClick={loadData}>Tentar novamente</Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="font-display text-3xl">Relatório de Vendas</h1>
          <p className="text-muted-foreground">
            {pagination.total.toLocaleString('pt-BR')} transações encontradas
          </p>
        </div>
        <Button variant="outline" onClick={handleExport}>
          <Download className="w-4 h-4 mr-2" />
          Exportar CSV
        </Button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Receita Total
            </CardTitle>
            <DollarSign className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-500">
              R$ {(stats?.total_revenue || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Apenas vendas concluídas
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
            <div className="text-2xl font-bold">{stats?.total_tickets_sold || 0}</div>
            <p className="text-xs text-muted-foreground mt-1">
              Total de ingressos
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total de Vendas
            </CardTitle>
            <DollarSign className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats?.total_sales || 0}</div>
            <p className="text-xs text-muted-foreground mt-1">
              Transações concluídas
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-4">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Buscar por cliente, jogo ou ID..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-10"
          />
        </div>
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger className="w-full sm:w-[180px]">
            <Filter className="w-4 h-4 mr-2" />
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Todos os status</SelectItem>
            <SelectItem value="completed">Concluída</SelectItem>
            <SelectItem value="pending">Pendente</SelectItem>
            <SelectItem value="cancelled">Cancelada</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50">
              <TableHead>ID</TableHead>
              <TableHead>Cliente</TableHead>
              <TableHead>Jogo</TableHead>
              <TableHead>Categoria</TableHead>
              <TableHead>Qtd</TableHead>
              <TableHead>Total</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Data</TableHead>
              <TableHead className="text-right">Ações</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filteredSales.length === 0 ? (
              <TableRow>
                <TableCell colSpan={9} className="text-center py-8 text-muted-foreground">
                  Nenhuma venda encontrada
                </TableCell>
              </TableRow>
            ) : (
              filteredSales.map((sale) => (
                <TableRow key={sale.id}>
                  <TableCell className="font-mono text-sm">#{sale.id}</TableCell>
                  <TableCell>
                    <div>
                      <p className="font-medium">{sale.user_name}</p>
                      <p className="text-xs text-muted-foreground">{sale.user_email}</p>
                    </div>
                  </TableCell>
                  <TableCell className="font-medium">
                    {sale.home_team_code || sale.home_team} vs {sale.away_team_code || sale.away_team}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{sale.category}</Badge>
                  </TableCell>
                  <TableCell>{sale.quantity}</TableCell>
                  <TableCell className="font-medium">
                    R$ {sale.total_price?.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell>{getStatusBadge(sale.status)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1 text-muted-foreground text-sm">
                      <Calendar className="w-3 h-3" />
                      {new Date(sale.created_at).toLocaleDateString('pt-BR')}
                    </div>
                  </TableCell>
                  <TableCell className="text-right">
                    <Button variant="ghost" size="icon" onClick={() => setSelectedSale(sale)}>
                      <Eye className="w-4 h-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>

        <PaginationBar
          page={pagination.page}
          pageSize={pagination.pageSize}
          total={pagination.total}
          totalPages={pagination.totalPages}
          onPageChange={(p) => setPagination((prev) => ({ ...prev, page: p }))}
          onPageSizeChange={(size) =>
            setPagination((prev) => ({ ...prev, pageSize: size, page: 1 }))
          }
          itemLabel="vendas"
        />
      </div>

      {/* Dialog de Detalhes */}
      <Dialog open={!!selectedSale} onOpenChange={() => setSelectedSale(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Detalhes da Venda #{selectedSale?.id}</DialogTitle>
          </DialogHeader>
          {selectedSale && (
            <div className="space-y-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-muted-foreground">Cliente</p>
                  <p className="font-medium">{selectedSale.user_name}</p>
                  <p className="text-sm text-muted-foreground">{selectedSale.user_email}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Status</p>
                  {getStatusBadge(selectedSale.status)}
                </div>
              </div>
              
              <div className="border-t pt-4">
                <p className="text-sm text-muted-foreground mb-2">Jogo</p>
                <div className="bg-muted/50 p-3 rounded-lg">
                  <p className="font-medium text-lg">
                    {selectedSale.home_team} vs {selectedSale.away_team}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {selectedSale.stadium_name} - {selectedSale.stadium_city}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {selectedSale.match_date ? new Date(selectedSale.match_date).toLocaleDateString('pt-BR') : '-'} às {selectedSale.match_time}
                  </p>
                </div>
              </div>
              
              <div className="border-t pt-4">
                <p className="text-sm text-muted-foreground mb-2">Ingresso</p>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <p className="text-sm text-muted-foreground">Categoria</p>
                    <p className="font-medium">{selectedSale.category}</p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Quantidade</p>
                    <p className="font-medium">{selectedSale.quantity}</p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Preço Unitário</p>
                    <p className="font-medium">R$ {selectedSale.unit_price?.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Total</p>
                    <p className="font-medium text-lg text-green-500">
                      R$ {selectedSale.total_price?.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    </p>
                  </div>
                </div>
              </div>
              
              <div className="border-t pt-4">
                <p className="text-sm text-muted-foreground">Data da Compra</p>
                <p className="font-medium">
                  {new Date(selectedSale.created_at).toLocaleString('pt-BR')}
                </p>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
};

export default AdminSales;
