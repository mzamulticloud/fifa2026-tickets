// =============================================================================
// Story 2.5 / F5 — Adapter OpenAI-compatible (Groq + Mistral, AC-10).
//
// Groq e Mistral expõem a MESMA forma de chat completions com function calling
// (campos `tools` / `tool_calls`). Um único adapter cobre ambos, parametrizado
// pelo provider + modelo. Endpoints pinados por ADE-002 Inv 3 (AC-15):
//   groq    : https://api.groq.com/openai/v1   (chat/completions)
//             fonte: https://console.groq.com/docs
//   mistral : https://api.mistral.ai/v1        (chat/completions)
//             fonte: https://docs.mistral.ai/
//
// SEGURANÇA: a key é injetada pelo PROXY server-side (ver proxy.ts) — nunca no
// bundle. Modelo configurável por env (VITE_GROQ_MODEL / VITE_MISTRAL_MODEL) com
// defaults conservadores documentados.
// =============================================================================

import type { McpToolDefinition } from '@/lib/mcpTools';
import { llmFetch } from '@/lib/llm/proxy';
import type {
  ChatMessage,
  LlmProvider,
  LlmProviderName,
  LlmTurn,
  ToolCallRequest,
  ToolCallResult,
} from '@/lib/llm/types';

interface OpenAiToolCall {
  id: string;
  type: 'function';
  function: { name: string; arguments: string };
}
interface OpenAiMessage {
  role: 'user' | 'assistant' | 'system' | 'tool';
  content: string | null;
  tool_calls?: OpenAiToolCall[];
  tool_call_id?: string;
}

export class OpenAiCompatProvider implements LlmProvider {
  constructor(
    readonly name: LlmProviderName,
    private readonly model: string,
  ) {}

  async chat(
    messages: ChatMessage[],
    tools: McpToolDefinition[],
    toolResults: ToolCallResult[],
  ): Promise<LlmTurn> {
    const oaMessages: OpenAiMessage[] = messages.map((m) => ({
      role: m.role,
      content: m.content,
    }));

    // Resultados de tools do turno anterior → mensagens role:tool (OpenAI-compat).
    for (const tr of toolResults) {
      oaMessages.push({ role: 'tool', tool_call_id: tr.id, content: tr.content });
    }

    const body = {
      model: this.model,
      messages: oaMessages,
      tools: tools.map((t) => ({
        type: 'function',
        function: { name: t.name, description: t.description, parameters: t.parameters },
      })),
      tool_choice: 'auto',
    };

    const data = (await llmFetch(this.name, '/chat/completions', body)) as {
      choices?: { message?: { content?: string | null; tool_calls?: OpenAiToolCall[] } }[];
    };

    const choice = data?.choices?.[0]?.message ?? {};
    const text: string = choice.content ?? '';
    const toolCalls: ToolCallRequest[] = (choice.tool_calls ?? []).map((tc: OpenAiToolCall) => ({
      id: tc.id,
      name: tc.function.name,
      arguments: safeParse(tc.function.arguments),
    }));

    return { text, toolCalls };
  }
}

function safeParse(json: string): Record<string, unknown> {
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return {};
  }
}
