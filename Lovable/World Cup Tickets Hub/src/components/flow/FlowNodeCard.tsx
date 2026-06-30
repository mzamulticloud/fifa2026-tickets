// Story 2.6 / F6 — Um nó do diagrama de fluxo, com inspeção via Sheet (AC-6/AC-9).
import { ShieldCheck, Zap, Mailbox, Cog, Workflow, Database, type LucideIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { cn } from '@/lib/utils';
import type { FlowNodeMeta } from '@/lib/flowNodes';
import type { FlowEvent } from '@/lib/flowApi';

const ICONS: Record<FlowNodeMeta['icon'], LucideIcon> = {
  ShieldCheck,
  Zap,
  Mailbox,
  Cog,
  Workflow,
  Database,
};

interface FlowNodeCardProps {
  node: FlowNodeMeta;
  /** Evento recebido para este nó (se já chegou). */
  event?: FlowEvent;
  /** Nó já foi atingido pela bolinha. */
  active: boolean;
}

export function FlowNodeCard({ node, event, active }: FlowNodeCardProps) {
  const Icon = ICONS[node.icon];
  const status = event?.status;
  const isError = status === 'error';

  // AC-9 — aria-label descritivo do nó + estado atual.
  const ariaLabel =
    `Nó ${node.index}: ${node.label}. ${node.description} ` +
    (active ? (isError ? 'Status: erro.' : 'Status: concluído.') : 'Status: aguardando.');

  return (
    <Sheet>
      <SheetTrigger asChild>
        <button
          type="button"
          aria-label={ariaLabel}
          className={cn(
            'flex w-full flex-col items-center gap-2 rounded-xl border p-3 text-center transition-colors',
            'focus:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2',
            active && !isError && 'border-primary bg-primary/10',
            active && isError && 'border-destructive bg-destructive/10',
            !active && 'border-muted bg-muted/30 opacity-70',
          )}
        >
          <span
            className={cn(
              'flex h-10 w-10 items-center justify-center rounded-full',
              active && !isError && 'bg-primary text-primary-foreground',
              active && isError && 'bg-destructive text-destructive-foreground',
              !active && 'bg-muted text-muted-foreground',
            )}
          >
            <Icon className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="text-xs font-medium leading-tight">{node.label}</span>
          {active && event?.durationMs != null && (
            <Badge variant="secondary" className="text-[10px]">
              {Math.round(event.durationMs)} ms
            </Badge>
          )}
          {active && (
            <Badge variant={isError ? 'destructive' : 'default'} className="text-[10px]">
              {isError ? 'erro' : 'ok'}
            </Badge>
          )}
        </button>
      </SheetTrigger>

      <SheetContent>
        <SheetHeader>
          <SheetTitle>
            Nó {node.index} — {node.label}
          </SheetTitle>
          <SheetDescription>{node.description}</SheetDescription>
        </SheetHeader>

        <div className="mt-6 space-y-3 text-sm">
          <div>
            <span className="font-medium">Tipo de evento: </span>
            <code className="rounded bg-muted px-1 py-0.5 text-xs">{node.eventType}</code>
          </div>
          {event ? (
            <>
              <div>
                <span className="font-medium">Status: </span>
                <Badge variant={isError ? 'destructive' : 'default'}>{event.status}</Badge>
              </div>
              {event.durationMs != null && (
                <div>
                  <span className="font-medium">Duração no hop: </span>
                  {Math.round(event.durationMs)} ms
                </div>
              )}
              <div>
                <span className="font-medium">Timestamp: </span>
                {new Date(event.timestamp).toLocaleString()}
              </div>
              <div>
                <span className="font-medium">Payload / trace: </span>
                <pre className="mt-1 max-h-48 overflow-auto rounded bg-muted p-2 text-xs">
                  {event.message ?? '(sem mensagem)'}
                </pre>
              </div>
            </>
          ) : (
            <p className="text-muted-foreground">Aguardando evento deste nó…</p>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
