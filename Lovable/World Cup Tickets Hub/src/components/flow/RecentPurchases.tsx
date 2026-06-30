// Story 2.6 / F6 — Lista das últimas 50 compras (sortable + searchable por correlationId, AC-5).
import { useMemo, useState } from 'react';
import { ArrowUpDown, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import type { RecentPurchase } from '@/lib/flowApi';

interface RecentPurchasesProps {
  purchases: RecentPurchase[];
  selectedCorrelationId: string | null;
  onSelect: (correlationId: string) => void;
}

type SortDir = 'asc' | 'desc';

export function RecentPurchases({ purchases, selectedCorrelationId, onSelect }: RecentPurchasesProps) {
  const [query, setQuery] = useState('');
  const [sortDir, setSortDir] = useState<SortDir>('desc');

  const rows = useMemo(() => {
    const filtered = purchases.filter((p) =>
      p.correlationId.toLowerCase().includes(query.trim().toLowerCase()),
    );
    return [...filtered].sort((a, b) => {
      const cmp = new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime();
      return sortDir === 'asc' ? cmp : -cmp;
    });
  }, [purchases, query, sortDir]);

  return (
    <div className="space-y-3">
      <div className="relative">
        <Search
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden="true"
        />
        <Input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Buscar por correlationId…"
          aria-label="Buscar compra por correlationId"
          className="pl-9"
        />
      </div>

      <div className="max-h-[60vh] overflow-auto rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Correlation ID</TableHead>
              <TableHead>
                <button
                  type="button"
                  onClick={() => setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'))}
                  className="inline-flex items-center gap-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                  aria-label={`Ordenar por data (${sortDir === 'asc' ? 'crescente' : 'decrescente'})`}
                >
                  Quando <ArrowUpDown className="h-3.5 w-3.5" aria-hidden="true" />
                </button>
              </TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={3} className="text-center text-sm text-muted-foreground">
                  Nenhuma compra encontrada.
                </TableCell>
              </TableRow>
            ) : (
              rows.map((p) => (
                <TableRow
                  key={p.correlationId}
                  tabIndex={0}
                  role="button"
                  aria-label={`Visualizar fluxo da compra ${p.correlationId}`}
                  aria-pressed={p.correlationId === selectedCorrelationId}
                  onClick={() => onSelect(p.correlationId)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      onSelect(p.correlationId);
                    }
                  }}
                  className={cn(
                    'cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary',
                    p.correlationId === selectedCorrelationId && 'bg-primary/10',
                  )}
                >
                  <TableCell className="font-mono text-xs">{p.correlationId}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">
                    {new Date(p.timestamp).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <Badge variant={p.status === 'error' ? 'destructive' : 'default'}>
                      {p.status}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
