using PaymentsService.Domain.Exceptions;

namespace PaymentsService.Domain.ValueObjects;

public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new PaymentsDomainException("Money amount cannot be negative.");
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new PaymentsDomainException("Currency cannot be empty.");

        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
    
    public override string ToString() => $"{Amount} {Currency}";

    public bool IsZero() => Amount == 0;
}