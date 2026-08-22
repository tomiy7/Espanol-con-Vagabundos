using PaymentsService.Application.Interfaces;
using PaymentsService.Application.Queries.GetPaymentById;

namespace PaymentsService.Application.Queries.GetPaymentsByUser;

public record GetPaymentsByUserQuery(Guid UserId);

public record PaymentSummaryDto(
    Guid Id,
    Guid? CourseId,
    string ProductType,
    decimal Amount,
    string Currency,
    string ReferenceNumber,
    string Status,
    DateTime CreatedAt,
    DateTime? ConfirmedAt);

public class GetPaymentsByUserHandler
{
    private readonly IPaymentRepository _repository;

    public GetPaymentsByUserHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PaymentSummaryDto>> Handle(GetPaymentsByUserQuery query)
    {
        var payments = await _repository.GetByUserIdAsync(query.UserId);

        return payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentSummaryDto(
                p.Id,
                p.CourseId,
                p.ProductType.ToString(),
                p.Amount.Amount,
                p.Amount.Currency,
                p.ReferenceNumber,
                p.Status.ToString(),
                p.CreatedAt,
                p.ConfirmedAt
            ))
            .ToList();
    }
}