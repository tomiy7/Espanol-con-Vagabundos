using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PaymentsService.Infrastructure.Documents;

public class PaymentSlipPdfDocument : IDocument
{
    private readonly string _receiverAccount;
    private readonly string _receiverName;
    private readonly string _payerName;
    private readonly decimal _amount;
    private readonly string _currency;
    private readonly string _referenceNumber;
    private readonly string _purpose;
    private readonly byte[] _qrCodeImage;
    
    public PaymentSlipPdfDocument(
        string receiverAccount,
        string receiverName,
        string payerName,
        decimal amount,
        string currency,
        string referenceNumber,
        string purpose,
        byte[] qrCodeImage)
    {
        _receiverAccount = receiverAccount;
        _receiverName = receiverName;
        _payerName = payerName;
        _amount = amount;
        _currency = currency;
        _referenceNumber = referenceNumber;
        _purpose = purpose;
        _qrCodeImage = qrCodeImage;
    }

    public DocumentMetadata GetMetadata()
    {
        return new DocumentMetadata
        {
            Title = "Uplatnica - Espanol con Vagabundos",
            Author = "Espanol con Vagabundos"
        };
    }
    
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);

            page.Header()
                .Text("ESPANOL CON VAGABUNDOS")
                .FontSize(20)
                .Bold();

            page.Content()
                .PaddingVertical(20)
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item()
                            .Text("UPLATNICA")
                            .FontSize(16)
                            .Bold();

                        column.Item()
                            .PaddingTop(15)
                            .Text($"Primalac: {_receiverName}");

                        column.Item()
                            .Text($"Racun primaoca: {_receiverAccount}");

                        column.Item()
                            .PaddingTop(10)
                            .Text($"Posiljalac: {_payerName}");

                        column.Item()
                            .PaddingTop(10)
                            .Text($"Iznos: {_amount:F2} {_currency}");

                        column.Item()
                            .Text($"Svrha uplate: {_purpose}");

                        column.Item()
                            .PaddingTop(10)
                            .Text($"Poziv na broj: {_referenceNumber}")
                            .Bold();

                        column.Item()
                            .PaddingTop(20)
                            .Text(
                                "Skenirajte QR kod kroz vasu bankarsku aplikaciju " +
                                "da izvrsite uplatu, ili prepisite podatke rucno.")
                            .FontSize(9)
                            .Italic();
                    });

                    row.ConstantItem(160)
                        .Image(_qrCodeImage);
                });

            page.Footer()
                .AlignCenter()
                .Text(text =>
                {
                    text.Span("Espanol con Vagabundos");
                    text.Span(" | ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));
                });
        });
    }
}