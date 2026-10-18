import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <p>
            Raised <strong>{created.reference}</strong> for {created.customer.name}.
          </p>

          {created.triage && (
            <div style={{ marginTop: '16px', marginBottom: '24px', padding: '16px', background: '#f9f9f9', borderRadius: '6px' }}>
              <h3 style={{ marginTop: 0 }}>Triage Decision</h3>
              <ul style={{ margin: 0, paddingLeft: '20px' }}>
                <li><strong>Priority:</strong> {created.triage.appliedPriority} <em>({created.triage.priorityReason})</em></li>
                <li><strong>SLA Window:</strong> {created.triage.slaWindowHours} hours <em>({created.triage.slaReason})</em></li>
                <li><strong>Assigned Agent:</strong> {created.assignedAgent?.fullName ?? 'Unassigned'} <em>({created.triage.assignmentReason})</em></li>
              </ul>
            </div>
          )}

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
