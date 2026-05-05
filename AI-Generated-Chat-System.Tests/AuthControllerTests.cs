using System.Collections.Generic;
using System.Threading.Tasks;
using AI_Generated_Chat_System.API.Controllers;
using AI_Generated_Chat_System.Domain.Entities;
using AI_Generated_Chat_System.API.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
        private readonly Mock<IHubContext<ChatHub>> _mockHubContext;
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
            
            _mockHubContext = new Mock<IHubContext<ChatHub>>();
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();
            mockClients.Setup(x => x.All).Returns(mockClientProxy.Object);
            _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

            _mockConfiguration = new Mock<IConfiguration>();

            var mockJwtSection = new Mock<IConfigurationSection>();
            mockJwtSection.Setup(x => x.Value).Returns("superSecretKey_At_Least_16_Bytes_Long!!^^");
            _mockConfiguration.Setup(x => x.GetSection("Jwt:Key")).Returns(mockJwtSection.Object);

            _controller = new AuthController(_mockUserManager.Object, _mockRoleManager.Object, _mockHubContext.Object, _mockConfiguration.Object);
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
        [Fact]
        public async Task AssignRole_UserNotFound_ReturnsNotFound()
        {
            _mockUserManager.Setup(x => x.FindByNameAsync("nonexistent")).ReturnsAsync((ApplicationUser)null!);

            var result = await _controller.AssignRole("nonexistent", "Admin");

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("User not found", notFoundResult.Value);
        }

        [Fact]
        public async Task AssignRole_RoleNotFound_ReturnsBadRequest()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockRoleManager.Setup(x => x.RoleExistsAsync("NonExistentRole")).ReturnsAsync(false);

            var result = await _controller.AssignRole("testuser", "NonExistentRole");

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Role does not exist.", badRequestResult.Value);
        }

        [Fact]
        public async Task AssignRole_Success_ReturnsOk()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockRoleManager.Setup(x => x.RoleExistsAsync("Admin")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.AssignRole("testuser", "Admin");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("successfully", jsonStr);
        }

        [Fact]
        public async Task Register_Success_ReturnsOk()
        {
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123"))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.Register(new RegisterDto { Username = "newuser", Email = "new@test.com", Password = "Password123" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("successfully", jsonStr);
        }

        [Fact]
        public async Task Register_Failure_ReturnsBadRequest()
        {
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error" }));

            var result = await _controller.Register(new RegisterDto { Username = "newuser", Email = "new@test.com", Password = "Password123" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_InvalidUsername_ReturnsUnauthorized()
        {
            _mockUserManager.Setup(x => x.FindByNameAsync("nonexistent")).ReturnsAsync((ApplicationUser)null!);

            var result = await _controller.Login(new LoginDto { Username = "nonexistent", Password = "Password123" });

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Login_InvalidPassword_ReturnsUnauthorized()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "WrongPass")).ReturnsAsync(false);

            var result = await _controller.Login(new LoginDto { Username = "testuser", Password = "WrongPass" });

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Enable2FA_UserNotFound_ReturnsNotFound()
        {
            _mockUserManager.Setup(x => x.FindByNameAsync("nonexistent")).ReturnsAsync((ApplicationUser)null!);

            var result = await _controller.Enable2FA("nonexistent");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Enable2FA_Success_ReturnsOkAndQrCodeUri()
        {
            var user = new ApplicationUser { UserName = "testuser", Email = "test@test.com" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.GetAuthenticatorKeyAsync(user)).ReturnsAsync("SECRETKEY");

            var result = await _controller.Enable2FA("testuser");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("SECRETKEY", jsonStr);
        }

        [Fact]
        public async Task VerifySetup2FA_UserNotFound_ReturnsNotFound()
        {
            _mockUserManager.Setup(x => x.FindByNameAsync("nonexistent")).ReturnsAsync((ApplicationUser)null!);

            var result = await _controller.VerifySetup2FA("nonexistent", new Setup2faDto { Code = "123456" });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task VerifySetup2FA_InvalidCode_ReturnsBadRequest()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, It.IsAny<string>(), "wrong")).ReturnsAsync(false);

            var result = await _controller.VerifySetup2FA("testuser", new Setup2faDto { Code = "wrong" });

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid 2FA code.", badRequestResult.Value);
        }

        [Fact]
        public async Task VerifySetup2FA_Success_ReturnsOk()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, It.IsAny<string>(), "correct")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.SetTwoFactorEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.VerifySetup2FA("testuser", new Setup2faDto { Code = "correct" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("successfully", jsonStr);
        }

        [Fact]
        public async Task Disable2FA_UserNotFound_ReturnsNotFound()
        {
            _mockUserManager.Setup(x => x.FindByNameAsync("nonexistent")).ReturnsAsync((ApplicationUser)null!);

            var result = await _controller.Disable2FA("nonexistent");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Disable2FA_Success_ReturnsOk()
        {
            var user = new ApplicationUser { UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.SetTwoFactorEnabledAsync(user, false)).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.Disable2FA("testuser");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("disabled successfully", jsonStr);
        }

        [Fact]
        public async Task RefreshToken_InvalidRequest_ReturnsBadRequest()
        {
            var result = await _controller.RefreshToken(null!);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid client request", badRequestResult.Value);
        }
    }
}
