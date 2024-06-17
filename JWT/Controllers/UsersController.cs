using System.IdentityModel.Tokens.Jwt;
using System.Text;
using JWT.Context;
using JWT.Helpers;
using JWT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace JWT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(IConfiguration _configuration, DatabaseContext _context) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public IActionResult RegisterUser(RegisterModel registerModel)
    {
        var hashedPassword = SecurityHelpers.GetHashedPasswordAndSalt(registerModel.Password);
        var user = new AppUser()
        {
            Login = registerModel.Login,
            Email = registerModel.Email,
            Password = hashedPassword.Item1,
            Salt = hashedPassword.Item2,
            RefreshToken = SecurityHelpers.GenerateRefreshToken(),
            RefreshTokenExp = DateTime.Now.AddDays(1)
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        return Ok( $"User with login: {registerModel.Login} was added");
    }
    
    [AllowAnonymous]
    [HttpPost("refresh")]
    public IActionResult Refresh(RefreshTokenModel refreshToken)
    {
        AppUser user = _context.Users.Where(u => u.RefreshToken == refreshToken.RefreshToken).FirstOrDefault();
        if (user == null)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        if (user.RefreshTokenExp < DateTime.Now)
        {
            throw new SecurityTokenException("Refresh token expired");
        }
        

        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));

        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken jwtToken = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            expires: DateTime.Now.AddMinutes(10),
            signingCredentials: creds
        );

        user.RefreshToken = SecurityHelpers.GenerateRefreshToken();
        user.RefreshTokenExp = DateTime.Now.AddDays(1);
        _context.SaveChanges();

        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken),
            refreshToken = user.RefreshToken
        });
    }
    
    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login(LoginModel loginModel)
    {
        AppUser user = _context.Users.Where(u => u.Login == loginModel.Login).FirstOrDefault();
        
        if (user == null)
        {
            return Unauthorized("Wrong username or password");
        }
        
        string passwordHashFromDb = user.Password;
        string curHashedPassword = SecurityHelpers.GetHashedPasswordWithSalt(loginModel.Password, user.Salt);

        if (passwordHashFromDb != curHashedPassword)
        {
            return Unauthorized("Wrong username or password");
        }

        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));

        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            expires: DateTime.Now.AddMinutes(10),
            signingCredentials: creds
        );

        user.RefreshToken = SecurityHelpers.GenerateRefreshToken();
        user.RefreshTokenExp = DateTime.Now.AddDays(1);
        _context.SaveChanges();

        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            refreshToken = user.RefreshToken
        });
    }
    
    [HttpGet("get")]
    [Authorize]
    public IActionResult Get()
    {
        return Ok("Hello world");
    }
}