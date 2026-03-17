using System.Collections.Frozen;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotDesk.Core.Models;

namespace SpotDesk.UI.ViewModels;

public partial class ConnectionTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ConnectionGroupViewModel> _groups = [];

    [ObservableProperty]
    private ObservableCollection<ConnectionEntry> _recents = [];

    [ObservableProperty]
    private ObservableCollection<ConnectionEntry> _filteredConnections = [];

    private FrozenDictionary<Guid, ConnectionEntry> _index = FrozenDictionary<Guid, ConnectionEntry>.Empty;

    partial void OnSearchQueryChanged(string value) => ApplyFilter(value);

    public void LoadConnections(IEnumerable<ConnectionGroup> groups, IEnumerable<ConnectionEntry> entries)
    {
        var entryList = entries.ToList();
        _index = entryList.ToFrozenDictionary(e => e.Id);

        Groups.Clear();
        foreach (var g in groups.OrderBy(g => g.SortOrder))
        {
            var groupEntries = entryList.Where(e => e.GroupId == g.Id).OrderBy(e => e.Name);
            Groups.Add(new ConnectionGroupViewModel(g, groupEntries));
        }

        // Favorites as first virtual group
        var favorites = entryList.Where(e => e.IsFavorite).OrderBy(e => e.Name).ToList();
        if (favorites.Count > 0)
            Groups.Insert(0, new ConnectionGroupViewModel(
                new ConnectionGroup { Name = "Favorites" }, favorites));
    }

    private void ApplyFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            FilteredConnections.Clear();
            return;
        }

        var q = query.ToLowerInvariant();
        var matches = _index.Values
            .Where(e => FuzzyMatch(e, q))
            .OrderByDescending(e => e.LastConnectedAt)
            .Take(20);

        FilteredConnections = [.. matches];
    }

    private static bool FuzzyMatch(ConnectionEntry entry, string query) =>
        entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Host.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Raised when the sidebar header "Reconnect All" button is pressed.
    /// Handled by MainWindowViewModel.ReconnectAllCommand.
    /// </summary>
    public event Action? ReconnectAllRequested;

    [RelayCommand]
    private void ReconnectAll() => ReconnectAllRequested?.Invoke();

    [RelayCommand]
    private void QuickConnect(string query)
    {
        // Auto-detect protocol by port pattern
        var protocol = query.EndsWith(":22") ? Protocol.Ssh
            : query.EndsWith(":5900") ? Protocol.Vnc
            : Protocol.Rdp;

        var (host, port) = ParseHostPort(query, ConnectionEntry.DefaultPortFor(protocol));
        // TODO: open a tab for this ad-hoc connection
    }

    private static (string Host, int Port) ParseHostPort(string input, int defaultPort)
    {
        var lastColon = input.LastIndexOf(':');
        if (lastColon > 0 && int.TryParse(input[(lastColon + 1)..], out var port))
            return (input[..lastColon], port);
        return (input, defaultPort);
    }
}

public partial class ConnectionGroupViewModel : ObservableObject
{
    public ConnectionGroup Group { get; }

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<ConnectionEntry> Entries { get; }

    public ConnectionGroupViewModel(ConnectionGroup group, IEnumerable<ConnectionEntry> entries)
    {
        Group = group;
        _isExpanded = group.IsExpanded;
        Entries = new ObservableCollection<ConnectionEntry>(entries);
    }

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}
