using PaymentsService.Application.Interfaces;

namespace PaymentsService.Application.Queries.GetPaymentsByCourse;


public record GetPaymentsByCourseQuery(Guid CourseId);

public record PaymentByCourseDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    string Currency,
    string ReferenceNumber,
    string Status,
    DateTime CreatedAt,
    DateTime? ConfirmedAt);

public class GetPaymentsByCourseHandler
{
    private readonly IPaymentRepository _repository;

    public GetPaymentsByCourseHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PaymentByCourseDto>> Handle(GetPaymentsByCourseQuery query)
    {
        var payments = await _repository.GetByCourseIdAsync(query.CourseId);

        return payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentByCourseDto(
                p.Id,
                p.UserId,
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