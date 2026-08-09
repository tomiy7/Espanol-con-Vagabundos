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
    private readonly IConfiguration _config;
    private readonly AppDbContext _dbContext;
    private readonly TokenService _tokenService;

    public AuthController(ILogger<AuthController> logger, AppDbContext dbContext, TokenService tokenService, IConfiguration config)
    {
        _logger = logger;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _config = config;
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
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequestDto.Password),
            Role = Roles.Student
        };

        await _dbContext.AddAsync(user);
        await _dbContext.SaveChangesAsync();
        
        var tokens= await IssueTokens(user);
        return Ok(tokens);
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
        
        var tokens= await IssueTokens(user);
        return Ok(tokens);
    }
    
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync(RefreshRequestDto refreshRequestDto)
    {
        var storedToken = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshRequestDto.RefreshToken);
        
        if (storedToken == null || !storedToken.IsActive)
            return Unauthorized(new { error = "INVALID_REFRESH_TOKEN" });
        
        storedToken.RevokedAt = DateTime.UtcNow;
        
        var tokens = await IssueTokens(storedToken.User);
        await _dbContext.SaveChangesAsync();
        return Ok(tokens);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(RefreshRequestDto refreshRequestDto)
    {
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshRequestDto.RefreshToken);

        if (storedToken != null)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        
        return Ok(new { message = "Successfuly  logged out" });
    }

    [HttpGet("admin/users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _dbContext.Users.ToListAsync();
        
        var response = users.Select(user => new UserDetailResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        }).ToList();
        
        return Ok(response);
    }

    [HttpGet("admin/users/{userId}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(UserDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        
        if (user == null)
            return NotFound(new
            {
                error = "USER_NOT_FOUND",
                message = "User not found"
            });
        return Ok(new UserDetailResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
        });
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

    private async Task<AuthResponseDto> IssueTokens(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var refreshTokenDays = _config.GetValue<int>("Jwt:RefreshTokenDays", 60);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays)
        };
        
        await _dbContext.RefreshTokens.AddAsync(refreshToken);
        await _dbContext.SaveChangesAsync();
        
        return new AuthResponseDto { AccessToken = accessToken, RefreshToken = refreshTokenValue };
    }
}