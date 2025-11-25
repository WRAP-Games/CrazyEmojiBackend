using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Services;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

public static class WordBootstrap
{
    public async static Task<IServiceCollection> AddWordService(this IServiceCollection services)
    {
        var wordService = new WordService();
        using var stream = File.OpenRead("Content/words.txt");
        await wordService.LoadWordsAsync(stream);

        return services.AddSingleton<IWordService>(wordService);
    }
}
