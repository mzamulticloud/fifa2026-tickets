// =============================================================================
// Story 2.5 / F5 — Tipos neutros da camada LLM (AC-8/AC-10).
//
// Abstração agnóstica de provider: o chatbot fala com `LlmProvider`; cada adapter
// (gemini/groq/mistral) implementa esta interface. A troca é por env var
// `VITE_LLM_PROVIDER` (AC-10), sem mudança de código no componente.
// =============================================================================

import type { McpToolDefinition } from '@/lib/mcpTools';

export type LlmProviderName = 'gemini' | 'groq' | 'mistral';

export interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

/** Pedido do LLM para executar uma tool MCP (function call). */
export interface ToolCallRequest {
  /** ID opaco do call (necessário p/ correlacionar a resposta no protocolo OpenAI-compat). */
  id: string;
  name: string;
  arguments: Record<string, unknown>;
}

/** Resultado da execução de uma tool, devolvido ao LLM. */
export interface ToolCallResult {
  id: string;
  name: string;
  /** Conteúdo serializado (JSON) retornado pela tool MCP. */
  content: string;
}

/** Resposta de um turno do LLM: ou texto final, ou pedidos de tool. */
export interface LlmTurn {
  /** Texto da resposta (vazio quando o LLM só pediu tools). */
  text: string;
  /** Tools que o LLM quer executar antes de responder (vazio = resposta final). */
  toolCalls: ToolCallRequest[];
}

export interface LlmProvider {
  readonly name: LlmProviderName;

  /**
   * Envia o histórico + catálogo de tools e retorna o próximo turno do LLM.
   * `toolResults` carrega os resultados das tools chamadas no turno anterior
   * (vazio na primeira chamada).
   */
  chat(
    messages: ChatMessage[],
    tools: McpToolDefinition[],
    toolResults: ToolCallResult[],
  ): Promise<LlmTurn>;
}
