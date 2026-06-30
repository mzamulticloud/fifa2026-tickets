// =============================================================================
// Story 2.6 / F6 — Diagrama dos 6 nós + "bolinha" animada (framer-motion) percorrendo
// Gateway YARP (nó 0) → Function Entry → Service Bus → Function Consumer → n8n → SQL.
//
// A bolinha avança até o nó mais avançado já atingido pelos eventos recebidos. Respeita
// prefers-reduced-motion (AC-9): sem motion, a posição é estática (transição instantânea).
// NÓ ZERO = Gateway YARP — NUNCA APIM (ADE-004).
// =============================================================================

import { motion } from 'framer-motion';
import { FlowNodeCard } from '@/components/flow/FlowNodeCard';
import { FLOW_NODES } from '@/lib/flowNodes';
import { usePrefersReducedMotion } from '@/hooks/useFlowConnection';
import type { FlowEvent } from '@/lib/flowApi';

interface FlowDiagramProps {
  /** Eventos recebidos (ordenados por nó). */
  events: FlowEvent[];
}

export function FlowDiagram({ events }: FlowDiagramProps) {
  const reducedMotion = usePrefersReducedMotion();

  const byIndex = new Map<number, FlowEvent>(events.map((e) => [e.nodeIndex, e]));
  // Nó mais avançado já atingido (para posicionar a bolinha).
  const reachedIndex = events.length > 0 ? Math.max(...events.map((e) => e.nodeIndex)) : -1;

  const total = FLOW_NODES.length;
  // Posição horizontal da bolinha em % (centro de cada coluna do grid).
  const ballLeftPercent =
    reachedIndex < 0 ? 0 : ((reachedIndex + 0.5) / total) * 100;

  return (
    <div className="w-full">
      {/* Trilho + bolinha (oculto de leitores de tela; o estado vem dos nós — AC-9). */}
      <div className="relative mb-3 h-2 w-full" aria-hidden="true">
        <div className="absolute inset-x-0 top-1/2 h-0.5 -translate-y-1/2 rounded bg-muted" />
        {reachedIndex >= 0 && (
          <motion.div
            className="absolute top-1/2 h-4 w-4 -translate-y-1/2 rounded-full bg-primary shadow-lg ring-2 ring-primary/40"
            initial={false}
            animate={{ left: `calc(${ballLeftPercent}% - 0.5rem)` }}
            transition={
              reducedMotion
                ? { duration: 0 }
                : { type: 'spring', stiffness: 120, damping: 18 }
            }
          />
        )}
      </div>

      {/* Os 6 nós (lista semântica para acessibilidade — AC-9). */}
      <ol
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6"
        aria-label="Fluxo da compra: 6 nós, do Gateway YARP ao SQL"
      >
        {FLOW_NODES.map((node) => (
          <li key={node.index}>
            <FlowNodeCard
              node={node}
              event={byIndex.get(node.index)}
              active={node.index <= reachedIndex}
            />
          </li>
        ))}
      </ol>
    </div>
  );
}
