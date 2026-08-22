using PaymentsService.Application.Interfaces;
using QuestPDF.Fluent;

namespace PaymentsService.Infrastructure.Documents;

public class PaymentSlipGenerator : IPaymentSlipGenerator
{
    public byte[] GeneratePaymentSlipPdf(
        string receiverAccount, 
        string receiverName, 
        string payerName, 
        decimal amount,
        string currency, 
        string referenceNumber, 
        string purpose, 
        byte[] qrCodeImage)
    {
        var document = new PaymentSlipPdfDocument(
            receiverAccount,
            receiverName,
            payerName,
            amount,
            currency,
            referenceNumber,
            purpose,
            qrCodeImage);

        return document.GeneratePdf();
        // vidi da li ce se cuvati negde kao dokaz ako bude placeno
    }
}