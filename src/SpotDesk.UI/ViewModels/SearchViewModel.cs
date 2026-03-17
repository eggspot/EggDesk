using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotDesk.Core.Models;

namespace SpotDesk.UI.ViewModels;

public record SearchResultItem(
    Guid   ConnectionId,
    string Name,
    string Host,
    string Protocol,
    string GroupName,
    string StatusColor
);

public partial class SearchViewModel : ObservableObject
{
    private readonly IReadOnlyList<ConnectionEntry> _allEntries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    [NotifyPropertyChangedFor(nameof(ShowQuickConnectHint))]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private ObservableCollection<SearchResultItem> _results = [];

    [ObservableProperty] private int _selectedIndex = -1;

    public bool HasResults      => Results.Count > 0;
    public bool ShowQuickConnectHint => Results.Count == 0 && LooksLikeIpOrHost(Query);

    public event Action<ConnectionEntry>? ConnectionActivated;
    public event Action?                  CloseRequested;

    public SearchViewModel(IReadOnlyList<ConnectionEntry> allEntries)
    {
        _allEntries = allEntries;
    }

    partial void OnQueryChanged(string value)
    {
        SelectedIndex = -1;
        if (string.IsNullOrWhiteSpace(value))
        {
            Results.Clear();
            return;
        }

        var q = value.Trim().ToLowerInvariant();
        var matches = _allEntries
            .Where(e => FuzzyMatch(e, q))
            .OrderByDescending(e => e.LastConnectedAt)
            .Take(15)
            .Select(e => new SearchResultItem(
                e.Id,
                e.Name,
                e.Host,
                e.Protocol.ToString(),
                GroupName: string.Empty,
                StatusColor: "#6B7280"))
            .ToList();

        Results = new ObservableCollection<SearchResultItem>(matches);
        if (Results.Count > 0) SelectedIndex = 0;
    }

    [RelayCommand]
    private void SelectNext()
    {
        if (Results.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Results.Count;
    }

    [RelayCommand]
    private void SelectPrevious()
    {
        if (Results.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Results.Count - 1 : SelectedIndex - 1;
    }

    [RelayCommand]
    private void ActivateSelected()
    {
        if (ShowQuickConnectHint)
        {
            QuickConnect(Query);
            return;
        }

        if (SelectedIndex < 0 || SelectedIndex >= Results.Count) return;
        var item  = Results[SelectedIndex];
        var entry = _allEntries.FirstOrDefault(e => e.Id == item.ConnectionId);
        if (entry is not null) ConnectionActivated?.Invoke(entry);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private void QuickConnect(string query)
    {
        var protocol  = query.EndsWith(":22")   ? Protocol.Ssh
                      : query.EndsWith(":5900") ? Protocol.Vnc
                      : Protocol.Rdp;

        var lastColon = query.LastIndexOf(':');
        string host;
        int port;
        if (lastColon > 0 && int.TryParse(query[(lastColon + 1)..], out var p))
        {
            host = query[..lastColon];
            port = p;
        }
        else
        {
            host = query;
            port = ConnectionEntry.DefaultPortFor(protocol);
        }

        var adhoc = new ConnectionEntry
        {
            Name     = host,
            Host     = host,
            Port     = port,
            Protocol = protocol
        };
        ConnectionActivated?.Invoke(adhoc);
    }

    private static bool FuzzyMatch(ConnectionEntry e, string q) =>
        e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
        || e.Host.Contains(q, StringComparison.OrdinalIgnoreCase)
        || e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeIpOrHost(string q) =>
        !string.IsNullOrWhiteSpace(q) &&
        (System.Net.IPAddress.TryParse(q.Split(':')[0], out _)
         || (q.Length > 2 && !q.StartsWith(' ')));
}
