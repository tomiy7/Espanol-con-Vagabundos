using PaymentsService.Application.Interfaces;

namespace PaymentsService.Application.Queries.GetPaymentById;

public record GetPaymentByIdQuery(Guid PaymentId);

public record PaymentDetailDto(
    Guid Id,
    Guid UserId,
    Guid? CourseId,
    string ProductType,
    decimal Amount,
    string Currency,
    string ReferenceNumber,
    string Status,
    DateTime CreatedAt,
    DateTime? ConfirmedAt);

public class GetPaymentByIdHandler
{
    private readonly IPaymentRepository _repository;
    
    public GetPaymentByIdHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDetailDto?> Handle(GetPaymentByIdQuery query)
    {
        var payment = await _repository.GetByIdAsync(query.PaymentId);
        if (payment == null) return null;
        
        return new PaymentDetailDto(
            payment.Id,
            payment.UserId,
            payment.CourseId,
            payment.ProductType.ToString(),
            payment.Amount.Amount,
            payment.Amount.Currency,
            payment.ReferenceNumber,
            payment.Status.ToString(),
            payment.CreatedAt,
            payment.ConfirmedAt
        );
    }
}