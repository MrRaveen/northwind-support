import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketListItem } from '../../../test/fixtures';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
    ticketsApi: {
        escalate: vi.fn(),
    },
}));

const mockTicketDetail: TicketDetail = {
    ...ticketListItem(),
    description: 'Full description of the problem.',
    customer: { id: 2, name: 'Fabrikam Inc', email: 'support@example.com', phone: null, tier: 'Standard' },
    category: { id: 2, name: 'Billing', requiresSpecialist: true },
};

describe('EscalationPanel', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('blocks submission and prevents API calls when reason is shorter than 5 characters', async () => {
        const user = userEvent.setup();
        render(<EscalationPanel ticket={mockTicketDetail} onUpdated={vi.fn()} />);

        const textarea = screen.getByLabelText(/reason for escalation/i);
        const button = screen.getByRole('button', { name: /escalate ticket/i });

        await user.type(textarea, 'bad');
        await user.click(button);

        expect(screen.getByText(/reason must be between 5 and 500 characters/i)).toBeInTheDocument();
        expect(ticketsApi.escalate).not.toHaveBeenCalled();
    });

    it('displays a 409 conflict message when server rejects escalation', async () => {
        const user = userEvent.setup();
        vi.mocked(ticketsApi.escalate).mockRejectedValueOnce(
            new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
        );

        render(<EscalationPanel ticket={mockTicketDetail} onUpdated={vi.fn()} />);

        const textarea = screen.getByLabelText(/reason for escalation/i);
        const button = screen.getByRole('button', { name: /escalate ticket/i });

        await user.type(textarea, 'Valid escalation justification string');
        await user.click(button);

        expect(await screen.findByText(/conflict: ticket tck-0012 is already critical/i)).toBeInTheDocument();
    });

    it('disables controls when the ticket is already Resolved or Critical', () => {
        const resolvedTicket: TicketDetail = { ...mockTicketDetail, status: 'Resolved' };

        render(<EscalationPanel ticket={resolvedTicket} onUpdated={vi.fn()} />);

        expect(screen.getByRole('button', { name: /escalate ticket/i })).toBeDisabled();
        expect(screen.getByLabelText(/reason for escalation/i)).toBeDisabled();
        expect(screen.getByText(/cannot be escalated because it is already resolved/i)).toBeInTheDocument();
    });
});