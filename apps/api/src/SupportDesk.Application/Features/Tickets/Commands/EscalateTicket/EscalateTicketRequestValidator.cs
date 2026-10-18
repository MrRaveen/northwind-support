using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(5)
            .MaximumLength(500);

        RuleFor(x => x.EscalatedBy)
            .NotEmpty()
            .MaximumLength(100);
    }
}