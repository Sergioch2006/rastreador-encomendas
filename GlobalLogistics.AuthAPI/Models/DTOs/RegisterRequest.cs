using System.ComponentModel.DataAnnotations;

namespace GlobalLogistics.AuthAPI.Models.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;
    
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
    
    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? FullName { get; set; }
}
