using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentsService.API.Constants;
using PaymentsService.API.DTOs;
using PaymentsService.Application.Commands.CancelPayment;
using PaymentsService.Application.Commands.ConfirmPayment;
using PaymentsService.Application.Commands.CreatePayment;
using PaymentsService.Application.Queries.GetPaymentById;
using PaymentsService.Application.Queries.GetPaymentsByCourse;
using PaymentsService.Application.Queries.GetPaymentsByUser;
using PaymentsService.Application.Queries.GetPendingPayments;
using PaymentsService.Domain.Exceptions;

namespace PaymentsService.API.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly CreatePaymentHandler _createPayment;
    private readonly ConfirmPaymentHandler _confirmPayment;
    private readonly CancelPaymentHandler _cancelPayment;
    private readonly GetPendingPaymentsHandler _getPending;
    private readonly GetPaymentByIdHandler _getById;
    private readonly GetPaymentsByUserHandler _getByUser;
    private readonly GetPaymentsByCourseHandler _getByCourse;
    private readonly ILogger<PaymentsController> _logger;
    
    public PaymentsController(
        CreatePaymentHandler createPayment,
        ConfirmPaymentHandler confirmPayment,
        CancelPaymentHandler cancelPayment,
        GetPendingPaymentsHandler getPending,
        GetPaymentByIdHandler getById,
        GetPaymentsByUserHandler getByUser,
        GetPaymentsByCourseHandler getByCourse,
        ILogger<PaymentsController> logger)
    {
        _createPayment = createPayment;
        _confirmPayment = confirmPayment;
        _cancelPayment = cancelPayment;
        _getPending = getPending;
        _getById = getById;
        _getByUser = getByUser;
        _getByCourse = getByCourse;
        _logger = logger;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(CreatePaymentRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var result = await _createPayment.Handle(new CreatePaymentCommand(
                userId, request.PayerName, request.CourseId, request.ProductType, request.Amount, request.Currency));
            
            _logger.LogInformation(
                "Payment created: {PaymentId}, reference {ReferenceNumber}, user {UserId}",
                result.PaymentId, result.ReferenceNumber, userId);

            return StatusCode(201, new
            {
                paymentId = result.PaymentId,
                referenceNumber = result.ReferenceNumber,
                qrCodeImageBase64 = Convert.ToBase64String(result.QrCodeImage),
                uplatnicaPdfBase64 = Convert.ToBase64String(result.PaymentSlipPdf)
            });
        }
        catch (PaymentsDomainException e)
        {
            _logger.LogWarning("Payment creation failed.");
            return BadRequest(new ErrorResponse { Error = "INVALID_PAYMENT", Message = e.Message });
        }
    }
    
    // just admin
    [HttpPost("{paymentId}/confirm")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(Guid paymentId)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized();

        try
        {
            await _confirmPayment.Handle(new ConfirmPaymentCommand(paymentId, adminUserId));
            _logger.LogInformation("Payment confirmed: {PaymentId} by admin {AdminId}", paymentId, adminUserId);
            return Ok(new { message = "Payment confirmed" });
        }
        catch (PaymentsDomainException e)
        {
            _logger.LogWarning("Payment confirmation failed.");
            return BadRequest(new ErrorResponse { Error = "CANNOT_CONFIRM", Message = e.Message });
        }
    }

    [HttpPost("{paymentId}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid paymentId)
    {
        try
        {
            await _cancelPayment.Handle(new CancelPaymentCommand(paymentId));
            _logger.LogInformation("Payment cancelled: {PaymentId}", paymentId);
            return Ok(new { message = "Payment cancelled" });
        }
        catch (PaymentsDomainException e)
        {
            _logger.LogWarning("Payment cancelation failed.");
            return BadRequest(new ErrorResponse { Error = "CANNOT_CANCEL", Message = e.Message });
        }
    }
    
    [HttpGet("pending")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _getPending.Handle(new GetPendingPaymentsQuery());
        return Ok(pending);
    }
    
    // My payments
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPayments()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var payments = await _getByUser.Handle(new GetPaymentsByUserQuery(userId));
        return Ok(payments);
    }
    
    [HttpGet("course/{courseId}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCourse(Guid courseId)
    {
        var payments = await _getByCourse.Handle(new GetPaymentsByCourseQuery(courseId));
        return Ok(payments);
    }

    [HttpGet("{paymentId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid paymentId)
    {
        var payment = await _getById.Handle(new GetPaymentByIdQuery(paymentId));
        if (payment == null)
            return NotFound(new ErrorResponse { Error = "PAYMENT_NOT_FOUND", Message = "Payment does not exist" });

        return Ok(payment);
    }
}