namespace PaymentsService.Application.Interfaces;

public interface IQrCpdeGemerator
{
    byte[] GenerateIpsQrCode(
        string receiverAccount,
        string receiverName,
        decimal amount,
        string referenceNumber,
        string purpose);
}

public interface IPaymentSlipGenerator
{
    byte[] GeneratePaymentSlipPdf(
        string receiverAccount,
        string receiverName,
        string payerName,
        decimal amount,
        string currency,
        string referenceNumber,
        string purpose,
        byte[] qrCodeImage);
}