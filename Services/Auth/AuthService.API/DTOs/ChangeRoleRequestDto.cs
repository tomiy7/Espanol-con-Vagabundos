using System.ComponentModel.DataAnnotations;

namespace AuthService.API.DTOs;

public class ChangeRoleRequestDto
{
    // "student", "professor" ili "admin"
    [Required]
    public string Role { get; set; } = string.Empty;
}
