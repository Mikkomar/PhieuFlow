using PhieuFlow.FormBuilder.Models;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// State machine behind the per-form Responses page, lifted out of the component the same
/// way <see cref="FormsListSession"/> was: no Blazor types, unit-testable on its own.
/// Consumes the submission batch stream (keyset-paged server-side), accumulates the rows,
/// sorts them newest-first for display, derives the question columns as the union of every
/// answered question across the loaded submissions, and pages the result client-side.
/// </summary>
public sealed class FormResponsesSession(
    IFormsService formsService,
    ILogger<FormResponsesSession>? logger = null) : IDisposable
{
    public const int PageSize = 25;

    private readonly List<FormResponse> _responses = [];
    private CancellationTokenSource? _loadCts;
    private int _page = 1;

    /// <summary>Raised after any state change so the owning component can re-render.</summary>
    public event Action? Changed;

    public bool Loading { get; private set; } = true;
    public bool LoadingMore { get; private set; }
    public string? LoadError { get; private set; }

    public int TotalCount => _responses.Count;

    /// <summary>Every question answered by at least one loaded submission, in display order.</summary>
    public IReadOnlyList<ResponseColumn> Columns { get; private set; } = [];

    public IReadOnlyList<FormResponse> PagedResponses { get; private set; } = [];
    public int TotalPages { get; private set; } = 1;
    public int ClampedPage { get; private set; } = 1;
    public int FirstRowNumber { get; private set; }
    public int LastRowNumber { get; private set; }

    public async Task LoadAsync(Guid formId)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        _responses.Clear();
        LoadError = null;
        Loading = true;
        LoadingMore = true;
        Recompute();
        Changed?.Invoke();

        try
        {
            await foreach (var batch in formsService.GetSubmissionsStreamingAsync(formId, cts.Token))
            {
                _responses.AddRange(batch);
                Loading = false;
                Recompute();
                Changed?.Invoke();
            }
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError(ex, "Loading submissions for form {FormId} from the Hub failed.", formId);
            LoadError = "Couldn't load responses from the server.";
        }
        catch (OperationCanceledException)
        {
            // navigated away mid-stream — nothing to report
        }
        finally
        {
            Loading = false;
            LoadingMore = false;
            Recompute();
            Changed?.Invoke();
        }
    }

    public void SetPage(int page)
    {
        _page = page;
        Recompute();
        Changed?.Invoke();
    }

    private void Recompute()
    {
        var ordered = _responses
            .OrderByDescending(r => r.SubmittedAt)
            .ThenByDescending(r => r.Id)
            .ToList();

        Columns = ordered
            .SelectMany(r => r.Answers)
            .GroupBy(a => a.QuestionId)
            .Select(g => new ResponseColumn(g.Key, g.First().QuestionText, g.Min(a => a.Order)))
            .OrderBy(c => c.Order)
            .ThenBy(c => c.QuestionText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TotalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize));
        ClampedPage = Math.Clamp(_page, 1, TotalPages);
        PagedResponses = ordered.Skip((ClampedPage - 1) * PageSize).Take(PageSize).ToList();
        FirstRowNumber = ordered.Count == 0 ? 0 : (ClampedPage - 1) * PageSize + 1;
        LastRowNumber = Math.Min(ClampedPage * PageSize, ordered.Count);
    }

    public void Dispose() => _loadCts?.Cancel();
}

/// <summary>A derived question column on the Responses table.</summary>
public readonly record struct ResponseColumn(Guid QuestionId, string QuestionText, int Order);
