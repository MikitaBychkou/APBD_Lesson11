using System.ComponentModel.DataAnnotations;

namespace JWT.Models;

public class LoginModel
{
    [MaxLength(50)]
    public string Login { get; set; }
    
    [MaxLength(50)]
    public string Password { get; set; }
}