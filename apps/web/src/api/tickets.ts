import type {
  CreateTicketPayload,
  PagedResult,
  TicketDetail,
  TicketEscalation,
  TicketFilters,
  TicketListItem,
  TicketStatus,
} from '../types/api';
import { request } from './client';

type QueryValue = string | number | boolean | undefined;

function buildQuery(filters: TicketFilters): string {
  const params = new URLSearchParams();

  const append = (key: string, value: QueryValue) => {
    if (value === undefined || value === '') {
      return;
    }

    params.set(key, String(value));
  };

  append('page', filters.page);
  append('pageSize', filters.pageSize);
  append('search', filters.search.trim());
  append('status', filters.status);
  append('priority', filters.priority);
  append('categoryId', filters.categoryId);
  append('customerId', filters.customerId);
  append('assignedAgentId', filters.assignedAgentId);
  append('unassignedOnly', filters.unassignedOnly);
  append('sortBy', filters.sortBy);
  append('sortDirection', filters.sortDirection);
  append('slaStatus', filters.slaStatus);

  return params.toString();
}

export const ticketsApi = {
  getTickets: (filters: TicketFilters) =>
    request<PagedResult<TicketListItem>>(`/tickets?${buildQuery(filters)}`),

  getTicket: (id: number) => request<TicketDetail>(`/tickets/${id}`),

  createTicket: (payload: CreateTicketPayload) =>
    request<TicketDetail>('/tickets', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateStatus: (id: number, status: TicketStatus) =>
    request<TicketDetail>(`/tickets/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),

  assignAgent: (id: number, agentId: number | null) =>
    request<TicketDetail>(`/tickets/${id}/assignment`, {
      method: 'PATCH',
      body: JSON.stringify({ agentId }),
    }),
  escalate: (id: number, reason: string) =>
    request<TicketDetail>(`/tickets/${id}/escalate`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  getEscalations: (id: number) =>
    request<TicketEscalation[]>(`/tickets/${id}/escalations`),
};
