using PaymentsService.Application.Interfaces;
using PaymentsService.Domain.Entities;
using PaymentsService.Domain.Enums;
using PaymentsService.Domain.Exceptions;
using PaymentsService.Domain.ValueObjects;

namespace PaymentsService.Application.Commands.CreatePayment;

public class CreatePaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IPaymentSlipGenerator _paymentSlipGenerator;
    
    // U produkciji, ovo ide u appsettings/User Secrets, ne hardkodovano -
    // ostavljeno ovde eksplicitno da se lako vidi sta treba popuniti.
    private const string ReceiverAccount = "TVOJ-RACUN-PAUSALNE-FIRME";
    private const string ReceiverName = "Espanol con Vagabundos";

    public CreatePaymentHandler(
        IPaymentRepository repository,
        IQrCodeGenerator qrCodeGenerator,
        IPaymentSlipGenerator paymentSlipGenerator
    )
    {
        _repository = repository;
        _qrCodeGenerator = qrCodeGenerator;
        _paymentSlipGenerator = paymentSlipGenerator;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand command)
    {
        var existing = await _repository.GetActivePendingAsync(command.UserId, command.CourseId, command.ProductType);
        if (existing != null)
            throw new PaymentsDomainException(
                $"Active payment already in pending for this course (reference: {existing.ReferenceNumber})");

        var amount = new Money(command.Amount, command.Currency);
        var payment = Payment.Create(command.UserId, command.CourseId, command.ProductType, amount);
        
        await _repository.AddAsync(payment);
        await _repository.SaveChangesAsync();

        var purpose = command.ProductType == ProductType.Ebook
            ? "Uplata e-knjige - Español con Vagabundos"
            : "Uplata kursa - Español con Vagabundos";


        var qrImage = _qrCodeGenerator.GenerateIpsQrCode(
            receiverAccount: ReceiverAccount,
            receiverName: ReceiverName,
            amount: command.Amount,
            referenceNumber: payment.ReferenceNumber,
            purpose: purpose);
        
        var paymentSlipPdf = _paymentSlipGenerator.GeneratePaymentSlipPdf(
            receiverAccount: ReceiverAccount,
            receiverName: ReceiverName,
            payerName: command.PayerName,
            amount: command.Amount,
            currency: command.Currency,
            referenceNumber: payment.ReferenceNumber,
            purpose: purpose,
            qrCodeImage: qrImage);
        
        return new CreatePaymentResult(payment.Id, payment.ReferenceNumber, qrImage, paymentSlipPdf);
    }
}