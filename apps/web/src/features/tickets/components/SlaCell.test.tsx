import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaCell } from './SlaCell';

describe('SlaCell', () => {
    const states: Array<{ status: SlaStatus; expectedText: string; expectedSymbol: string }> = [
        { status: 'WithinSla', expectedText: 'Within SLA', expectedSymbol: '●' },
        { status: 'AtRisk', expectedText: 'At risk', expectedSymbol: '▲' },
        { status: 'Breached', expectedText: 'Breached', expectedSymbol: '✖' },
        { status: 'Met', expectedText: 'Met', expectedSymbol: '✔' },
        { status: 'NotApplicable', expectedText: 'No SLA', expectedSymbol: '–' },
    ];

    it.each(states)(
        'renders $status with distinct symbol "$expectedSymbol" and text "$expectedText"',
        ({ status, expectedText, expectedSymbol }) => {
            const { container } = render(
                <SlaCell dueAtUtc="2026-10-19T10:00:00Z" slaStatus={status} />,
            );

            expect(screen.getByText(expectedText)).toBeInTheDocument();
            expect(container).toHaveTextContent(expectedSymbol);
        },
    );
});