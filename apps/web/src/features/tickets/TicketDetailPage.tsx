import { Link, useParams } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import { AssignAgentControl } from './components/AssignAgentControl';
import { StatusActions } from './components/StatusActions';
import { TicketSummary } from './components/TicketSummary';
import { useTicket } from './hooks/useTicket';
import { EscalationPanel } from './components/EscalationPanel';
import { EscalationHistory } from './components/EscalationHistory';
import type { TicketDetail } from '../../types/api';

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const ticketId = Number(id);

  const { data: ticket, isLoading, error, reload, setData } = useTicket(ticketId);
  const reference = useReferenceData();

  if (Number.isNaN(ticketId)) {
    return <ErrorMessage message="That is not a valid ticket reference." />;
  }

  // Intercept the API update to locally adjust agent ticket counts
  const handleTicketUpdated = (updatedTicket: TicketDetail) => {
    setData(updatedTicket);

    if (reference.data && ticket) {
      const wasOpen = ticket.status !== 'Resolved' && ticket.status !== 'Closed';
      const isOpen = updatedTicket.status !== 'Resolved' && updatedTicket.status !== 'Closed';

      const oldAgentId = ticket.assignedAgent?.id;
      const newAgentId = updatedTicket.assignedAgent?.id;

      const updatedAgents = reference.data.agents.map((agent) => {
        let countChange = 0;

        // The old agent loses the ticket (if it was previously open)
        if (wasOpen && oldAgentId === agent.id) countChange -= 1;

        // The new agent gains the ticket (if it is currently open)
        if (isOpen && newAgentId === agent.id) countChange += 1;

        if (countChange === 0) return agent;
        return { ...agent, openTicketCount: agent.openTicketCount + countChange };
      });

      reference.setData({ ...reference.data, agents: updatedAgents });
    }
  };

  if (isLoading && !ticket) {
    return <Spinner label="Loading ticket" />;
  }

  if (error && !ticket) {
    return <ErrorMessage message={error} onRetry={() => void reload()} />;
  }

  if (!ticket) {
    return null;
  }

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / {ticket.reference}
          </p>
          <h1>{ticket.title}</h1>
        </div>
      </header>

      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      <div className="detail-layout">
        <div className="detail-layout__main">
          <div className="card">
            <h2>Description</h2>
            <p className="description">{ticket.description}</p>
          </div>

          {/* Wire the new handler to the components that mutate state */}
          <StatusActions ticket={ticket} onUpdated={handleTicketUpdated} />

          <EscalationPanel ticket={ticket} onUpdated={handleTicketUpdated} />

          <EscalationHistory ticketId={ticket.id} lastUpdatedUtc={ticket.updatedAtUtc} />
        </div>

        <aside className="detail-layout__side">
          <TicketSummary ticket={ticket} />

          {/* Wire the new handler to the components that mutate state */}
          <AssignAgentControl
            ticket={ticket}
            agents={reference.data?.agents ?? []}
            onUpdated={handleTicketUpdated}
          />
        </aside>
      </div>
    </section>
  );
}