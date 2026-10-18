import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { humanize } from '../../../lib/format';
import type { TicketDetail, TicketStatus } from '../../../types/api';

const statuses: TicketStatus[] = ['New', 'Open', 'InProgress', 'Resolved', 'Closed'];

interface StatusActionsProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

/** Moves a ticket to another status. */
export function StatusActions({ ticket, onUpdated }: StatusActionsProps) {
  const [pendingStatus, setPendingStatus] = useState<TicketStatus | undefined>();
  const [error, setError] = useState<string>();

  const isClosed = ticket.status === 'Closed';

  const changeStatus = async (status: TicketStatus) => {
    // ...
    setPendingStatus(status);
    setError(undefined);

    try {
      onUpdated(await ticketsApi.updateStatus(ticket.id, status));
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not change the status.'));
    } finally {
      setPendingStatus(undefined);
    }
  };

  return (
    <div className="card">
      <h2>Status</h2>

      <div className="button-row">
        {isClosed ? (
          <p className="field__hint">Closed tickets cannot transition to any other status.</p>
        ) : (
          statuses
            .filter((status) => status !== ticket.status)
            .map((status) => (
              <button
                key={status}
                type="button"
                className="button button--ghost"
                disabled={pendingStatus !== undefined}
                onClick={() => void changeStatus(status)}
              >
                {pendingStatus === status ? 'Saving...' : `Move to ${humanize(status)}`}
              </button>
            ))
        )}
      </div>

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
