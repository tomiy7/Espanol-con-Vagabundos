namespace PaymentsService.Application.Commands.CreatePayment;

public record CreatePaymentCommand(
    Guid UserId,
    string PayerName,
    Guid CouseId,
    decimal Amount,
    string Currency);
    
public record CreatePaymentResult(
    Guid PaymentId,
    string ReferenceNumber,
    byte[] QrCodeImage,
    byte[] PaymentSlipPdf);