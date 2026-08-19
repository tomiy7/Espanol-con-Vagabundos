using PaymentsService.Application.Interfaces;
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
        
        // TODO: kad Groups servis bude povezan, ovde ide poziv ka
        // POST /groups/{id}/join (ili slicno) da se korisnik automatski
        // ubaci u odgovarajucu grupu nakon potvrde uplate.
    }
}