using PaymentsService.Application.Interfaces;

namespace PaymentsService.Application.Queries.GetPendingPayments;

public record GetPendingPaymentsQuery;

public record PendingPaymentDto(
    Guid Id,
    Guid UserId,
    Guid CourseId,
    decimal Amount,
    string Currency,
    string ReferenceNumber,
    DateTime CreatedAt);


public class GetPendingPaymentsHandler
{
    private readonly IPaymentRepository _repository;
    
    public GetPendingPaymentsHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PendingPaymentDto>> Handle(GetPendingPaymentsQuery query)
    {
        var payments = await _repository.GetPendingAsync();
        
        return payments.Select(p => new PendingPaymentDto(
            p.Id,
            p.UserId,
            p.CourseId,
            p.Amount.Amount,
            p.Amount.Currency,
            p.ReferenceNumber,
            p.CreatedAt
        )).ToList();
    }
}