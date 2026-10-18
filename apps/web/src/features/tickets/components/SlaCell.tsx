import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

interface SlaLabel {
  text: string;
  tone: BadgeTone;
  symbol: string;
}

const labels: Record<SlaStatus, SlaLabel> = {
  WithinSla: { text: 'Within SLA', tone: 'info', symbol: '●' },
  AtRisk: { text: 'At risk', tone: 'warning', symbol: '▲' },
  Breached: { text: 'Breached', tone: 'danger', symbol: '✖' },
  Met: { text: 'Met', tone: 'success', symbol: '✔' },
  NotApplicable: { text: 'No SLA', tone: 'neutral', symbol: '–' },
};

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** The SLA indicator used by both the ticket list and the detail page. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  const label = labels[slaStatus];

  return (
    <div className="sla-cell">
      <Badge tone={label.tone}>
        <span aria-hidden="true">{label.symbol} </span>
        {label.text}
      </Badge>
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}