import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { TicketDetail } from '../../../types/api';

interface EscalationPanelProps {
    ticket: TicketDetail;
    onUpdated: (ticket: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onUpdated }: EscalationPanelProps) {
    const [reason, setReason] = useState('');
    const [escalatedBy, setEscalatedBy] = useState(''); // Will be replaced by Auth token in Task 3
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string>();

    const cannotBeEscalated =
        ticket.priority === 'Critical' ||
        ticket.status === 'Resolved' ||
        ticket.status === 'Closed';

    const submit = async (event: React.FormEvent) => {
        event.preventDefault();

        if (reason.trim().length < 5 || reason.trim().length > 500) {
            setError('Reason must be between 5 and 500 characters.');
            return;
        }
        if (!escalatedBy.trim()) {
            setError('You must provide your name/handle.');
            return;
        }

        setIsSaving(true);
        setError(undefined);

        try {
            const updatedTicket = await ticketsApi.escalate(ticket.id, reason, escalatedBy);
            setReason('');
            setEscalatedBy('');
            onUpdated(updatedTicket);
        } catch (caught) {
            if (caught instanceof ApiError && caught.status === 409) {
                setError(`Conflict: ${caught.message}`);
            } else {
                setError(toErrorMessage(caught, 'Could not escalate the ticket.'));
            }
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <form className="card form" onSubmit={submit} noValidate>
            <h2>Escalate Ticket</h2>

            {cannotBeEscalated && (
                <p className="field__hint" style={{ marginBottom: '12px' }}>
                    This ticket cannot be escalated because it is already {ticket.priority === 'Critical' ? 'Critical' : ticket.status}.
                </p>
            )}

            <Field id="escalate-reason" label="Reason for escalation" error={error}>
                {(fieldProps) => (
                    <textarea
                        {...fieldProps}
                        rows={3}
                        value={reason}
                        disabled={cannotBeEscalated || isSaving}
                        placeholder="Why is this SLA at risk? (5-500 chars)"
                        onChange={(e) => setReason(e.target.value)}
                    />
                )}
            </Field>

            <Field id="escalate-by" label="Escalated by (Temp for Task 2)">
                {(fieldProps) => (
                    <input
                        {...fieldProps}
                        type="text"
                        value={escalatedBy}
                        disabled={cannotBeEscalated || isSaving}
                        placeholder="e.g. alex.lead"
                        onChange={(e) => setEscalatedBy(e.target.value)}
                    />
                )}
            </Field>

            <div className="button-row">
                <button
                    type="submit"
                    className="button button--danger"
                    disabled={cannotBeEscalated || isSaving}
                >
                    {isSaving ? 'Escalating...' : 'Escalate Ticket'}
                </button>
            </div>
        </form>
    );
}