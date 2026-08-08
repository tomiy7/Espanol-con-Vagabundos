using System.Security.Claims;
using AuthService.API.Data;
using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Services;
using Microsoft.AspNetCore.Authorization;
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

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync(LoginRequestDto loginRequestDto)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
            u.Email == loginRequestDto.Identifier || u.Username == loginRequestDto.Identifier);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequestDto.Password, user.PasswordHash))
            return Unauthorized(new { error = "INVALID_CREDENTIALS" });
        
        return Ok(new { token = _tokenService.GenerateToken(user) });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var username = User.FindFirstValue("username");
        return Ok(new { userId, email, username });
    }
}