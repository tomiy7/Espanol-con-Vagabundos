using PaymentsService.Domain.Enums;
using PaymentsService.Domain.Exceptions;
using PaymentsService.Domain.ValueObjects;

namespace PaymentsService.Domain.Entities;

public class Payment : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid CourseId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public string ReferenceNumber { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    
    private Payment() { }

    public static Payment Create(Guid userId, Guid courseId, Money amount)
    {
        if (userId == Guid.Empty)
            throw new PaymentsDomainException("User ID cannot be empty.");

        if (courseId == Guid.Empty)
            throw new PaymentsDomainException("Course ID cannot be empty.");

        if (amount.IsZero())
            throw new PaymentsDomainException("Payment amount cannot be zero.");

        return new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = courseId,
            Amount = amount,
            ReferenceNumber = GenerateReferenceNumber(),
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string GenerateReferenceNumber()
    {
        var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var suffix = Random.Shared.Next(100, 999);
        return $"{timeStamp}{suffix}";
    }

    public void Confirm(Guid confirmedByUserId)
    {
        if (confirmedByUserId == Guid.Empty)
            throw new PaymentsDomainException("Confirmed by user ID cannot be empty.");

        if (Status != PaymentStatus.Pending)
            throw new PaymentsDomainException("Only payments in status Pending can be approved.");

        Status = PaymentStatus.Completed;
        ConfirmedAt = DateTime.UtcNow;
        ConfirmedByUserId = confirmedByUserId;
    }
    
    public void Cancel()
    {
        if (Status != PaymentStatus.Pending)
            throw new PaymentsDomainException("Only payments in status Pending cam be cancelled.");

        Status = PaymentStatus.Cancelled;
    }
    
    public bool IsCompleted => Status == PaymentStatus.Completed;
    
    public static readonly TimeSpan ExpirationWindow = TimeSpan.FromHours(24);

    public bool HasExpired(DateTime nowUtc) =>
        Status == PaymentStatus.Pending && CreatedAt + ExpirationWindow < nowUtc;
}