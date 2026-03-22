using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Data;

namespace Wrap.CrazyEmoji.Api.Services;

public class FriendshipSchemaInitializer(IDbContextFactory<GameDbContext> dbFactory, ILogger<FriendshipSchemaInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        const string sql = """
            CREATE TABLE IF NOT EXISTS \"Friendship\" (
                \"Id\" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                \"UserAUsername\" varchar(32) NOT NULL,
                \"UserBUsername\" varchar(32) NOT NULL,
                \"CreatedAtUtc\" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT \"CK_Friendship_NoSelfReference\" CHECK (\"UserAUsername\" <> \"UserBUsername\"),
                CONSTRAINT \"FK_Friendship_UserA\" FOREIGN KEY (\"UserAUsername\") REFERENCES \"User\" (\"Username\") ON DELETE CASCADE,
                CONSTRAINT \"FK_Friendship_UserB\" FOREIGN KEY (\"UserBUsername\") REFERENCES \"User\" (\"Username\") ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Friendship_UserA_UserB\"
                ON \"Friendship\" (\"UserAUsername\", \"UserBUsername\");
        """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Friendship table could not be initialized automatically.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
