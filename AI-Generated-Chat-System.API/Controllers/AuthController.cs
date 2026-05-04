using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using AI_Generated_Chat_System.Domain.Entities;

namespace AI_Generated_Chat_System.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            var user = new ApplicationUser { UserName = model.Username, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
                return Ok(new { Message = "User registered successfully" });

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                if (await _userManager.GetTwoFactorEnabledAsync(user))
                {
                    if (string.IsNullOrEmpty(model.TwoFactorCode))
                        return Ok(new { RequiresTwoFactor = true });

                    var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, model.TwoFactorCode);
                    if (!isValid)
                        return Unauthorized("Invalid 2FA code.");
                }

                var token = await GenerateJwtToken(user);
                user.RefreshToken = GenerateRefreshToken();
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                await _userManager.UpdateAsync(user);

                return Ok(new
                {
                    Token = token,
                    RefreshToken = user.RefreshToken
                });
            }
            return Unauthorized();
        }

        [HttpPost("{username}/2fa/enable")]
        public async Task<IActionResult> Enable2FA(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return NotFound();

            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            var qrCodeUri = $"otpauth://totp/AI-Generated-Chat-System:{user.Email}?secret={key}&issuer=AI-Generated-Chat-System";
            return Ok(new { SharedKey = key, QrCodeUri = qrCodeUri });
        }

        [HttpPost("{username}/2fa/verify-setup")]
        public async Task<IActionResult> VerifySetup2FA(string username, [FromBody] Setup2faDto model)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return NotFound();

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, model.Code);
            if (isValid)
            {
                await _userManager.SetTwoFactorEnabledAsync(user, true);
                return Ok(new { Message = "2FA enabled successfully" });
            }

            return BadRequest("Invalid 2FA code.");
        }

        [HttpPost("{username}/2fa/disable")]
        public async Task<IActionResult> Disable2FA(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return NotFound();

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            return Ok(new { Message = "2FA disabled successfully" });
        }

        [HttpPost("{username}/assign-role")]
        public async Task<IActionResult> AssignRole(string username, [FromBody] string role)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return NotFound("User not found");

            if (!await _roleManager.RoleExistsAsync(role))
                return BadRequest("Role does not exist.");

            var result = await _userManager.AddToRoleAsync(user, role);
            if (result.Succeeded)
                return Ok(new { Message = $"Role {role} assigned to {username} successfully" });

            return BadRequest(result.Errors);
        }

        [Authorize(Roles = "Super Admin,Admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminOnly()
        {
            return Ok("You have access to the Admin-Only endpoint.");
        }

        [Authorize(Policy = "FinanceOnly")]
        [HttpGet("finance-only")]
        public IActionResult FinanceOnly()
        {
            return Ok("You have access to the Finance-Only endpoint.");
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto model)
        {
            if (model is null)
                return BadRequest("Invalid client request");

            var principal = GetPrincipalFromExpiredToken(model.AccessToken);
            if (principal == null)
                return BadRequest("Invalid access token or refresh token");

            var username = principal.Identity?.Name;
            var user = await _userManager.FindByNameAsync(username);

            if (user == null || user.RefreshToken != model.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return BadRequest("Invalid access token or refresh token");

            var newAccessToken = await GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "superSecretKey_At_Least_16_Bytes_Long!!^^");
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "http://localhost:5000",
                audience: _configuration["Jwt:Audience"] ?? "http://localhost:5000",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "superSecretKey_At_Least_16_Bytes_Long!!^^")),
                ValidateLifetime = false 
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }

    public class RegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TwoFactorCode { get; set; }
    }

    public class Setup2faDto
    {
        public string Code { get; set; } = string.Empty;
    }

    public class RefreshTokenDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
