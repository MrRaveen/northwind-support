import { useEffect, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);
  const [state, setState] = useState<State>({ isLoading: true });

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [filters.search]);

  useEffect(() => {
    let isCancelled = false;

    const load = async () => {
      setState((current) => ({ ...current, isLoading: true, error: undefined }));

      try {
        const queryFilters: TicketFilters = {
          ...filters,
          search: debouncedSearch,
        };

        const data = await ticketsApi.getTickets(queryFilters);
        if (!isCancelled) {
          setState({ isLoading: false, data, error: undefined });
        }
      } catch (error) {
        if (!isCancelled) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setState((current) => ({ ...current, isLoading: false, error: message }));
        }
      }
    };

    load();

    return () => {
      isCancelled = true;
    };
  }, [
    debouncedSearch,
    filters.status,
    filters.priority,
    filters.categoryId,
    filters.customerId,
    filters.assignedAgentId,
    filters.unassignedOnly,
    filters.page,
    filters.pageSize,
    filters.slaStatus,
    filters.page,
    filters.sortBy,
    filters.sortDirection,
  ]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  const reload = async () => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));
    try {
      const data = await ticketsApi.getTickets({ ...filters, search: debouncedSearch });
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  };

  return { filters, updateFilters, resetFilters, reload, ...state };
}