using System.Collections;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.Services;

public class WordService : IWordService, IEnumerable<string>
{
    private readonly List<string> _words = [];
    private readonly Random _random = Random.Shared;

    public async Task LoadWordsAsync(Stream wordStream)
    {
        ArgumentNullException.ThrowIfNull(wordStream);

        using var reader = new StreamReader(wordStream);
        string? line;
        _words.Clear();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _words.Add(line.Trim());
        }
    }

    public Task<string> GetRandomWordAsync()
    {
        if (_words.Count == 0)
            throw new InvalidOperationException("Word list not loaded. Call LoadWordsAsync() first.");

        var word = _words[_random.Next(_words.Count)];
        return Task.FromResult(word);
    }

    public IEnumerator<string> GetEnumerator() => _words.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}