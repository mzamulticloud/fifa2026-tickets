// =============================================================================
// Story 2.6 / F6 — Conexão em tempo real com o FlowHub (SignalR) + fallback polling.
//
// Conecta ao Hub via @microsoft/signalr (WebSocket, com auto-reconnect). Ao selecionar
// um correlationId, entra no grupo correlation-<id> ("Subscribe") e recebe eventos
// ("FlowEvent"). Se o WebSocket não conectar (CORS/proxy/firewall), cai para POLLING
// da timeline REST a cada 2s (AC-6 — fallback polling 2s).
//
// O NÓ ZERO do fluxo é o Gateway YARP (ADE-004) — nunca APIM.
// =============================================================================

import { useCallback, useEffect, useRef, useState } from 'react';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import {
  FLOW_HUB_URL,
  fetchTimeline,
  replayFlow,
  type FlowEvent,
} from '@/lib/flowApi';

const POLL_INTERVAL_MS = 2000;

export type FlowTransport = 'connecting' | 'signalr' | 'polling' | 'idle';

interface UseFlowConnectionResult {
  /** Eventos recebidos para o correlationId selecionado, em ordem de nó. */
  events: FlowEvent[];
  /** Transporte ativo (websocket SignalR ou fallback polling). */
  transport: FlowTransport;
  /** Seleciona um correlationId para observar (inscreve no grupo + dispara replay). */
  watch: (correlationId: string) => void;
  /** Limpa a seleção atual. */
  clear: () => void;
}

/**
 * Hook de observação de um fluxo por correlationId via SignalR (com fallback polling).
 */
export function useFlowConnection(): UseFlowConnectionResult {
  const [events, setEvents] = useState<FlowEvent[]>([]);
  const [transport, setTransport] = useState<FlowTransport>('idle');

  const connectionRef = useRef<HubConnection | null>(null);
  const pollTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const watchedRef = useRef<string | null>(null);

  const mergeEvent = useCallback((incoming: FlowEvent) => {
    setEvents((prev) => {
      // Substitui (por nodeIndex) ou adiciona; mantém ordenado por nó.
      const next = prev.filter((e) => e.nodeIndex !== incoming.nodeIndex);
      next.push(incoming);
      next.sort((a, b) => a.nodeIndex - b.nodeIndex);
      return next;
    });
  }, []);

  const stopPolling = useCallback(() => {
    if (pollTimerRef.current) {
      clearInterval(pollTimerRef.current);
      pollTimerRef.current = null;
    }
  }, []);

  const startPolling = useCallback(
    (correlationId: string) => {
      stopPolling();
      setTransport('polling');
      const poll = async () => {
        try {
          const timeline = await fetchTimeline(correlationId);
          timeline.forEach(mergeEvent);
        } catch {
          // Silencioso: mantém o último estado conhecido; tenta de novo no próximo tick.
        }
      };
      void poll();
      pollTimerRef.current = setInterval(poll, POLL_INTERVAL_MS);
    },
    [mergeEvent, stopPolling],
  );

  const watch = useCallback(
    (correlationId: string) => {
      watchedRef.current = correlationId;
      setEvents([]);

      const connection = connectionRef.current;
      if (connection && connection.state === HubConnectionState.Connected) {
        void connection.invoke('Subscribe', correlationId).catch(() => undefined);
        // Pede ao backend que releia a telemetria e empurre os eventos via SignalR.
        void replayFlow(correlationId).catch(() => undefined);
      } else {
        // Sem WebSocket disponível → fallback polling (AC-6).
        void replayFlow(correlationId).catch(() => undefined);
        startPolling(correlationId);
      }
    },
    [startPolling],
  );

  const clear = useCallback(() => {
    const correlationId = watchedRef.current;
    watchedRef.current = null;
    setEvents([]);
    stopPolling();
    const connection = connectionRef.current;
    if (correlationId && connection && connection.state === HubConnectionState.Connected) {
      void connection.invoke('Unsubscribe', correlationId).catch(() => undefined);
    }
    if (transport !== 'idle') {
      setTransport(connection?.state === HubConnectionState.Connected ? 'signalr' : 'idle');
    }
  }, [stopPolling, transport]);

  // Estabelece a conexão SignalR uma vez (com auto-reconnect). Fallback polling se falhar.
  useEffect(() => {
    if (!FLOW_HUB_URL) {
      return;
    }

    let disposed = false;
    setTransport('connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(FLOW_HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('FlowEvent', (event: FlowEvent) => {
      if (event.correlationId === watchedRef.current) {
        mergeEvent(event);
      }
    });

    connectionRef.current = connection;

    connection
      .start()
      .then(() => {
        if (disposed) {
          return;
        }
        setTransport('signalr');
        stopPolling();
        // Reassina se já havia um correlationId selecionado antes de conectar.
        const watched = watchedRef.current;
        if (watched) {
          void connection.invoke('Subscribe', watched).catch(() => undefined);
        }
      })
      .catch(() => {
        if (disposed) {
          return;
        }
        // WebSocket indisponível → fallback polling se já estiver observando algo.
        const watched = watchedRef.current;
        if (watched) {
          startPolling(watched);
        } else {
          setTransport('idle');
        }
      });

    return () => {
      disposed = true;
      stopPolling();
      void connection.stop().catch(() => undefined);
      connectionRef.current = null;
    };
  }, [mergeEvent, startPolling, stopPolling]);

  return { events, transport, watch, clear };
}

/** AC-9 — respeita a preferência do usuário por menos animação. */
export function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) {
      return;
    }
    const query = window.matchMedia('(prefers-reduced-motion: reduce)');
    setReduced(query.matches);
    const handler = (e: MediaQueryListEvent) => setReduced(e.matches);
    query.addEventListener('change', handler);
    return () => query.removeEventListener('change', handler);
  }, []);

  return reduced;
}
