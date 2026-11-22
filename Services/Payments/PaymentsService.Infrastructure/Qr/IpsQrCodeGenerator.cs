using System.Text;
using PaymentsService.Application.Interfaces;
using QRCoder;

namespace PaymentsService.Infrastructure.Qr;

public class IpsQrCodeGenerator : IQrCodeGenerator
{
    public byte[] GenerateIpsQrCode(
        string receiverAccount, 
        string receiverName, 
        decimal amount, 
        string referenceNumber,
        string purpose)
    {
        var ipsString = BuildIpsString(receiverAccount, receiverName, amount, referenceNumber, purpose);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ipsString, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        
        return qrCode.GetGraphic(20);
    }

    private static string BuildIpsString(
        string receiverAccount,
        string receiverName,
        decimal amount,
        string referenceNumber,
        string purpose)
    {
        var sb = new StringBuilder();
        sb.Append("K:PR|V:01|C:1|"); // kod formata:patment request|varzija formata|charset
        sb.Append($"R:{receiverAccount}|"); // racun primaoca
        sb.Append($"N:{receiverName}|"); // naziv primaoca
        sb.Append($"I:RSD{amount:F2}|"); // iznos sa RSD
        sb.Append("SF:289|"); // sifra placanja
        sb.Append($"S:{purpose}|"); // svrha placanja 
        sb.Append($"RO:{referenceNumber}"); // poziv na broj
        
        return sb.ToString();
    }
}