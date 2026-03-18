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

    /// <summary>
    /// Adds a single new entry to the named group, creating the group if it doesn't exist.
    /// Called immediately after the New Connection dialog saves.
    /// </summary>
    public void AddEntry(ConnectionEntry entry, string groupName)
    {
        var name = string.IsNullOrWhiteSpace(groupName) ? "Default" : groupName.Trim();

        var groupVm = Groups.FirstOrDefault(g =>
            string.Equals(g.Group.Name, name, StringComparison.OrdinalIgnoreCase));

        if (groupVm is null)
        {
            var group = new ConnectionGroup { Name = name };
            groupVm = new ConnectionGroupViewModel(group, []);
            Groups.Add(groupVm);
        }

        entry.GroupId = groupVm.Group.Id;
        groupVm.Entries.Add(entry);

        // Rebuild search index to include the new entry
        var all = Groups.SelectMany(g => g.Entries).ToList();
        _index = all.ToFrozenDictionary(e => e.Id);
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

    /// <summary>
    /// Raised when the user presses Enter in the Quick Connect bar.
    /// MainWindowViewModel handles this by calling OpenTab.
    /// </summary>
    public event Action<ConnectionEntry>? QuickConnectRequested;

    [RelayCommand]
    private void QuickConnect(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        // Auto-detect protocol by port pattern
        var protocol = query.EndsWith(":22") ? Protocol.Ssh
            : query.EndsWith(":5900") ? Protocol.Vnc
            : Protocol.Rdp;

        var (host, port) = ParseHostPort(query, ConnectionEntry.DefaultPortFor(protocol));

        var entry = new ConnectionEntry
        {
            Name     = host,
            Host     = host,
            Port     = port,
            Protocol = protocol,
        };

        QuickConnectRequested?.Invoke(entry);
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
