namespace CodeScanner.Tests.AI;

public class FixSuggestionServiceTests
{
    [Fact]
    public async Task GenerateAsync_NoFindings_EmptyResultAndZeroSummary()
    {
        var svc = new FixSuggestionService(new FakeClient(_ => "ok"), model: "m", concurrency: 4);
        using var tree = new TempTree();

        var result = await svc.GenerateAsync(
            tree.Root,
            Array.Empty<SmellFinding>(),
            Array.Empty<SecurityFinding>(),
            CancellationToken.None);

        Assert.Empty(result.Smells);
        Assert.Empty(result.SecurityFindings);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.Summary.TotalCalls);
        Assert.Equal(0, result.Summary.Failed);
    }

    [Fact]
    public async Task GenerateAsync_AllSuccess_AllFindingsHaveSuggestions()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { void Foo() { } }\n");

        var smells = new[]
        {
            new SmellFinding("long_function", "low", path, "Foo", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "low", path, "Bar", 1, 1, 1, 50, "msg"),
        };

        var svc = new FixSuggestionService(
            new FakeClient(_ => "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}"),
            model: "test-model", concurrency: 4);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.Equal(2, result.Smells.Count);
        Assert.All(result.Smells, s => Assert.NotNull(s.AiSuggestion));
        Assert.All(result.Smells, s => Assert.Equal("test-model", s.AiSuggestion!.Model));
        Assert.Equal(2, result.Summary.TotalCalls);
        Assert.Equal(2, result.Summary.Successful);
        Assert.Equal(0, result.Summary.Failed);
    }

    [Fact]
    public async Task GenerateAsync_OneClientThrows_OthersStillSucceed_AndErrorRecorded()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { void Foo() { } }\n");
        var smells = new[]
        {
            new SmellFinding("long_function", "low", path, "Foo", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "low", path, "Bar", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "low", path, "Baz", 1, 1, 1, 50, "msg"),
        };

        var callCount = 0;
        var svc = new FixSuggestionService(
            new FakeClient(_ =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 2) { throw new HttpRequestException("simulated 500"); }
                return "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}";
            }), model: "m", concurrency: 1);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.Equal(3, result.Summary.TotalCalls);
        Assert.Equal(2, result.Summary.Successful);
        Assert.Equal(1, result.Summary.Failed);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task GenerateAsync_ConcurrencyZero_NoCallsMade()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { }\n");
        var smells = new[] { new SmellFinding("long_function", "low", path, "Foo", 1, 1, 1, 50, "msg") };

        var calls = 0;
        var svc = new FixSuggestionService(
            new FakeClient(_ => { Interlocked.Increment(ref calls); return "x"; }),
            model: "m", concurrency: 0);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Equal(0, result.Summary.TotalCalls);
        Assert.Single(result.Smells);
        Assert.Null(result.Smells[0].AiSuggestion);
    }

    [Fact]
    public async Task GenerateAsync_RespectsConcurrencyLimit()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { }\n");

        var smells = Enumerable.Range(0, 8)
            .Select(i => new SmellFinding("long_function", "low", path, $"F{i}", 1, 1, 1, 50, "msg"))
            .ToArray();

        var inFlight = 0;
        var maxInFlight = 0;
        var maxLock = new object();

        var svc = new FixSuggestionService(
            new FakeClient(async _ =>
            {
                var n = Interlocked.Increment(ref inFlight);
                lock (maxLock) { if (n > maxInFlight) { maxInFlight = n; } }
                await Task.Delay(20).ConfigureAwait(false);
                Interlocked.Decrement(ref inFlight);
                return "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}";
            }),
            model: "m", concurrency: 3);

        await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.True(maxInFlight <= 3, $"max in flight was {maxInFlight}, expected <= 3");
    }

    [Fact]
    public async Task GenerateAsync_QualityDeltaOnlyCountsSuggestionsThatLanded()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { }\n");

        var smells = new[]
        {
            new SmellFinding("long_function", "high", path, "Foo", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "high", path, "Bar", 1, 1, 1, 50, "msg"),
        };

        var i = 0;
        var svc = new FixSuggestionService(
            new FakeClient(_ =>
            {
                var n = Interlocked.Increment(ref i);
                if (n == 1) { throw new HttpRequestException("fail one"); }
                return "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}";
            }),
            model: "m", concurrency: 1);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        // Current score: 100 - 5 - 5 = 90. With one suggestion landing,
        // optimistic = 100 - 5 = 95. Delta = 5.
        Assert.Equal(95, result.Summary.QualityScoreIfAllFixed);
        Assert.Equal(5,  result.Summary.QualityScoreDelta);
    }

    private sealed class FakeClient : IClaudeClient
    {
        private readonly Func<string, ValueTask<string>>? _asyncFn;
        private readonly Func<string, string>? _syncFn;
        public FakeClient(Func<string, string> fn) { _syncFn = fn; }
        public FakeClient(Func<string, ValueTask<string>> fn) { _asyncFn = fn; }
        public async Task<string> SendAsync(string body, CancellationToken ct)
        {
            if (_asyncFn is not null) { return await _asyncFn(body); }
            return _syncFn!(body);
        }
    }
}
