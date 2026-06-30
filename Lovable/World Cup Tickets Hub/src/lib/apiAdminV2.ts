// =============================================================================
// Story 2.11 / Quartas (F3) — Cliente das chamadas ADMINISTRATIVAS ao gateway YARP
// com Bearer token WORKFORCE (Entra ID + App Role "Admin").
//
// Espelha o padrão de src/lib/apiV2.ts (cliente CIAM), mas pega o token da instância
// MSAL workforce (src/lib/authAdmin.ts). As rotas admin do gateway exigem a policy
// AdminOnly (RequireRole("Admin")):
//   - token workforce com role "Admin" → 200
//   - token CIAM (cliente) válido       → 403 (autenticado, sem a role)
//   - sem token / inválido              → 401
//
// Base URL via VITE_GATEWAY_V2_URL (mesmo gateway do fluxo cliente). Nunca hardcoded.
// =============================================================================

import { getAdminAccessToken } from '@/lib/authAdmin';
import type { Sale, Pagination, AdminStats } from '@/lib/api';

const GATEWAY_V2_URL = import.meta.env.VITE_GATEWAY_V2_URL ?? '';

export interface AdminApiResult<T> {
  data?: T;
  error?: string;
  /** HTTP status (útil para distinguir 401 vs 403 no lab didático). */
  status?: number;
}

export interface AdminPingResponse {
  status: string;
  scope: string;
}

/**
 * GET autenticado ao gateway com Bearer WORKFORCE. Centraliza a obtenção do token
 * admin (MSAL workforce), o tratamento de 401/403 (didático nas Quartas) e os erros de
 * conexão. NÃO usa o token v1 do localStorage — só o token workforce do gateway.
 */
async function adminGet<T>(path: string): Promise<AdminApiResult<T>> {
  const token = await getAdminAccessToken();
  if (!token) {
    return { error: 'Faça o login administrativo (Entra workforce) antes de chamar /admin.' };
  }

  try {
    const response = await fetch(`${GATEWAY_V2_URL}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      let message = `Erro na requisição admin (${response.status})`;
      if (response.status === 401) {
        message = 'Não autorizado (401): token workforce ausente, expirado ou inválido.';
      } else if (response.status === 403) {
        message = 'Proibido (403): autenticado, mas sem a App Role "Admin".';
      }
      return { error: message, status: response.status };
    }

    const data = (await response.json()) as T;
    return { data, status: response.status };
  } catch (error) {
    console.error('API admin (gateway) error:', error);
    return { error: 'Erro de conexão com o gateway (rota admin).' };
  }
}

/**
 * GET /admin/ping no gateway com Bearer workforce. Demonstra a separação dos dois
 * mundos no próprio gateway (AdminOnly). Sem token workforce → erro local antes da call.
 */
export function adminPing(): Promise<AdminApiResult<AdminPingResponse>> {
  return adminGet<AdminPingResponse>('/admin/ping');
}

/**
 * GET /admin/stats — estatísticas do dashboard, via gateway (policy AdminOnly).
 * Mesma forma de resposta de api.getAdminStats() (`{ stats }`), mas com identidade
 * WORKFORCE em vez do JWT v1.
 */
export function getAdminStats(): Promise<AdminApiResult<{ stats: AdminStats }>> {
  return adminGet<{ stats: AdminStats }>('/admin/stats');
}

/**
 * GET /admin/sales — lista paginada de vendas, via gateway (policy AdminOnly). Os
 * query params seguem o backend v1 (page, pageSize, status, search, start_date,
 * end_date). Valores undefined/null/'' são omitidos.
 */
export function getSales(params?: {
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
  start_date?: string;
  end_date?: string;
}): Promise<AdminApiResult<{ sales: Sale[]; pagination?: Pagination }>> {
  const cleanParams: Record<string, string> = {};
  if (params) {
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') cleanParams[k] = String(v);
    });
  }
  const q = Object.keys(cleanParams).length > 0
    ? '?' + new URLSearchParams(cleanParams).toString()
    : '';
  return adminGet<{ sales: Sale[]; pagination?: Pagination }>(`/admin/sales${q}`);
}

/**
 * GET /admin/sales/:id — detalhe de uma venda, via gateway (policy AdminOnly).
 */
export function getSale(id: number): Promise<AdminApiResult<{ sale: Sale }>> {
  return adminGet<{ sale: Sale }>(`/admin/sales/${id}`);
}
