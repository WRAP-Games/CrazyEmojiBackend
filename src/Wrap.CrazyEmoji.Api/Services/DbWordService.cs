using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Data;

namespace Wrap.CrazyEmoji.Api.Services;

public class DbWordService(GameDbContext db) : IWordService
{
    public async Task<string> GetRandomWordAsync(long categoryId)
    {
        return await db.Words
            .Where(w => w.CategoryId == categoryId)
            .OrderBy(_ => EF.Functions.Random())
            .Select(w => w.Text)
            .FirstOrDefaultAsync() ?? throw new InvalidOperationException($"No words found for category {categoryId}");
    }
}