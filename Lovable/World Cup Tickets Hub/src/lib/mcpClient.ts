// =============================================================================
// Story 2.5 / F5 — Cliente MCP do frontend (AC-8/AC-9).
//
// Quando um LLM decide chamar uma tool, o front roteia a chamada `tools/call`
// JSON-RPC 2.0 para o McpServer .NET VIA o gateway YARP (rota /mcp), com
// `Authorization: Bearer <token Entra>` (MSAL, herança de F3). O gateway valida
// o JWT e propaga X-Entra-OID ao McpServer — o front NUNCA fala direto com o
// McpServer (tudo passa pelo gateway, ADE-004/ADE-005).
//
// Base URL = VITE_GATEWAY_V2_URL (mesma do apiV2.ts). Nunca hardcoded.
//
// Streamable HTTP: o endpoint /mcp aceita um único POST JSON-RPC com
// Accept: application/json, text/event-stream (spec MCP). A resposta pode vir
// como JSON puro ou como evento SSE (data: {...}); normalizamos ambos.
// =============================================================================

import { getV2AccessToken } from '@/lib/authV2';

const GATEWAY_V2_URL = import.meta.env.VITE_GATEWAY_V2_URL ?? '';

let rpcId = 0;

/**
 * Executa uma tool MCP no McpServer (via gateway). Lança em erro de transporte
 * ou erro JSON-RPC — o chamador (useLlmChat) decide como reportar ao LLM/usuário.
 */
export async function callMcpTool(
  toolName: string,
  args: Record<string, unknown>,
): Promise<unknown> {
  const token = await getV2AccessToken();
  if (!token) {
    throw new Error('Faça o "Login v2" (Entra) antes de usar o chatbot (as tools exigem token).');
  }

  const response = await fetch(`${GATEWAY_V2_URL}/mcp`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      // Streamable HTTP exige os dois Accept (spec MCP).
      Accept: 'application/json, text/event-stream',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      jsonrpc: '2.0',
      id: ++rpcId,
      method: 'tools/call',
      params: { name: toolName, arguments: args },
    }),
  });

  if (!response.ok) {
    throw new Error(`MCP tools/call falhou (HTTP ${response.status}) para ${toolName}.`);
  }

  const raw = await response.text();
  const payload = parseJsonRpc(raw);

  if (payload.error) {
    throw new Error(`MCP erro: ${payload.error.message ?? 'desconhecido'}`);
  }

  return payload.result;
}

interface JsonRpcResponse {
  result?: unknown;
  error?: { message?: string };
}

/** Aceita JSON puro OU formato SSE (linhas "data: {...}"). */
function parseJsonRpc(raw: string): JsonRpcResponse {
  const trimmed = raw.trim();
  if (trimmed.startsWith('{')) {
    return JSON.parse(trimmed) as JsonRpcResponse;
  }
  for (const line of trimmed.split('\n')) {
    const l = line.trim();
    if (l.toLowerCase().startsWith('data:')) {
      return JSON.parse(l.slice('data:'.length).trim()) as JsonRpcResponse;
    }
  }
  return JSON.parse(trimmed) as JsonRpcResponse;
}
