using System.Collections.Generic;
using System.Threading.Tasks;
using AI_Generated_Chat_System.API.Controllers;
using AI_Generated_Chat_System.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI_Generated_Chat_System.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var options = new Mock<IOptions<IdentityOptions>>();
            var idOptions = new IdentityOptions();
            options.Setup(o => o.Value).Returns(idOptions);

            _mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, options.Object, null, null, null, null, null, null, null);
            
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(roleStore.Object, null, null, null, null);
            
            _mockConfiguration = new Mock<IConfiguration>();

            var mockJwtSection = new Mock<IConfigurationSection>();
            mockJwtSection.Setup(x => x.Value).Returns("superSecretKey_At_Least_16_Bytes_Long!!^^");
            _mockConfiguration.Setup(x => x.GetSection("Jwt:Key")).Returns(mockJwtSection.Object);

            _controller = new AuthController(_mockUserManager.Object, _mockRoleManager.Object, _mockConfiguration.Object);
        }

        [Fact]
        public async Task Login_Without2FA_Success()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "Password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var result = await _controller.Login(new LoginDto { Username = "testuser", Password = "Password123" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Login_With2FA_RequiresTwoFactor()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "Password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetTwoFactorEnabledAsync(user)).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var result = await _controller.Login(new LoginDto { Username = "testuser", Password = "Password123" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("RequiresTwoFactor", jsonStr);
        }

        [Fact]
        public async Task Login_With2FA_InvalidOTP_ReturnsUnauthorized()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "Password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetTwoFactorEnabledAsync(user)).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, It.IsAny<string>(), "123456")).ReturnsAsync(false);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var result = await _controller.Login(new LoginDto { Username = "testuser", Password = "Password123", TwoFactorCode = "123456" });

            var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Invalid 2FA code.", unauthResult.Value);
        }
    }
}
