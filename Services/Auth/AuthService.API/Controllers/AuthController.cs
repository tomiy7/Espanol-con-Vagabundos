using AuthService.API.Data;
using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly TokenService _tokenService;

    public AuthController(ILogger<AuthController> logger, AppDbContext dbContext, TokenService tokenService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync(RegisterRequestDto registerRequestDto)
    {
        if (await _dbContext.Users.AnyAsync(u => u.Email == registerRequestDto.Email))
            return Conflict(new { error = "EMAIL_EXISTS" });

        if (await _dbContext.Users.AnyAsync(u => u.Username == registerRequestDto.Username))
            return Conflict(new { error = "USERNAME_EXISTS" });

        var user = new User()
        {
            Username = registerRequestDto.Username,
            Email = registerRequestDto.Email,
            Phone = registerRequestDto.Phone,
            FirstName = registerRequestDto.FirstName,
            LastName = registerRequestDto.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequestDto.Password)
        };

        await _dbContext.AddAsync(user);
        await _dbContext.SaveChangesAsync();
        return Ok(new { token = _tokenService.GenerateToken(user) });
    }
}