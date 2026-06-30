// =============================================================================
// Story 2.5 / F5 — Factory de provider LLM com SWITCH POR ENV VAR (AC-10).
//
// Trocar VITE_LLM_PROVIDER=gemini|groq|mistral muda o provider SEM mudar código no
// componente (portabilidade — demo bônus). Default: gemini (ADE-002 Inv 3).
//
// Modelos por env (com defaults documentados):
//   gemini  → gemini-2.0-flash (fixo, AC-8)
//   groq    → VITE_GROQ_MODEL    (default: llama-3.3-70b-versatile)
//   mistral → VITE_MISTRAL_MODEL (default: mistral-large-latest)
// Os modelos default são pinados por NOME aqui; o @dev confirma disponibilidade na
// doc oficial de cada provider (AC-15). Endpoints oficiais ficam no proxy server-side.
// =============================================================================

import { GeminiProvider } from '@/lib/llm/gemini';
import { OpenAiCompatProvider } from '@/lib/llm/openaiCompat';
import type { LlmProvider, LlmProviderName } from '@/lib/llm/types';

const DEFAULT_GROQ_MODEL = 'llama-3.3-70b-versatile';
const DEFAULT_MISTRAL_MODEL = 'mistral-large-latest';

/** Lê o provider atual da env (AC-10). Default gemini. */
export function getConfiguredProvider(): LlmProviderName {
  const raw = (import.meta.env.VITE_LLM_PROVIDER ?? 'gemini').toLowerCase();
  if (raw === 'groq' || raw === 'mistral' || raw === 'gemini') {
    return raw;
  }
  return 'gemini';
}

/** Cria o provider concreto a partir do nome (env var ou override do usuário). */
export function createLlmProvider(name: LlmProviderName = getConfiguredProvider()): LlmProvider {
  switch (name) {
    case 'groq':
      return new OpenAiCompatProvider(
        'groq',
        import.meta.env.VITE_GROQ_MODEL ?? DEFAULT_GROQ_MODEL,
      );
    case 'mistral':
      return new OpenAiCompatProvider(
        'mistral',
        import.meta.env.VITE_MISTRAL_MODEL ?? DEFAULT_MISTRAL_MODEL,
      );
    case 'gemini':
    default:
      return new GeminiProvider();
  }
}

export type { LlmProvider, LlmProviderName } from '@/lib/llm/types';
export { isLlmProxyConfigured } from '@/lib/llm/proxy';
