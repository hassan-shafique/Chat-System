using Microsoft.AspNetCore.Identity;

namespace AI_Generated_Chat_System.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
    }
}
