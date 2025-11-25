using System.Collections;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.Services;

public record WordEntry(string Text, int Length)
{
    public WordEntry(string text) : this(text, text.Length) { }
}

public class WordService : IWordService, IEnumerable<WordEntry>
{
    private readonly List<WordEntry> _words = [];
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
            {
                var trimmed = line.Trim();
                var entry = new WordEntry(trimmed);

                if (!_words.Contains(entry))
                    _words.Add(entry);
            }
        }
    }

    public Task<string> GetRandomWordAsync()
    {
        if (_words.Count == 0)
            throw new InvalidOperationException("Word list not loaded. Call LoadWordsAsync() first.");

        var word = _words[_random.Next(_words.Count)];
        return Task.FromResult(word.Text);
    }

    public IEnumerator<WordEntry> GetEnumerator() => _words.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}