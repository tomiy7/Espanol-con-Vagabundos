namespace PaymentsService.Application.Commands.ConfirmPayment;

public record ConfirmPaymentCommand(Guid PaymentId, Guid ConfirmedByUserId);