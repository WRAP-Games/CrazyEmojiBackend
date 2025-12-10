using System.Net;
using System.Net.Http.Json;
using Wrap.CrazyEmoji.IntegrationTests.Infrastructure;

namespace Wrap.CrazyEmoji.IntegrationTests.Controllers;

public class RegisterControllerTests(ApiFixture apiFixture)
{
    [Fact]
    public async Task Register_NewUser_ShouldReturn200OK()
    {
        // Arrange
        var client = await apiFixture.GetUnauthorizedUser1ClientAsync();
        var registerRequest = new
        {
            email = "test@example.com",
            password = "Password123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("api/auth/register", registerRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}