using PaymentsService.Application.Interfaces;
using PaymentsService.Domain.Exceptions;

namespace PaymentsService.Application.Commands.CancelPayment;

public class CancelPaymentHandler
{
    private readonly IPaymentRepository _repository;
    
    public CancelPaymentHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(CancelPaymentCommand command)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId)
            ?? throw new PaymentsDomainException("Payment not found.");
        
        payment.Cancel();
        await _repository.SaveChangesAsync();
    }
}