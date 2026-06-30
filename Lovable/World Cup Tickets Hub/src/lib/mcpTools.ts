// =============================================================================
// Story 2.5 / F5 — Catálogo das tools MCP, no formato neutro consumido pelos
// adapters de LLM (Gemini/Groq/Mistral). Cada adapter traduz este catálogo para
// o formato de function calling do seu provider.
//
// Os nomes/descrições/schemas espelham as tools reais do McpServer .NET
// (src/Fifa2026.V2.McpServer/Tools/FifaTickerTools.cs). Mantidos em sincronia
// manual: o front declara o que o LLM pode pedir; a execução real acontece no
// McpServer via gateway YARP (AC-8/AC-9).
// =============================================================================

export interface ToolParameterSchema {
  type: 'object';
  properties: Record<string, { type: string; description: string }>;
  required: string[];
}

export interface McpToolDefinition {
  name: string;
  description: string;
  parameters: ToolParameterSchema;
}

/** As 3 tools expostas pelo McpServer (AC-3/4/5). */
export const MCP_TOOLS: McpToolDefinition[] = [
  {
    name: 'consultar_disponibilidade',
    description:
      'Consulta disponibilidade e preços de ingressos para uma partida da Copa 2026. ' +
      'Use quando o usuário perguntar se há ingressos para um jogo ou quanto custam.',
    parameters: {
      type: 'object',
      properties: {
        matchId: { type: 'integer', description: 'ID numérico da partida (opcional).' },
        matchDescription: {
          type: 'string',
          description: "Descrição da partida, ex.: 'Brasil x Argentina' (opcional).",
        },
      },
      required: [],
    },
  },
  {
    name: 'verificar_ingresso',
    description:
      'Verifica se um ingresso é válido e retorna comprador, partida, categoria e data da compra. ' +
      'Use quando o usuário perguntar se um ingresso/ID é válido.',
    parameters: {
      type: 'object',
      properties: {
        ingressoId: { type: 'integer', description: 'ID numérico do ingresso (compra) a verificar.' },
      },
      required: ['ingressoId'],
    },
  },
  {
    name: 'consultar_bracket',
    description:
      'Consulta os jogos de uma rodada do mata-mata (oitavas, quartas, semifinal, final) com placares. ' +
      'Use quando o usuário perguntar sobre confrontos/resultados de uma fase.',
    parameters: {
      type: 'object',
      properties: {
        rodada: {
          type: 'string',
          description: "Rodada do mata-mata, ex.: 'oitavas', 'quartas', 'semifinal', 'final'.",
        },
      },
      required: ['rodada'],
    },
  },
];
