import type {
  Agent,
  Category,
  CustomerListItem,
  TicketFilters as Filters,
  SlaStatus,
  TicketPriority,
  TicketStatus,
} from '../../../types/api';
import { humanize } from '../../../lib/format';

const statuses: TicketStatus[] = ['New', 'Open', 'InProgress', 'Resolved', 'Closed'];
const priorities: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

interface TicketFiltersProps {
  filters: Filters;
  agents: Agent[];
  categories: Category[];
  customers: CustomerListItem[];
  onChange: (patch: Partial<Filters>) => void;
  onReset: () => void;
}

/** The filter bar above the ticket list. Presentational: it only reports changes upwards. */
export function TicketFilters({
  filters,
  agents,
  categories,
  customers,
  onChange,
  onReset,
}: TicketFiltersProps) {
  const toNumber = (value: string) => (value === '' ? undefined : Number(value));

  return (
    <section className="filters" aria-label="Ticket filters">
      <div className="filters__row">
        <div className="field">
          <label className="field__label" htmlFor="filter-search">
            Search
          </label>
          <input
            id="filter-search"
            type="search"
            placeholder="Title, reference or customer"
            value={filters.search}
            onChange={(event) => onChange({ search: event.target.value })}
          />
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-status">
            Status
          </label>
          <select
            id="filter-status"
            value={filters.status ?? ''}
            onChange={(event) =>
              onChange({ status: (event.target.value || undefined) as TicketStatus | undefined })
            }
          >
            <option value="">All statuses</option>
            {statuses.map((status) => (
              <option key={status} value={status}>
                {humanize(status)}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-priority">
            Priority
          </label>
          <select
            id="filter-priority"
            value={filters.priority ?? ''}
            onChange={(event) =>
              onChange({ priority: (event.target.value || undefined) as TicketPriority | undefined })
            }
          >
            <option value="">All priorities</option>
            {priorities.map((priority) => (
              <option key={priority} value={priority}>
                {priority}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="filters__row">
        <div className="field">
          <label className="field__label" htmlFor="filter-sla">
            SLA Status
          </label>
          <select
            id="filter-sla"
            value={filters.slaStatus ?? ''}
            onChange={(event) =>
              onChange({ slaStatus: (event.target.value || undefined) as SlaStatus | undefined })
            }
          >
            <option value="">All SLA states</option>
            <option value="WithinSla">Within SLA</option>
            <option value="AtRisk">At risk</option>
            <option value="Breached">Breached</option>
            <option value="Met">Met</option>
            <option value="NotApplicable">No SLA</option>
          </select>
        </div>
        <div className="field">
          <label className="field__label" htmlFor="filter-category">
            Category
          </label>
          <select
            id="filter-category"
            value={filters.categoryId ?? ''}
            onChange={(event) => onChange({ categoryId: toNumber(event.target.value) })}
          >
            <option value="">All categories</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-customer">
            Customer
          </label>
          <select
            id="filter-customer"
            value={filters.customerId ?? ''}
            onChange={(event) => onChange({ customerId: toNumber(event.target.value) })}
          >
            <option value="">All customers</option>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.name}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-agent">
            Assigned agent
          </label>
          <select
            id="filter-agent"
            value={filters.assignedAgentId ?? ''}
            onChange={(event) => onChange({ assignedAgentId: toNumber(event.target.value) })}
          >
            <option value="">Any agent</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.fullName}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-sort">
            Sort by
          </label>
          <select
            id="filter-sort"
            value={`${filters.sortBy}:${filters.sortDirection}`}
            onChange={(event) => {
              const [sortBy, sortDirection] = event.target.value.split(':');
              onChange({
                sortBy: sortBy as Filters['sortBy'],
                sortDirection: sortDirection as Filters['sortDirection'],
              });
            }}
          >
            <option value="createdAtUtc:desc">Newest first</option>
            <option value="createdAtUtc:asc">Oldest first</option>
            <option value="dueAtUtc:asc">Due soonest</option>
            <option value="priority:desc">Highest priority</option>
            <option value="updatedAtUtc:desc">Recently updated</option>
          </select>
        </div>
      </div>

      <div className="filters__row filters__row--actions">
        <label className="checkbox">
          <input
            type="checkbox"
            checked={filters.unassignedOnly ?? false}
            onChange={(event) => onChange({ unassignedOnly: event.target.checked })}
          />
          Unassigned only
        </label>

        <button type="button" className="button button--ghost" onClick={onReset}>
          Clear filters
        </button>
      </div>
    </section>
  );
}
