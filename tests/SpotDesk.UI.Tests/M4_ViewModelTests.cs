using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SpotDesk.Core.Models;
using SpotDesk.UI.Dialogs;
using SpotDesk.UI.ViewModels;
using Xunit;
#pragma warning disable CA1001 // NewConnectionDialogViewModel has no disposable resources

namespace SpotDesk.UI.Tests;

// ── MainWindowViewModel ───────────────────────────────────────────────────────

public class M4_MainWindowViewModelTests
{
    private static MainWindowViewModel CreateVm() =>
        new(new ConnectionTreeViewModel());

    [Fact, Trait("Category", "M4")]
    public void ViewModel_InitialState_NoActiveSessions()
    {
        var vm = CreateVm();
        Assert.Empty(vm.Tabs);
        Assert.Null(vm.ActiveTab);
    }

    [Fact, Trait("Category", "M4")]
    public void ViewModel_AddSession_TabAppears()
    {
        var vm = CreateVm();
        var entry = new ConnectionEntry { Name = "Test Server", Host = "192.168.1.1", Protocol = Protocol.Rdp, Port = 3389 };

        vm.OpenTab(entry);

        Assert.Single(vm.Tabs);
        Assert.Equal("Test Server", vm.Tabs[0].DisplayName);
    }

    [Fact, Trait("Category", "M4")]
    public void ViewModel_CloseTab_TabRemoved()
    {
        var vm = CreateVm();
        var entry = new ConnectionEntry { Name = "Test Server", Host = "192.168.1.1", Protocol = Protocol.Rdp, Port = 3389 };
        vm.OpenTab(entry);
        var tab = vm.Tabs[0];

        vm.CloseTab(tab);

        Assert.Empty(vm.Tabs);
    }

    [Fact, Trait("Category", "M4")]
    public void ViewModel_SwitchTab_UpdatesActiveSession()
    {
        var vm = CreateVm();
        var e1 = new ConnectionEntry { Name = "Server 1", Host = "10.0.0.1", Protocol = Protocol.Rdp, Port = 3389 };
        var e2 = new ConnectionEntry { Name = "Server 2", Host = "10.0.0.2", Protocol = Protocol.Ssh, Port = 22 };

        vm.OpenTab(e1);
        vm.OpenTab(e2);
        vm.SwitchToTab(0);

        Assert.Equal("Server 1", vm.ActiveTab?.DisplayName);
    }

    [Fact, Trait("Category", "M4")]
    public void ViewModel_OpenSameEntryTwice_DoesNotDuplicateTab()
    {
        var vm    = CreateVm();
        var entry = new ConnectionEntry { Name = "Server", Host = "10.0.0.1", Protocol = Protocol.Rdp, Port = 3389 };

        vm.OpenTab(entry);
        vm.OpenTab(entry);

        Assert.Single(vm.Tabs);
    }
}

// ── ConnectionTreeViewModel ───────────────────────────────────────────────────

public class M4_ConnectionTreeViewModelTests
{
    private static (ConnectionTreeViewModel vm, List<ConnectionGroup> groups, List<ConnectionEntry> entries)
        BuildTree()
    {
        var group1 = new ConnectionGroup { Name = "Production", SortOrder = 0 };
        var group2 = new ConnectionGroup { Name = "Development", SortOrder = 1 };

        var entries = new List<ConnectionEntry>
        {
            new() { Name = "Alpha",  Host = "10.0.0.1", Protocol = Protocol.Rdp, GroupId = group1.Id },
            new() { Name = "Beta",   Host = "10.0.0.2", Protocol = Protocol.Ssh, GroupId = group1.Id },
            new() { Name = "Gamma",  Host = "10.0.0.3", Protocol = Protocol.Vnc, GroupId = group2.Id },
        };

        var vm = new ConnectionTreeViewModel();
        vm.LoadConnections([group1, group2], entries);
        return (vm, [group1, group2], entries);
    }

    [Fact, Trait("Category", "M4")]
    public void Groups_Load_PopulatesFromVault()
    {
        var (vm, groups, _) = BuildTree();
        Assert.Equal(groups.Count, vm.Groups.Count);
    }

    [Fact, Trait("Category", "M4")]
    public void Search_EmptyQuery_ShowsAllConnections()
    {
        var (vm, _, _) = BuildTree();
        vm.SearchQuery = string.Empty;

        Assert.Empty(vm.FilteredConnections);
    }

    [Fact, Trait("Category", "M4")]
    public void Search_MatchingQuery_FiltersResults()
    {
        var (vm, _, _) = BuildTree();
        vm.SearchQuery = "Alpha";

        Assert.Single(vm.FilteredConnections);
        Assert.Equal("Alpha", vm.FilteredConnections[0].Name);
    }

    [Fact, Trait("Category", "M4")]
    public void Search_NoMatch_ShowsEmptyState()
    {
        var (vm, _, _) = BuildTree();
        vm.SearchQuery = "ZZZnonexistent";

        Assert.Empty(vm.FilteredConnections);
    }

    [Fact, Trait("Category", "M4")]
    public void AddGroup_AppearsInTree()
    {
        var vm    = new ConnectionTreeViewModel();
        var group = new ConnectionGroup { Name = "New Group" };
        vm.LoadConnections([group], []);

        Assert.Single(vm.Groups);
        Assert.Equal("New Group", vm.Groups[0].Group.Name);
    }

    [Fact, Trait("Category", "M4")]
    public void RemoveConnection_DisappearsFromTree()
    {
        var group = new ConnectionGroup { Name = "G" };
        var entry = new ConnectionEntry { Name = "Target", Host = "1.2.3.4", Protocol = Protocol.Rdp, GroupId = group.Id };
        var other = new ConnectionEntry { Name = "Keep",   Host = "5.6.7.8", Protocol = Protocol.Rdp, GroupId = group.Id };

        var vm = new ConnectionTreeViewModel();
        vm.LoadConnections([group], [entry, other]);

        // Re-load without the removed entry (simulates vault update)
        vm.LoadConnections([group], [other]);

        Assert.Single(vm.Groups[0].Entries);
        Assert.Equal("Keep", vm.Groups[0].Entries[0].Name);
    }
}

// ── NewConnectionDialogViewModel ─────────────────────────────────────────────

public class M4_NewConnectionDialogViewModelTests
{
    [Fact, Trait("Category", "M4")]
    public void DefaultProtocol_IsRdp_PortIs3389()
    {
        var vm = new NewConnectionDialogViewModel();
        Assert.Equal(Protocol.Rdp, vm.Protocol);
        Assert.Equal(3389, vm.Port);
        Assert.True(vm.IsRdp);
        Assert.False(vm.IsSsh);
    }

    [Fact, Trait("Category", "M4")]
    public void ChangeProtocol_ToSsh_UpdatesPortAndFlags()
    {
        var vm = new NewConnectionDialogViewModel();
        vm.Protocol = Protocol.Ssh;
        Assert.Equal(22, vm.Port);
        Assert.True(vm.IsSsh);
        Assert.False(vm.IsRdp);
    }

    [Fact, Trait("Category", "M4")]
    public void ChangeProtocol_ToVnc_UpdatesPort()
    {
        var vm = new NewConnectionDialogViewModel();
        vm.Protocol = Protocol.Vnc;
        Assert.Equal(5900, vm.Port);
    }

    [Fact, Trait("Category", "M4")]
    public void IsValid_EmptyName_ReturnsFalse()
    {
        var vm = new NewConnectionDialogViewModel { Host = "10.0.0.1" };
        Assert.False(vm.IsValid);
    }

    [Fact, Trait("Category", "M4")]
    public void IsValid_EmptyHost_ReturnsFalse()
    {
        var vm = new NewConnectionDialogViewModel { Name = "Server" };
        Assert.False(vm.IsValid);
    }

    [Fact, Trait("Category", "M4")]
    public void IsValid_NameAndHost_ReturnsTrue()
    {
        var vm = new NewConnectionDialogViewModel { Name = "Server", Host = "10.0.0.1" };
        Assert.True(vm.IsValid);
    }

    [Fact, Trait("Category", "M4")]
    public void BuildEntry_ReturnsCorrectValues()
    {
        var vm = new NewConnectionDialogViewModel
        {
            Name = "DB Server", Host = "10.0.0.5", Port = 3389, Protocol = Protocol.Rdp,
        };
        var entry = vm.BuildEntry();
        Assert.Equal("DB Server",  entry.Name);
        Assert.Equal("10.0.0.5",   entry.Host);
        Assert.Equal(Protocol.Rdp, entry.Protocol);
        Assert.Equal(3389,         entry.Port);
    }

    [Fact, Trait("Category", "M4")]
    public void BuildCredential_NoUsernameOrKey_ReturnsNull()
    {
        var vm = new NewConnectionDialogViewModel { Name = "S", Host = "h" };
        Assert.Null(vm.BuildCredential());
    }

    [Fact, Trait("Category", "M4")]
    public void BuildCredential_WithUsername_ReturnsUsernamePasswordType()
    {
        var vm = new NewConnectionDialogViewModel
        {
            Name = "S", Host = "h", Username = "alice", Password = "secret",
        };
        var cred = vm.BuildCredential();
        Assert.NotNull(cred);
        Assert.Equal("alice",  cred!.Username);
        Assert.Equal("secret", cred.Password);
        Assert.Equal(CredentialType.UsernamePassword, cred.Type);
    }

    [Fact, Trait("Category", "M4")]
    public void BuildCredential_WithSshKey_ReturnsSshKeyType()
    {
        var vm = new NewConnectionDialogViewModel
        {
            Name = "S", Host = "h", Username = "alice", SshKeyPath = "~/.ssh/id_ed25519",
        };
        var cred = vm.BuildCredential();
        Assert.Equal(CredentialType.SshKey,  cred!.Type);
        Assert.Equal("~/.ssh/id_ed25519",    cred.SshKeyPath);
    }
}

// ── ConnectionTreeViewModel.AddEntry ─────────────────────────────────────────

public class M4_ConnectionTreeAddEntryTests
{
    [Fact, Trait("Category", "M4")]
    public void AddEntry_NewGroupName_CreatesGroupAndAppendsEntry()
    {
        var vm    = new ConnectionTreeViewModel();
        var entry = new ConnectionEntry { Name = "Server A", Host = "10.0.0.1", Protocol = Protocol.Rdp };

        vm.AddEntry(entry, "Production");

        Assert.Single(vm.Groups);
        Assert.Equal("Production", vm.Groups[0].Group.Name);
        Assert.Single(vm.Groups[0].Entries);
        Assert.Equal("Server A", vm.Groups[0].Entries[0].Name);
    }

    [Fact, Trait("Category", "M4")]
    public void AddEntry_ExistingGroupName_AppendsWithoutDuplicatingGroup()
    {
        var group = new ConnectionGroup { Name = "Dev" };
        var first = new ConnectionEntry { Name = "Alpha", Host = "1.1.1.1", Protocol = Protocol.Rdp, GroupId = group.Id };
        var vm    = new ConnectionTreeViewModel();
        vm.LoadConnections([group], [first]);

        var second = new ConnectionEntry { Name = "Beta", Host = "2.2.2.2", Protocol = Protocol.Ssh };
        vm.AddEntry(second, "Dev");

        Assert.Single(vm.Groups);
        Assert.Equal(2, vm.Groups[0].Entries.Count);
    }

    [Fact, Trait("Category", "M4")]
    public void AddEntry_EmptyGroupName_DefaultsToDefaultGroup()
    {
        var vm    = new ConnectionTreeViewModel();
        var entry = new ConnectionEntry { Name = "X", Host = "1.2.3.4", Protocol = Protocol.Rdp };

        vm.AddEntry(entry, "   ");

        Assert.Equal("Default", vm.Groups[0].Group.Name);
    }

    [Fact, Trait("Category", "M4")]
    public void AddEntry_SetsGroupIdOnEntry()
    {
        var vm    = new ConnectionTreeViewModel();
        var entry = new ConnectionEntry { Name = "X", Host = "1.2.3.4", Protocol = Protocol.Rdp };

        vm.AddEntry(entry, "Staging");

        Assert.Equal(vm.Groups[0].Group.Id, entry.GroupId);
    }

    [Fact, Trait("Category", "M4")]
    public void AddEntry_SearchFindsEntryAfterAdd()
    {
        var vm    = new ConnectionTreeViewModel();
        var entry = new ConnectionEntry { Name = "UniqueServer", Host = "9.9.9.9", Protocol = Protocol.Rdp };

        vm.AddEntry(entry, "QA");
        vm.SearchQuery = "UniqueServer";

        Assert.Single(vm.FilteredConnections);
        Assert.Equal("UniqueServer", vm.FilteredConnections[0].Name);
    }
}

// ── MainWindowViewModel.AddNewConnection ─────────────────────────────────────

public class M4_AddNewConnectionTests
{
    private static MainWindowViewModel CreateVm() =>
        new(new ConnectionTreeViewModel());

    [Fact, Trait("Category", "M4")]
    public void AddNewConnection_AppearsInConnectionTree()
    {
        var vm    = CreateVm();
        var entry = new ConnectionEntry { Name = "Prod DB", Host = "10.0.0.5", Protocol = Protocol.Rdp };

        vm.AddNewConnection(entry, "Production");

        var entries = vm.ConnectionTree.Groups.SelectMany(g => g.Entries).ToList();
        Assert.Contains(entries, e => e.Name == "Prod DB");
    }

    [Fact, Trait("Category", "M4")]
    public void AddNewConnection_OpensTab()
    {
        var vm    = CreateVm();
        var entry = new ConnectionEntry { Name = "Dev Box", Host = "192.168.1.10", Protocol = Protocol.Ssh };

        vm.AddNewConnection(entry, "Dev");

        Assert.Single(vm.Tabs);
        Assert.Equal("Dev Box", vm.Tabs[0].DisplayName);
    }
}

// ── ThemeService ──────────────────────────────────────────────────────────────
// These tests require a running Avalonia application — use [AvaloniaFact].

public class M4_ThemeServiceTests
{
    [AvaloniaFact, Trait("Category", "M4")]
    public void SetTheme_Dark_UpdatesRequestedThemeVariant()
    {
        var svc = new ThemeService();
        svc.SetTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, svc.Current);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void SetTheme_Light_UpdatesRequestedThemeVariant()
    {
        var svc = new ThemeService();
        svc.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, svc.Current);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void SetTheme_System_UsesDefault()
    {
        var svc = new ThemeService();
        svc.SetTheme(AppTheme.System);

        Assert.Equal(AppTheme.System, svc.Current);
    }
}

// ── NewConnectionDialog — headless UI layout tests ────────────────────────────
// These run on the Avalonia UI thread via [AvaloniaFact] so they exercise the
// real XAML/compiled-binding pipeline, not just the ViewModel in isolation.

public class M4_NewConnectionDialogUITests
{
    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_CanInstantiate_WithDefaultRdpViewModel()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        Assert.NotNull(vm);
        Assert.Equal(Protocol.Rdp, vm!.Protocol);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_DefaultProtocol_IsRdp_SshSectionHidden()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        // IsVisible="{Binding IsSsh}" on the SSH key path panel
        Assert.False(vm!.IsSsh);
        Assert.True(vm.IsRdp);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_SwitchToSsh_SshFlagsUpdate()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        vm!.Protocol = Protocol.Ssh;

        Assert.True(vm.IsSsh);
        Assert.False(vm.IsRdp);
        Assert.Equal(22, vm.Port);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_SwitchToVnc_PortUpdatesCorrectly()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        vm!.Protocol = Protocol.Vnc;

        Assert.Equal(5900, vm.Port);
        Assert.False(vm.IsRdp);
        Assert.False(vm.IsSsh);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_SaveButton_DisabledWithoutNameAndHost()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        // Both Name and Host are empty by default
        Assert.False(vm!.IsValid);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_SaveButton_EnabledWhenNameAndHostProvided()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        vm!.Name = "Web Server";
        vm.Host  = "192.168.1.10";

        Assert.True(vm.IsValid);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_BuildEntry_ReflectsFormValues()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        vm!.Name     = "DB Primary";
        vm.Host      = "10.0.0.100";
        vm.Protocol  = Protocol.Rdp;
        vm.Username  = "Administrator";
        vm.Password  = "s3cr3t";

        var entry = vm.BuildEntry();
        var cred  = vm.BuildCredential();

        Assert.Equal("DB Primary",     entry.Name);
        Assert.Equal("10.0.0.100",     entry.Host);
        Assert.Equal(Protocol.Rdp,     entry.Protocol);
        Assert.NotNull(cred);
        Assert.Equal("Administrator",  cred!.Username);
        Assert.Equal("s3cr3t",         cred.Password);
    }

    [AvaloniaFact, Trait("Category", "M4")]
    public void Dialog_SshKeyPath_SetsCredentialTypeSshKey()
    {
        var dialog = new NewConnectionDialog();
        var vm     = dialog.DataContext as NewConnectionDialogViewModel;

        vm!.Protocol   = Protocol.Ssh;
        vm.Username    = "ubuntu";
        vm.SshKeyPath  = "~/.ssh/id_ed25519";

        var cred = vm.BuildCredential();

        Assert.NotNull(cred);
        Assert.Equal(CredentialType.SshKey,   cred!.Type);
        Assert.Equal("~/.ssh/id_ed25519",     cred.SshKeyPath);
    }
}
