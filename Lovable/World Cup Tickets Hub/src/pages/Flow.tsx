// =============================================================================
// Story 2.6 / F6 — Rota /flow: Flow Visualizer com correlation ID em tempo real.
//
// A "estrela didática" do workshop. Mostra, para cada compra v2, a bolinha animada
// percorrendo os 6 nós REAIS em tempo real via SignalR:
//   Gateway YARP (nó 0) → Function Entry → Service Bus → Function Consumer → n8n → SQL.
// NÓ ZERO = Gateway YARP — NUNCA APIM (ADE-004). O gateway injeta o X-Correlation-ID.
// =============================================================================

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { List, Radio, RefreshCw } from 'lucide-react';
import { Helmet } from 'react-helmet-async';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { FlowDiagram } from '@/components/flow/FlowDiagram';
import { RecentPurchases } from '@/components/flow/RecentPurchases';
import { FLOW_NODES } from '@/lib/flowNodes';
import { fetchRecentPurchases } from '@/lib/flowApi';
import { useFlowConnection, usePrefersReducedMotion } from '@/hooks/useFlowConnection';

export default function Flow() {
  const reducedMotion = usePrefersReducedMotion();
  // AC-9 — modo lista (sem animação) auto-ativado quando o usuário prefere menos movimento.
  const [listMode, setListMode] = useState(reducedMotion);
  const [selected, setSelected] = useState<string | null>(null);

  const { events, transport, watch, clear } = useFlowConnection();

  const {
    data: purchases = [],
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['flow-recent'],
    queryFn: () => fetchRecentPurchases(50),
    refetchInterval: 10_000,
  });

  useEffect(() => {
    setListMode(reducedMotion);
  }, [reducedMotion]);

  const handleSelect = (correlationId: string) => {
    setSelected(correlationId);
    watch(correlationId);
  };

  const transportLabel = useMemo(() => {
    switch (transport) {
      case 'signalr':
        return 'tempo real (SignalR)';
      case 'polling':
        return 'polling 2s (fallback)';
      case 'connecting':
        return 'conectando…';
      default:
        return 'inativo';
    }
  }, [transport]);

  return (
    <div className="container mx-auto px-4 py-8">
      <Helmet>
        <title>Flow Visualizer — Copa 2026 Tickets</title>
      </Helmet>

      <header className="mb-6 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold">Flow Visualizer</h1>
          <p className="text-sm text-muted-foreground">
            Acompanhe uma compra atravessando os 6 componentes em tempo real, por correlation ID:
            Gateway YARP → Function Entry → Service Bus → Function Consumer → n8n → SQL.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="secondary" className="gap-1">
            <Radio className="h-3 w-3" aria-hidden="true" />
            {transportLabel}
          </Badge>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setListMode((v) => !v)}
            aria-pressed={listMode}
          >
            <List className="mr-1 h-4 w-4" aria-hidden="true" />
            {listMode ? 'Modo diagrama' : 'Modo lista'}
          </Button>
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-[1fr_minmax(0,2fr)]">
        {/* Lista de compras (AC-5) */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base">Últimas compras</CardTitle>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={() => void refetch()}
              aria-label="Atualizar lista de compras"
            >
              <RefreshCw className={isRefetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} aria-hidden="true" />
            </Button>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <p className="text-sm text-muted-foreground">Carregando…</p>
            ) : (
              <RecentPurchases
                purchases={purchases}
                selectedCorrelationId={selected}
                onSelect={handleSelect}
              />
            )}
          </CardContent>
        </Card>

        {/* Visualização do fluxo (AC-6) */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {selected ? (
                <span className="font-mono text-sm">{selected}</span>
              ) : (
                'Selecione uma compra'
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {!selected ? (
              <p className="text-sm text-muted-foreground">
                Clique numa compra à esquerda para ver a bolinha percorrer os 6 nós.
              </p>
            ) : listMode ? (
              // AC-9 — modo lista acessível (sem animação): nós como lista de status.
              <ol className="space-y-2" aria-label="Eventos do fluxo (modo lista)">
                {FLOW_NODES.map((node) => {
                  const event = events.find((e) => e.nodeIndex === node.index);
                  return (
                    <li
                      key={node.index}
                      className="flex items-center justify-between rounded-md border p-2 text-sm"
                    >
                      <span>
                        <span className="font-medium">
                          {node.index}. {node.label}
                        </span>
                        {event?.message && (
                          <span className="ml-2 text-xs text-muted-foreground">{event.message}</span>
                        )}
                      </span>
                      <span className="flex items-center gap-2">
                        {event?.durationMs != null && (
                          <span className="text-xs text-muted-foreground">
                            {Math.round(event.durationMs)} ms
                          </span>
                        )}
                        <Badge variant={!event ? 'outline' : event.status === 'error' ? 'destructive' : 'default'}>
                          {!event ? 'aguardando' : event.status}
                        </Badge>
                      </span>
                    </li>
                  );
                })}
              </ol>
            ) : (
              <FlowDiagram events={events} />
            )}

            {selected && (
              <div className="mt-4">
                <Button type="button" variant="ghost" size="sm" onClick={() => { setSelected(null); clear(); }}>
                  Limpar seleção
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
