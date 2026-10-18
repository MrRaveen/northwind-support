import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { Agent, TicketDetail } from '../../../types/api';

interface AssignAgentControlProps {
  ticket: TicketDetail;
  agents: Agent[];
  onUpdated: (ticket: TicketDetail) => void;
}

/** Hands the ticket to an agent, or takes it back. */
export function AssignAgentControl({ ticket, agents, onUpdated }: AssignAgentControlProps) {
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string>();

  const isClosed = ticket.status === 'Closed';

  const assign = async (agentId: number | null) => {
    if (isClosed) return;

    setIsSaving(true);
    setError(undefined);

    try {
      onUpdated(await ticketsApi.assignAgent(ticket.id, agentId));
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not assign the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="card">
      <h2>Assignment</h2>

      {isClosed && (
        <p className="field__hint" style={{ marginBottom: '8px' }}>
          Closed tickets cannot be reassigned.
        </p>
      )}

      <Field id="assign-agent" label="Assigned agent" error={error}>
        {(fieldProps) => (
          <select
            {...fieldProps}
            value={ticket.assignedAgent?.id ?? ''}
            disabled={isSaving || isClosed}
            onChange={(event) =>
              void assign(event.target.value === '' ? null : Number(event.target.value))
            }
          >
            <option value="">Unassigned</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id} disabled={!agent.isActive}>
                {agent.fullName} ({agent.openTicketCount}/{agent.maxOpenTickets} open)
                {agent.isActive ? '' : ' - inactive'}
              </option>
            ))}
          </select>
        )}
      </Field>

      {isSaving && <p className="field__hint">Saving...</p>}
    </div>
  );
}