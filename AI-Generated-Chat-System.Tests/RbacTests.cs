using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AI_Generated_Chat_System.API.Controllers;
using Xunit;

namespace AI_Generated_Chat_System.Tests
{
    public class RbacTests
    {
        [Fact]
        public void AdminOnlyEndpoint_HasCorrectAuthorizeAttribute()
        {
            var methodInfo = typeof(AuthController).GetMethod(nameof(AuthController.AdminOnly));
            Assert.NotNull(methodInfo);

            var authorizeAttribute = methodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true).FirstOrDefault();
            
            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Super Admin,Admin", authorizeAttribute.Roles);
        }

        [Fact]
        public void FinanceOnlyEndpoint_HasCorrectAuthorizeAttribute()
        {
            var methodInfo = typeof(AuthController).GetMethod(nameof(AuthController.FinanceOnly));
            Assert.NotNull(methodInfo);

            var authorizeAttribute = methodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true).FirstOrDefault();
            
            Assert.NotNull(authorizeAttribute);
            Assert.Equal("FinanceOnly", authorizeAttribute.Policy);
        }
    }
}
