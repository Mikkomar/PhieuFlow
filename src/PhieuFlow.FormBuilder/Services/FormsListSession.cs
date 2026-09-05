using PhieuFlow.FormBuilder.Enums;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// The forms list's state machine, lifted out of <c>Home</c> the same way <see cref="FormEditorSession"/>
/// was lifted out of <c>FormBuilder</c>: no Blazor types, unit-testable on its own. Owns the streamed-in
/// forms, the current query/tab/sort/page (pushed in via <see cref="SetView"/> whenever the page's
/// URL-derived state changes), the derived view (filtered/paged/counts), and the row actions
/// (publish/duplicate/delete) that mutate the underlying list and recompute the view.
/// </summary>
public sealed class FormsListSession(IFormsService formsService) : IDisposable
{
    public const int PageSize = 20;

    public static readonly FormListTab[] Tabs =
        [FormListTab.All, FormListTab.Live, FormListTab.NeverPublished, FormListTab.UnpublishedEdits];

    private List<FormSummary> _forms = [];
    private CancellationTokenSource? _loadCts;

    private string _query = string.Empty;
    private FormListTab _tab = FormListTab.All;
    private FormListSortColumn _sortColumn = FormListSortColumn.Modified;
    private bool _sortDescending = true;
    private int _page = 1;

    private readonly int[] _tabCounts = new int[Tabs.Length];

    /// <summary>Raised after any state change so the owning component can re-render.</summary>
    public event Action? Changed;

    public bool Loading { get; private set; } = true;
    public bool LoadingMore { get; private set; }
    public string? LoadError { get; private set; }
    public string? ActionError { get; private set; }

    public IReadOnlyList<FormSummary> FilteredForms { get; private set; } = [];
    public IReadOnlyList<FormSummary> PagedForms { get; private set; } = [];
    public int TotalPages { get; private set; } = 1;
    public int ClampedPage { get; private set; } = 1;
    public int FirstRowNumber { get; private set; }
    public int LastRowNumber { get; private set; }

    public int TabCount(FormListTab tab) => _tabCounts[Array.IndexOf(Tabs, tab)];

    /// <summary>Pushes the page's current query/tab/sort/page in and recomputes the derived view.</summary>
    public void SetView(string query, FormListTab tab, FormListSortColumn sortColumn, bool sortDescending, int page)
    {
        _query = query;
        _tab = tab;
        _sortColumn = sortColumn;
        _sortDescending = sortDescending;
        _page = page;
        Recompute();
        Changed?.Invoke();
    }

    public async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        _forms = [];
        LoadError = null;
        LoadingMore = true;
        Recompute();
        Changed?.Invoke();

        try
        {
            await foreach (var batch in formsService.GetAllStreamingAsync(cts.Token))
            {
                _forms.AddRange(batch);
                Recompute();
                Loading = false;
                Changed?.Invoke();
            }
        }
        catch (HttpRequestException)
        {
            LoadError = "Couldn't load forms from the server.";
        }
        catch (OperationCanceledException)
        {
            // navigated away mid-stream — nothing to report
        }
        finally
        {
            Loading = false;
            LoadingMore = false;
            Changed?.Invoke();
        }
    }

    public async Task<PublishResultDto?> PublishAsync(FormSummary form)
    {
        ActionError = null;
        if (form.Status == FormStatus.Published)
        {
            return null;
        }

        PublishResultDto result;
        try
        {
            result = await formsService.PublishAsync(form.Id);
        }
        catch (HttpRequestException)
        {
            ActionError = $"Couldn't publish \"{form.Title}\".";
            Changed?.Invoke();
            return null;
        }

        if (result.Published)
        {
            form.Status = FormStatus.Published;
            form.VersionNumber = result.VersionNumber;
            form.Revision = result.Revision;
            form.LastModifiedAt = result.LastModifiedAt;
            form.LatestPublishedVersionNumber = result.VersionNumber;
            form.LatestPublishedAt = result.PublishedAt;

            if (string.IsNullOrEmpty(form.PublicUrl))
            {
                form.PublicUrl = $"https://forms.phieuflow.app/f/{form.Id.ToString()[..8]}";
            }

            Recompute();
        }

        Changed?.Invoke();
        return result;
    }

    public async Task DuplicateAsync(FormSummary form)
    {
        ActionError = null;
        try
        {
            var newId = await formsService.DuplicateAsync(form.Id);
            var loaded = await formsService.GetByIdAsync(newId);
            if (loaded is not null)
            {
                _forms.Insert(0, new FormSummary
                {
                    Id = newId,
                    Title = loaded.Title,
                    Description = loaded.Description,
                    Status = FormStatus.Draft,
                    CreatedAt = loaded.CreatedAt,
                    LastModifiedAt = loaded.LastModifiedAt,
                    LastModifiedBy = loaded.LastModifiedBy ?? string.Empty,
                    Revision = loaded.Revision,
                    VersionNumber = loaded.VersionNumber,
                    LatestPublishedVersionNumber = null,
                    LatestPublishedAt = null,
                    QuestionCount = loaded.Pages.Sum(p => p.Questions.Count),
                    PageCount = loaded.Pages.Count,
                });
            }

            Recompute();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            ActionError = $"Couldn't duplicate \"{form.Title}\".";
        }

        Changed?.Invoke();
    }

    public async Task DeleteAsync(FormSummary form)
    {
        ActionError = null;
        try
        {
            await formsService.DeleteAsync(form.Id);
        }
        catch (HttpRequestException)
        {
            ActionError = $"Couldn't delete \"{form.Title}\". The form is still here.";
            Changed?.Invoke();
            return;
        }

        _forms.Remove(form);
        Recompute();
        Changed?.Invoke();
    }

    private void Recompute()
    {
        for (var i = 0; i < Tabs.Length; i++)
        {
            _tabCounts[i] = TabFiltered(Tabs[i]).Count();
        }

        var query = _query.Trim();
        var result = TabFiltered(_tab);

        if (!string.IsNullOrEmpty(query))
        {
            result = result.Where(f =>
                f.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (f.Description ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = Sort(result).ToList();
        FilteredForms = filtered;
        TotalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
        ClampedPage = Math.Clamp(_page, 1, TotalPages);
        PagedForms = filtered.Skip((ClampedPage - 1) * PageSize).Take(PageSize).ToList();
        FirstRowNumber = filtered.Count == 0 ? 0 : (ClampedPage - 1) * PageSize + 1;
        LastRowNumber = Math.Min(ClampedPage * PageSize, filtered.Count);
    }

    private IEnumerable<FormSummary> TabFiltered(FormListTab tab) => tab switch
    {
        FormListTab.Live => _forms.Where(f => f.LatestPublishedVersionNumber is not null),
        FormListTab.NeverPublished => _forms.Where(f => f.LatestPublishedVersionNumber is null),
        FormListTab.UnpublishedEdits => _forms.Where(f =>
            f.LatestPublishedVersionNumber is not null && f.Status != FormStatus.Published),
        _ => _forms,
    };

    private IEnumerable<FormSummary> Sort(IEnumerable<FormSummary> forms)
    {
        IOrderedEnumerable<FormSummary> ordered = _sortColumn switch
        {
            FormListSortColumn.Name => _sortDescending
                ? forms.OrderByDescending(f => f.Title, StringComparer.OrdinalIgnoreCase)
                : forms.OrderBy(f => f.Title, StringComparer.OrdinalIgnoreCase),
            FormListSortColumn.Status => _sortDescending
                ? forms.OrderByDescending(StatusRank)
                : forms.OrderBy(StatusRank),
            _ => _sortDescending
                ? forms.OrderByDescending(f => f.LastModifiedAt)
                : forms.OrderBy(f => f.LastModifiedAt),
        };

        return ordered.ThenByDescending(f => f.LastModifiedAt);
    }

    // Never published (0) < unpublished edits over a live version (1) < live latest (2).
    private static int StatusRank(FormSummary f) =>
        f.LatestPublishedVersionNumber is null ? 0
        : f.Status != FormStatus.Published ? 1
        : 2;

    public void Dispose() => _loadCts?.Cancel();
}
