using PaymentsService.Application.Interfaces;
using PaymentsService.Domain.Enums;
using PaymentsService.Domain.Exceptions;

namespace PaymentsService.Application.Commands.ConfirmPayment;

public class ConfirmPaymentHandler
{
    private readonly IPaymentRepository _repository;
    
    public ConfirmPaymentHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ConfirmPaymentCommand command)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId)
            ?? throw new PaymentsDomainException($"Payment with id {command.PaymentId} does not exist");
        
        payment.Confirm(command.ConfirmedByUserId);
        await _repository.SaveChangesAsync();
        
        if (payment.ProductType == ProductType.Course)
        {
            // TODO: kad Groups servis bude povezan, ovde ide poziv ka
            // POST /groups/{id}/join (ili slicno) da se korisnik automatski
            // ubaci u odgovarajucu grupu nakon potvrde uplate kursa.
        }
        else if (payment.ProductType == ProductType.Ebook)
        {
            // TODO: kad ovo bude povezano sa Courses servisom, ovde se
            // omogucava pristup linku za PDF e-knjige (npr. cita
            // se EbookPdf polje iz Course entiteta preko HTTP poziva).
            // Za sad, NE pravi se Enrollment/upis u grupu - dobijanje
            // ebooka moze biti ostvareno i bez uzimanja kursa
        }
    }
}