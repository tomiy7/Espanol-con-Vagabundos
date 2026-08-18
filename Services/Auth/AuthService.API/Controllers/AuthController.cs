using System.Security.Claims;
using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Repositories;
using AuthService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _config;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;

    public AuthController(
        ILogger<AuthController> logger,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        TokenService tokenService,
        IConfiguration config)
    {
        _logger = logger;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _config = config;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync(
        RegisterRequestDto registerRequestDto)
    {
        if (await _userRepository.EmailExistsAsync(registerRequestDto.Email))
        {
            _logger.LogWarning(
                "Registration failed: email {Email} alredy exists",
                registerRequestDto.Email);

            return Conflict(new ErrorResponseDto
            {
                Error = AuthErrorCodes.EmailExists,
                Message = "Email already exists"
            });
        }

        if (await _userRepository.UsernameExistsAsync(
            registerRequestDto.Username))
        {
            _logger.LogWarning(
                "Registration failed: username {Username} alredy exists",
                registerRequestDto.Username);

            return Conflict(new ErrorResponseDto
            {
                Error = AuthErrorCodes.UsernameExists,
                Message = "Username already exists"
            });
        }

        var user = new User
        {
            Username = registerRequestDto.Username,
            Email = registerRequestDto.Email,
            Phone = registerRequestDto.Phone,
            FirstName = registerRequestDto.FirstName,
            LastName = registerRequestDto.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                registerRequestDto.Password),
            Role = Roles.Student
        };

        await _userRepository.AddUserAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation(
            "User {Username} with email {Email} successfully registered: Id - {UserId}",
            user.Username,
            user.Email,
            user.Id);

        var tokens = await IssueTokens(user);

        return StatusCode(StatusCodes.Status201Created, tokens);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync(
        LoginRequestDto loginRequestDto)
    {
        var user = await _userRepository.GetByIdentifierAsync(
            loginRequestDto.Identifier);

        if (user == null ||
            !BCrypt.Net.BCrypt.Verify(
                loginRequestDto.Password,
                user.PasswordHash))
        {
            _logger.LogWarning(
                "Failed login attempt for identifier: {Identifier}",
                loginRequestDto.Identifier);

            return Unauthorized(new ErrorResponseDto
            {
                Error = AuthErrorCodes.InvalidCredentials,
                Message = "Wrong username or password"
            });
        }

        _logger.LogInformation(
            "User {Username} successfully logged in",
            user.Username);

        var tokens = await IssueTokens(user);

        return Ok(tokens);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync(
        RefreshRequestDto refreshRequestDto)
    {
        var storedToken =
            await _refreshTokenRepository.GetByTokenAsync(
                refreshRequestDto.RefreshToken);

        if (storedToken == null || !storedToken.IsActive)
        {
            _logger.LogWarning(
                "Invalid or expired refresh token attempted");

            return Unauthorized(new ErrorResponseDto
            {
                Error = AuthErrorCodes.InvalidRefreshToken,
                Message = "Refresh token is invalid or expired"
            });
        }

        storedToken.RevokedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Token refreshed for user {UserId}",
            storedToken.UserId);

        var tokens = await IssueTokens(storedToken.User);

        await _refreshTokenRepository.SaveChangesAsync();

        return Ok(tokens);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(
        RefreshRequestDto refreshRequestDto)
    {
        var storedToken =
            await _refreshTokenRepository.GetByTokenAsync(
                refreshRequestDto.RefreshToken);

        if (storedToken != null)
        {
            storedToken.RevokedAt = DateTime.UtcNow;

            await _refreshTokenRepository.SaveChangesAsync();

            _logger.LogInformation(
                "User logged out: {UserId}",
                storedToken.UserId);
        }
        else
        {
            _logger.LogWarning(
                "Logout attempted with unknown or already-revoked refresh token");
        }

        return Ok(new
        {
            message = "Successfuly logged out"
        });
    }

    [HttpGet("admin/users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(List<UserDetailResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepository.GetAllUsersAsync();

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
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = AuthErrorCodes.UserNotFound,
                Message = "User not found"
            });
        }

        return Ok(new UserDetailResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user =
            await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = AuthErrorCodes.UserNotFound,
                Message = "User not found"
            });
        }

        return Ok(new UserDetailResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        });
    }

    private async Task<AuthResponseDto> IssueTokens(User user)
    {
        var accessToken =
            _tokenService.GenerateAccessToken(user);

        var refreshTokenValue =
            _tokenService.GenerateRefreshToken();

        var refreshTokenDays =
            _config.GetValue<int>("Jwt:RefreshTokenDays", 60);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays)
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue
        };
    }
}