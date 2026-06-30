// =============================================================================
// Story 2.5 / F5 — Hook de orquestração chat ↔ LLM ↔ tools MCP (Task 4.3 / 5.5).
//
// Loop de function calling:
//   1. envia o histórico + catálogo de tools ao LLM (provider atual, AC-10);
//   2. se o LLM pediu tools → executa cada uma no McpServer VIA gateway YARP
//      (callMcpTool — Bearer Entra, X-Entra-OID propagado: AC-8/AC-9);
//   3. devolve os resultados ao LLM e repete (até MAX_TOOL_ITERS) até obter
//      uma resposta em texto.
//
// A integração LLM vive AQUI no front (ADE-002 Inv 3); o McpServer só expõe tools.
// =============================================================================

import { useCallback, useMemo, useState } from 'react';
import { createLlmProvider, getConfiguredProvider, type LlmProviderName } from '@/lib/llm';
import { MCP_TOOLS } from '@/lib/mcpTools';
import { callMcpTool } from '@/lib/mcpClient';
import type { ChatMessage, ToolCallResult } from '@/lib/llm/types';

/** Limite de rodadas de tool calls por mensagem (evita loop infinito). */
const MAX_TOOL_ITERS = 4;

export interface UseLlmChat {
  messages: ChatMessage[];
  loading: boolean;
  provider: LlmProviderName;
  error: string | null;
  send: (text: string) => Promise<void>;
  reset: () => void;
}

export function useLlmChat(providerOverride?: LlmProviderName): UseLlmChat {
  const provider = providerOverride ?? getConfiguredProvider();
  const llm = useMemo(() => createLlmProvider(provider), [provider]);

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reset = useCallback(() => {
    setMessages([]);
    setError(null);
  }, []);

  const send = useCallback(
    async (text: string) => {
      const trimmed = text.trim();
      if (!trimmed || loading) {
        return;
      }

      setError(null);
      setLoading(true);

      const history: ChatMessage[] = [...messages, { role: 'user', content: trimmed }];
      setMessages(history);

      try {
        let toolResults: ToolCallResult[] = [];
        let working = history;

        for (let iter = 0; iter < MAX_TOOL_ITERS; iter++) {
          const turn = await llm.chat(working, MCP_TOOLS, toolResults);

          // Resposta final em texto (sem mais tools): exibe e encerra.
          if (turn.toolCalls.length === 0) {
            const assistant: ChatMessage = {
              role: 'assistant',
              content: turn.text || '(sem resposta)',
            };
            setMessages([...working, assistant]);
            return;
          }

          // O LLM pediu tools → executa cada uma no McpServer via gateway.
          toolResults = [];
          for (const call of turn.toolCalls) {
            try {
              const result = await callMcpTool(call.name, call.arguments);
              toolResults.push({
                id: call.id,
                name: call.name,
                content: JSON.stringify(result),
              });
            } catch (toolErr) {
              toolResults.push({
                id: call.id,
                name: call.name,
                content: JSON.stringify({ error: (toolErr as Error).message }),
              });
            }
          }

          // Registra o texto parcial do LLM (se houver) antes da próxima rodada.
          if (turn.text) {
            working = [...working, { role: 'assistant', content: turn.text }];
          }
        }

        setError('Limite de chamadas de ferramentas atingido sem resposta final.');
      } catch (err) {
        console.error('Erro no chat LLM:', err);
        setError((err as Error).message ?? 'Erro ao conversar com a LLM.');
      } finally {
        setLoading(false);
      }
    },
    [llm, loading, messages],
  );

  return { messages, loading, provider, error, send, reset };
}
