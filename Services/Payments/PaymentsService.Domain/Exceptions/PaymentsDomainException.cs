namespace PaymentsService.Domain.Exceptions;

public class PaymentsDomainException : Exception
{
    public PaymentsDomainException() { }
    
    public PaymentsDomainException(string message) : base(message) { }
    
    public PaymentsDomainException(string message, Exception innerException) : base(message, innerException) { }
}