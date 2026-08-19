using PaymentsService.Application.Interfaces;
using PaymentsService.Application.Queries.GetPaymentById;

namespace PaymentsService.Application.Queries.GetPaymentsByUser;

public record GetPaymentsByUserQuery(Guid UserId);

public class GetPaymentsByUserHandler
{
    private readonly IPaymentRepository _repository;

    public GetPaymentsByUserHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PaymentDetailDto>> Handle(GetPaymentsByUserQuery query)
    {
        var payments = await _repository.GetByUserIdAsync(query.UserId);

        return payments.Select(p => new PaymentDetailDto(
            p.Id, p.UserId, p.CourseId, p.Amount.Amount, p.Amount.Currency,
            p.ReferenceNumber, p.Status.ToString(), p.CreatedAt, p.ConfirmedAt
        )).ToList();
    }
}