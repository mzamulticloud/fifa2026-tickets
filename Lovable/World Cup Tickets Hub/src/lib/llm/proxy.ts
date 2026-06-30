// =============================================================================
// Story 2.5 / F5 — Camada de transporte LLM + DECISÃO DE SEGURANÇA DA API KEY.
//
// PROBLEMA (Task 5 / decisão explícita): a API key da LLM (Gemini/Groq/Mistral)
// NÃO pode ir no bundle do browser — qualquer um abriria o DevTools e a leria.
//
// DECISÃO ADOTADA: PROXY SERVER-SIDE MÍNIMO.
//   O front NUNCA conhece a key. Ele chama um endpoint de proxy
//   (VITE_LLM_PROXY_URL, ex.: o McpServer/gateway), que:
//     1. injeta a key (App Setting server-side) como header/param do provider;
//     2. encaminha o corpo (que o adapter já montou no formato do provider) ao
//        endpoint OFICIAL pinado (ADE-002 Inv 3);
//     3. devolve a resposta crua ao front.
//   O proxy é roteado pelo gateway YARP com Bearer Entra (coerência ADE-004/005).
//
// Caminho do proxy por provider:  {PROXY}/llm/{provider}{providerPath}
//   ex.: {PROXY}/llm/gemini/models/gemini-2.0-flash:generateContent
//        {PROXY}/llm/groq/chat/completions
//
// FALLBACK DE DEV (sem proxy): se VITE_LLM_PROXY_URL não estiver setado, este
// módulo LANÇA um erro explicativo em vez de tentar embutir a key — fail-safe,
// nunca vaza credencial. (Não há caminho "key no bundle".)
//
// Anti-hallucination (AC-15): este módulo NÃO inventa endpoints de provider — ele
// só encaminha ao proxy; o proxy server-side é quem fala o endpoint oficial.
// =============================================================================

import { getV2AccessToken } from '@/lib/authV2';
import type { LlmProviderName } from '@/lib/llm/types';

const PROXY_URL = import.meta.env.VITE_LLM_PROXY_URL ?? '';

/** True quando o proxy de LLM está configurado (sem ele, o chat fica indisponível). */
export const isLlmProxyConfigured = (): boolean => Boolean(PROXY_URL);

/**
 * Envia o corpo (já no formato do provider) ao proxy server-side, que injeta a key
 * e encaminha ao endpoint oficial. Bearer Entra para passar pelo gateway YARP.
 */
export async function llmFetch(
  provider: LlmProviderName,
  providerPath: string,
  body: unknown,
): Promise<Record<string, unknown>> {
  if (!PROXY_URL) {
    throw new Error(
      'LLM proxy não configurado (VITE_LLM_PROXY_URL). A API key da LLM NUNCA vai no ' +
        'bundle — configure o proxy server-side que injeta a key. Ver PORTAL-GUIDE.',
    );
  }

  const token = await getV2AccessToken();
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  // Bearer Entra para o gateway autorizar a rota do proxy (coerência F3).
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `${PROXY_URL}/llm/${provider}${providerPath}`;
  const response = await fetch(url, {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`LLM proxy (${provider}) falhou: HTTP ${response.status}.`);
  }

  return (await response.json()) as Record<string, unknown>;
}
