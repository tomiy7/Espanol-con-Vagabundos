using System.ComponentModel.DataAnnotations;

namespace AuthService.API.DTOs;

public class RegisterRequestDto
{
    [Required, MinLength(3), MaxLength(50)]
    [RegularExpression("^[a-zA-Z0-9_]+$")]
    public string Username { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email is in invalid format")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Phone is required")]
    [RegularExpression(@"^(\+381|0)6[0-9]{7,8}$", ErrorMessage = "Phone number must be in format 06XXXXXXXX or +3816XXXXXXXX")]
    public string Phone { get; set; } = string.Empty;
    
    [Required]
    [MinLength(8, ErrorMessage = "Password must have at least 8 characters")]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
    
    [Required]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    public string LastName { get; set; } = string.Empty;
}