// =============================================================================
// Story 2.5 / F5 — Adapter Gemini 2.0 Flash (provider DEFAULT, AC-8).
//
// Endpoint/modelo pinados por ADE-002 Inv 3 (anti-hallucination AC-15):
//   Base oficial: https://generativelanguage.googleapis.com/v1beta
//   Modelo:       models/gemini-2.0-flash
//   Method:       :generateContent
//   Function calling: campo `tools[].functionDeclarations` + `tool_config`
//   Fonte: https://ai.google.dev/api/generate-content
//
// SEGURANÇA (decisão de chave): a API key NÃO vai no bundle. O front chama o
// PROXY server-side (llmProxyBase()) que injeta a key como header e encaminha ao
// endpoint oficial. Em DEV sem proxy, pode-se apontar VITE_LLM_DIRECT_BASE para o
// endpoint oficial e passar a key via proxy local — nunca embutida no código.
// Ver src/lib/llm/proxy.ts.
// =============================================================================

import type { McpToolDefinition } from '@/lib/mcpTools';
import { llmFetch } from '@/lib/llm/proxy';
import type { ChatMessage, LlmProvider, LlmTurn, ToolCallRequest, ToolCallResult } from '@/lib/llm/types';

// gemini-2.0-flash saiu do free tier (quota free = 0 em 2026). gemini-2.5-flash
// é o flash atual com free tier disponível. Sobrescrevível por VITE_GEMINI_MODEL.
const MODEL = import.meta.env.VITE_GEMINI_MODEL ?? 'gemini-2.5-flash';

interface GeminiPart {
  text?: string;
  functionCall?: { name: string; args: Record<string, unknown> };
  functionResponse?: { name: string; response: Record<string, unknown> };
}
interface GeminiContent {
  role: 'user' | 'model' | 'function';
  parts: GeminiPart[];
}

export class GeminiProvider implements LlmProvider {
  readonly name = 'gemini' as const;

  async chat(
    messages: ChatMessage[],
    tools: McpToolDefinition[],
    toolResults: ToolCallResult[],
  ): Promise<LlmTurn> {
    const contents: GeminiContent[] = messages
      .filter((m) => m.role !== 'system')
      .map((m) => ({
        role: m.role === 'assistant' ? 'model' : 'user',
        parts: [{ text: m.content }],
      }));

    // Resultados de tools do turno anterior viram functionResponse parts.
    for (const tr of toolResults) {
      contents.push({
        role: 'function',
        parts: [{ functionResponse: { name: tr.name, response: { result: tr.content } } }],
      });
    }

    const body = {
      contents,
      tools: [
        {
          functionDeclarations: tools.map((t) => ({
            name: t.name,
            description: t.description,
            parameters: t.parameters,
          })),
        },
      ],
      // Deixa o modelo decidir quando chamar (modo AUTO — doc oficial).
      tool_config: { function_calling_config: { mode: 'AUTO' } },
    };

    const data = (await llmFetch('gemini', `/models/${MODEL}:generateContent`, body)) as {
      candidates?: { content?: { parts?: GeminiPart[] } }[];
    };

    const parts: GeminiPart[] = data?.candidates?.[0]?.content?.parts ?? [];
    const text = parts.filter((p) => p.text).map((p) => p.text).join('');
    const toolCalls: ToolCallRequest[] = parts
      .filter((p) => p.functionCall)
      .map((p, i) => ({
        id: `${p.functionCall!.name}-${i}`,
        name: p.functionCall!.name,
        arguments: p.functionCall!.args ?? {},
      }));

    return { text, toolCalls };
  }
}
