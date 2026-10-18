import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { useAsyncData } from '../../../hooks/useAsyncData';
import { formatDateTime } from '../../../lib/format';

interface EscalationHistoryProps {
    ticketId: number;
    lastUpdatedUtc: string; // Used to trigger refetch when ticket changes
}

export function EscalationHistory({ ticketId, lastUpdatedUtc }: EscalationHistoryProps) {
    const load = useCallback(() => ticketsApi.getEscalations(ticketId), [ticketId, lastUpdatedUtc]);
    const { data: escalations, isLoading, error, reload } = useAsyncData(load, 'Unable to load history.');

    if (isLoading && !escalations) return <Spinner label="Loading history" />;
    if (error) return <ErrorMessage message={error} onRetry={() => void reload()} />;
    if (!escalations || escalations.length === 0) return <EmptyState title="No escalations yet" />;

    return (
        <div className="card">
            <h2>Escalation History</h2>
            <div className="table-wrapper">
                <table className="table">
                    <thead>
                        <tr>
                            <th>Date</th>
                            <th>Priority Change</th>
                            <th>Agent Change</th>
                            <th>Reason</th>
                            <th>By</th>
                        </tr>
                    </thead>
                    <tbody>
                        {escalations.map((e) => (
                            <tr key={e.id}>
                                <td>{formatDateTime(e.escalatedAtUtc)}</td>
                                <td>{e.fromPriority} → <strong>{e.toPriority}</strong></td>
                                <td>
                                    {e.fromAgent?.id === e.toAgent?.id ? (
                                        <>
                                            {e.toAgent?.fullName ?? 'Unassigned'} <em>(no change)</em>
                                        </>
                                    ) : (
                                        <>
                                            {e.fromAgent?.fullName ?? 'Unassigned'} →{' '}
                                            <strong>{e.toAgent?.fullName ?? 'Unassigned'}</strong>
                                        </>
                                    )}
                                </td>
                                <td style={{ maxWidth: '300px' }}>{e.reason}</td>
                                <td>{e.escalatedBy}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}