namespace PaymentsService.API.DTOs;

public class CreatePaymentRequest
{
    public Guid CourseId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RSD";
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}