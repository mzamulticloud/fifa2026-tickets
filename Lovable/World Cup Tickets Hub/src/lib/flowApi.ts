// =============================================================================
// Story 2.6 / F6 — Cliente REST do Flow Visualizer (serviço FlowEvents via gateway).
//
// Toda chamada passa pelo gateway YARP (VITE_FLOW_EVENTS_BASE_URL = .../flow-events),
// que é o NÓ ZERO do fluxo (injeta X-Correlation-ID). NUNCA referencia APIM — APIM não
// existe no EPIC-002 (ADE-004). As rotas REST são fallback de polling (2s) quando o
// WebSocket SignalR não conecta (AC-6).
// =============================================================================

const FLOW_BASE = import.meta.env.VITE_FLOW_EVENTS_BASE_URL ?? '';

/**
 * Os 6 tipos de evento = os 6 nós do diagrama, na ordem REAL do fluxo.
 * NÓ ZERO = GATEWAY_YARP_RECEIVED (nunca APIM). Espelha FlowEventType.cs do backend.
 */
export type FlowEventType =
  | 'GATEWAY_YARP_RECEIVED'
  | 'FUNCTION_ENTRY_PROCESSED'
  | 'SERVICE_BUS_PUBLISHED'
  | 'FUNCTION_CONSUMER_DONE'
  | 'N8N_WEBHOOK_TRIGGERED'
  | 'SQL_INSERTED';

export interface FlowEvent {
  correlationId: string;
  eventType: FlowEventType;
  /** Índice ordinal do nó (0..5) — posição na animação da bolinha. */
  nodeIndex: number;
  timestamp: string;
  durationMs?: number | null;
  status: 'ok' | 'error';
  message?: string | null;
}

export interface RecentPurchase {
  correlationId: string;
  timestamp: string;
  status: 'ok' | 'error';
}

/** AC-5 — últimas N compras (default 50) para a lista do front. */
export async function fetchRecentPurchases(top = 50): Promise<RecentPurchase[]> {
  const response = await fetch(`${FLOW_BASE}/api/flow/recent?top=${top}`, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    throw new Error(`Falha ao listar compras recentes (${response.status}).`);
  }
  return (await response.json()) as RecentPurchase[];
}

/** AC-6 — timeline completa de um correlationId (fallback de polling). */
export async function fetchTimeline(correlationId: string): Promise<FlowEvent[]> {
  const response = await fetch(`${FLOW_BASE}/api/flow/${encodeURIComponent(correlationId)}`, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    throw new Error(`Falha ao obter a timeline (${response.status}).`);
  }
  return (await response.json()) as FlowEvent[];
}

/**
 * AC-6 — pede ao backend que releia a telemetria e empurre os eventos via SignalR para
 * o grupo correlation-<id>, disparando a animação em tempo real nos clientes assinantes.
 */
export async function replayFlow(correlationId: string): Promise<void> {
  await fetch(`${FLOW_BASE}/api/flow/${encodeURIComponent(correlationId)}/replay`, {
    method: 'POST',
  });
}

/** URL absoluta do Hub SignalR (via gateway). Usada pelo useFlowConnection. */
export const FLOW_HUB_URL = `${FLOW_BASE}/hubs/flow`;
