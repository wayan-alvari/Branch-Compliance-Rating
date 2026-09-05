using System.ComponentModel.DataAnnotations;

namespace BranchCompliance.Web.Models;

public sealed class LoginModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), StringLength(128)]
    public string Password { get; set; } = "";

    [StringLength(1024)]
    public string? ReturnUrl { get; set; }
}
