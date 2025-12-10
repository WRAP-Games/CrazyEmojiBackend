using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Services;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

public static class WordBootstrap
{
    public async static Task<IServiceCollection> AddWordService(this IServiceCollection services, IConfiguration configuration)
    {
        var wordService = new WordService();

        var wordsFilePath = configuration.GetValue<string>("WordsFilePath") ?? "Content/words.txt";

        // Only load words if the file exists
        if (File.Exists(wordsFilePath))
        {
            using var stream = File.OpenRead(wordsFilePath);
            await wordService.LoadWordsAsync(stream);
        }
        else
        {
            // For tests or when file is missing, load a minimal set of default words
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream);
            await writer.WriteLineAsync("test");
            await writer.WriteLineAsync("word");
            await writer.WriteLineAsync("sample");
            await writer.FlushAsync();
            memoryStream.Position = 0;
            await wordService.LoadWordsAsync(memoryStream);
        }

        return services.AddSingleton<IWordService>(wordService);
    }
}
