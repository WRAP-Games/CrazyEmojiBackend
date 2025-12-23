using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Data;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class DatabaseBootstrap
{
    internal static IServiceCollection RegisterDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddDbContext<GameDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
    }
}