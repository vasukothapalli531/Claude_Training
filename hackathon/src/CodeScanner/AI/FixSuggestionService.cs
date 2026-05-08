using System.Diagnostics;
using System.Text.Json.Nodes;

namespace CodeScanner;

internal sealed class FixSuggestionService
{
    public sealed record Result(
        IReadOnlyList<SmellFinding> Smells,
        IReadOnlyList<SecurityFinding> SecurityFindings,
        IReadOnlyList<ScanError> Errors,
        AiSummary Summary);

    private readonly IClaudeClient _client;
    private readonly string _model;
    private readonly int _concurrency;

    public FixSuggestionService(IClaudeClient client, string model, int concurrency)
    {
        _client = client;
        _model = model;
        _concurrency = concurrency;
    }

    public async Task<Result> GenerateAsync(
        string scanRoot,
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> securityFindings,
        CancellationToken cancellationToken)
    {
        if (_concurrency <= 0)
        {
            var disabledSummary = ComputeSummary(smells, securityFindings, totalElapsedMs: 0L, totalCalls: 0, successful: 0, failed: 0);
            return new Result(smells, securityFindings, Array.Empty<ScanError>(), disabledSummary);
        }

        var totalSw = Stopwatch.StartNew();
        var sem = new SemaphoreSlim(_concurrency);
        var errors = new List<ScanError>();
        var errorLock = new object();

        var smellResults = new SmellFinding[smells.Count];
        var secResults = new SecurityFinding[securityFindings.Count];

        var smellTasks = smells.Select((f, i) => RunSmell(f, i)).ToArray();
        var secTasks = securityFindings.Select((f, i) => RunSecurity(f, i)).ToArray();

        await Task.WhenAll(smellTasks.Concat(secTasks)).ConfigureAwait(false);
        totalSw.Stop();

        var enrichedSmells = smellResults.ToList();
        var enrichedSec = secResults.ToList();

        var totalCalls = smells.Count + securityFindings.Count;
        var successful = enrichedSmells.Count(s => s.AiSuggestion is not null) + enrichedSec.Count(s => s.AiSuggestion is not null);
        var failed = totalCalls - successful;

        var summary = ComputeSummary(enrichedSmells, enrichedSec, totalSw.ElapsedMilliseconds, totalCalls, successful, failed);
        return new Result(enrichedSmells, enrichedSec, errors, summary);

        async Task RunSmell(SmellFinding f, int index)
        {
            await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var enriched = await TryGenerateSmell(f).ConfigureAwait(false);
                smellResults[index] = enriched;
            }
            finally { sem.Release(); }
        }

        async Task RunSecurity(SecurityFinding f, int index)
        {
            await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var enriched = await TryGenerateSec(f).ConfigureAwait(false);
                secResults[index] = enriched;
            }
            finally { sem.Release(); }
        }

        async Task<SmellFinding> TryGenerateSmell(SmellFinding f)
        {
            try
            {
                string source;
                try { source = await File.ReadAllTextAsync(f.File, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    AppendError(f.File, $"AI suggestion failed: source unavailable: {ex.GetType().Name}");
                    return f;
                }

                var userContent = PromptBuilder.BuildUserContent(f, source);
                var sw = Stopwatch.StartNew();
                var responseText = await _client.SendAsync(BuildRequestBody(userContent), cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (!SuggestionParser.TryParse(responseText, out var suggestion, out var error) || suggestion is null)
                {
                    AppendError(f.File, $"AI suggestion failed: {error}");
                    return f;
                }
                return f with { AiSuggestion = suggestion with { Model = _model, ElapsedMs = sw.ElapsedMilliseconds } };
            }
            catch (Exception ex)
            {
                AppendError(f.File, $"AI suggestion failed: {ex.GetType().Name}: {ex.Message}");
                return f;
            }
        }

        async Task<SecurityFinding> TryGenerateSec(SecurityFinding f)
        {
            try
            {
                string source;
                try { source = await File.ReadAllTextAsync(f.File, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    AppendError(f.File, $"AI suggestion failed: source unavailable: {ex.GetType().Name}");
                    return f;
                }

                var userContent = PromptBuilder.BuildUserContent(f, source);
                var sw = Stopwatch.StartNew();
                var responseText = await _client.SendAsync(BuildRequestBody(userContent), cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (!SuggestionParser.TryParse(responseText, out var suggestion, out var error) || suggestion is null)
                {
                    AppendError(f.File, $"AI suggestion failed: {error}");
                    return f;
                }
                return f with { AiSuggestion = suggestion with { Model = _model, ElapsedMs = sw.ElapsedMilliseconds } };
            }
            catch (Exception ex)
            {
                AppendError(f.File, $"AI suggestion failed: {ex.GetType().Name}: {ex.Message}");
                return f;
            }
        }

        void AppendError(string path, string reason)
        {
            lock (errorLock) { errors.Add(new ScanError(path, reason)); }
        }
    }

    private string BuildRequestBody(string userContent)
    {
        var doc = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = 600,
            ["system"] = PromptBuilder.BuildSystem(),
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = userContent,
                },
            },
        };
        return doc.ToJsonString();
    }

    private AiSummary ComputeSummary(
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> security,
        long totalElapsedMs,
        int totalCalls,
        int successful,
        int failed)
    {
        var currentScore = RiskScoring.ComputeQualityScore(smells, security);
        var smellsRemaining = smells.Where(s => s.AiSuggestion is null).ToList();
        var secRemaining = security.Where(s => s.AiSuggestion is null).ToList();
        var optimistic = RiskScoring.ComputeQualityScore(smellsRemaining, secRemaining);
        var delta = optimistic - currentScore;

        return new AiSummary(
            Model: _model,
            TotalCalls: totalCalls,
            Successful: successful,
            Failed: failed,
            TotalElapsedMs: totalElapsedMs,
            QualityScoreIfAllFixed: optimistic,
            QualityScoreDelta: delta);
    }
}
