namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

internal static class TestUsers
{
    public static readonly UserCredentials User1 = new(Email: "tester1@local.testers.com", Password: "Password1!");
    public static readonly UserCredentials User2 = new(Email: "tester2@local.testers.com", Password: "Password2@");
    public static readonly UserCredentials User3 = new(Email: "tester3@local.testers.com", Password: "Password3#");
}