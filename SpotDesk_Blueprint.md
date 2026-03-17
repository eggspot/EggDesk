# SpotDesk by Eggspot — Full Project Blueprint
> Remote access, at Eggspot speed. | .NET 10 · AvaloniaUI 11 · C# 13

---

## Table of Contents
1. Platform Support Matrix
2. UI/UX Design System
3. Layout & Navigation Blueprint
4. Screen-by-Screen Wireframe Guide
5. Dark/Light Mode Spec
6. Component Library
7. UX Principles for All-Day Use
8. Architecture Overview
9. Linux-Specific Notes
10. Auth & Vault Key Design
11. Milestone Prompts (Ready-to-Use Vibe Coding)

---

## 1. Platform Support Matrix

| Platform       | RDP Backend           | SSH | VNC | Notes                          |
|----------------|-----------------------|-----|-----|-------------------------------|
| Windows 10/11  | AxMSTscLib (COM)      | ✓   | ✓   | NativeAOT binary              |
| macOS 13+      | FreeRDP 3.x (P/Invoke)| ✓   | ✓   | ReadyToRun binary             |
| Linux (X11)    | FreeRDP 3.x (P/Invoke)| ✓   | ✓   | Debian 9+, Ubuntu 16.04+      |
| Linux (Wayland)| FreeRDP 3.x (P/Invoke)| ✓   | ✓   | Via XWayland or native Wayland|

**Linux dependencies (apt):**
```
libfreerdp3 libssh2-1 libvncserver-dev libice6 libsm6 libfontconfig1
```

**Linux RDP note:** On Linux, use FreeRDP via P/Invoke identical to macOS. The `IRdpBackend`
interface abstracts this — Linux and macOS share `FreeRdpBackend.cs`. No separate Linux backend needed.

---

## 2. UI/UX Design System

### 2.1 Design Philosophy

SpotDesk is a **professional tool used for hours at a stretch**. The UI must:
- Disappear when you're working — sessions take full focus, chrome recedes
- Be readable at 8 AM and 10 PM without causing eye strain
- Respond instantly — no spinner for anything under 200ms
- Respect muscle memory — keyboard shortcuts for everything critical

**Aesthetic direction:** Refined utilitarian. Clean geometry, purposeful color, zero decoration.
Think Linear, Warp Terminal, or JetBrains Fleet — not Notion, not Windows XP.

### 2.2 Color Tokens

```xml
<!-- Define in App.axaml as ResourceDictionary -->

<!-- Backgrounds -->
<Color x:Key="BgBase">#0F1117</Color>         <!-- Dark: page bg -->
<Color x:Key="BgSurface">#171B26</Color>       <!-- Dark: sidebar, panels -->
<Color x:Key="BgElevated">#1E2333</Color>      <!-- Dark: cards, modals -->
<Color x:Key="BgHover">#252A3D</Color>         <!-- Dark: hover states -->
<Color x:Key="BgActive">#2D3350</Color>        <!-- Dark: selected item -->

<!-- Light equivalents -->
<Color x:Key="BgBaseLight">#F4F5F7</Color>
<Color x:Key="BgSurfaceLight">#FFFFFF</Color>
<Color x:Key="BgElevatedLight">#FAFAFA</Color>
<Color x:Key="BgHoverLight">#EEF0F4</Color>
<Color x:Key="BgActiveLight">#E3E8F2</Color>

<!-- Accent (Eggspot brand — a clean teal-blue) -->
<Color x:Key="AccentPrimary">#3B82F6</Color>   <!-- Primary action, selected tab -->
<Color x:Key="AccentSubtle">#1E3A5F</Color>    <!-- Dark: accent surface -->
<Color x:Key="AccentText">#93C5FD</Color>      <!-- Dark: accent text on dark bg -->

<!-- Status -->
<Color x:Key="StatusConnected">#22C55E</Color>
<Color x:Key="StatusConnecting">#F59E0B</Color>
<Color x:Key="StatusError">#EF4444</Color>
<Color x:Key="StatusIdle">#6B7280</Color>

<!-- Text -->
<Color x:Key="TextPrimary">#E8EAF0</Color>     <!-- Dark -->
<Color x:Key="TextSecondary">#9AA3B8</Color>   <!-- Dark muted -->
<Color x:Key="TextTertiary">#5A6478</Color>    <!-- Dark hints -->
<Color x:Key="TextPrimaryLight">#111827</Color>
<Color x:Key="TextSecondaryLight">#4B5563</Color>
<Color x:Key="TextTertiaryLight">#9CA3AF</Color>

<!-- Borders -->
<Color x:Key="BorderSubtle">#252A3D</Color>    <!-- Dark hairline -->
<Color x:Key="BorderDefault">#2E3449</Color>   <!-- Dark standard -->
<Color x:Key="BorderSubtleLight">#E5E7EB</Color>
<Color x:Key="BorderDefaultLight">#D1D5DB</Color>
```

### 2.3 Typography

```xml
<!-- Font stack — Avalonia font resource -->
<FontFamily x:Key="FontSans">avares://SpotDesk.UI/Assets/Fonts#Inter, 
    system-ui, -apple-system, sans-serif</FontFamily>
<FontFamily x:Key="FontMono">avares://SpotDesk.UI/Assets/Fonts#JetBrains Mono, 
    Consolas, monospace</FontFamily>

<!-- Type scale -->
<!-- Display:  22px / 500 — window title, section headers -->
<!-- Heading:  16px / 500 — panel titles, dialog headers -->
<!-- Body:     14px / 400 — connection names, labels -->
<!-- Small:    12px / 400 — metadata, timestamps, badges -->
<!-- Micro:    11px / 400 — status dots, tooltips -->
<!-- Mono:     13px / 400 — hostnames, IPs, terminal -->
```

### 2.4 Spacing Scale

```
4px  — icon padding, badge inner
8px  — inline gap, compact list item padding
12px — standard list item padding, form field gap
16px — section padding, card inner padding
24px — panel padding, dialog padding
32px — major section separation
```

### 2.5 Motion

All transitions: 120ms ease-out (snappy, not sluggish).
- Hover: background color only, no size change
- Selection: immediate, no animation
- Panel expand/collapse: 200ms ease-out height
- Session connect: progress bar, linear, no bounce
- Modals: 150ms fade-in + 4px translate-y from bottom
- Never animate what the user is actively controlling (drag, resize)

---

## 3. Layout & Navigation Blueprint

### 3.1 Main Window Layout

```
┌─────────────────────────────────────────────────────────────────┐
│ TITLEBAR (32px) — macOS/Linux: custom; Windows: native extend   │
│ [≡] SpotDesk    [Search ⌘K]           [Sync ↑] [Settings] [─□✕]│
├──────────┬──────────────────────────────────────────────────────┤
│          │  TAB BAR (40px)                                       │
│ SIDEBAR  │  [+ New] [● prod-web-01 ×] [● db-server ×] [...]    │
│ (240px)  ├──────────────────────────────────────────────────────┤
│          │                                                        │
│ Groups   │          SESSION PANE                                  │
│ + My     │          (RDP / SSH / VNC renders here)               │
│   Servers│          Full resolution, no letterbox               │
│   ▸ Prod │                                                        │
│   ▸ Dev  │                                                        │
│   ▸ Stg  │                                                        │
│          │                                                        │
│ Recents  │                                                        │
│  web-01  │                                                        │
│  db-01   │                                                        │
│  ci-box  │                                                        │
│          │                                                        │
│ [+ Add]  │                                                        │
└──────────┴──────────────────────────────────────────────────────┘
```

**Sidebar** is collapsible (⌘\) to icon-only mode (48px) for maximum session space.
**Tab bar** shows protocol icon + hostname + connection status dot + close.
**Session pane** captures all keyboard input when focused — Ctrl+Alt+Break releases focus.

### 3.2 Sidebar Anatomy

```
┌──────────────────────────┐
│ [≡]  SpotDesk    [+]     │  ← app name + new connection button
├──────────────────────────┤
│ ◉ Search...    ⌘K        │  ← fuzzy search bar (always visible)
├──────────────────────────┤
│ ▾ Favorites (3)          │  ← pinned connections
│   ● prod-web-01    [RDP] │
│   ● db-server      [SSH] │
│   ○ backup-box     [VNC] │
├──────────────────────────┤
│ ▾ Production (5)         │  ← collapsible group
│   ● web-01         [RDP] │
│   ● web-02         [RDP] │
│   ● db-primary     [SSH] │
│   ○ db-replica     [SSH] │
│   ○ cache-01       [SSH] │
├──────────────────────────┤
│ ▸ Staging (3)            │  ← collapsed group
├──────────────────────────┤
│ ▸ Development (8)        │
├──────────────────────────┤
│ ─────── Recent ──────── │
│   ● db-primary     2m    │
│   ○ web-01         1h    │
└──────────────────────────┘
```

Status dot: ● green = connected, ◑ amber = connecting, ○ gray = idle, ✕ red = error

### 3.3 Session Tab Bar

Each tab: `[protocol-icon] hostname [status-dot] [×]`

- Max tab width: 200px. Overflow: horizontal scroll (no dropdown).
- Active tab: accent underline (2px), slightly lighter background.
- Right-click tab: Disconnect | Reconnect | Rename | Duplicate | Close All Others | Close.
- Double-click tab: rename inline.
- Middle-click: close.

---

## 4. Screen-by-Screen Wireframe Guide

### 4.1 Welcome Screen (no sessions open)

```
┌─────────────────────────────────────────┐
│                                         │
│          SpotDesk  by Eggspot           │
│                                         │
│     Connect to your servers faster.    │
│                                         │
│    [+ New Connection]  [Import from RDM]│
│                                         │
│  ─────────── Recent ────────────────── │
│  ● prod-web-01    RDP    2 min ago     │
│  ○ db-primary     SSH    1 hour ago    │
│  ○ staging-box    VNC    yesterday     │
│                                         │
│  ─────────── Quick Tips ──────────── │
│  ⌘K  Search connections               │
│  ⌘N  New connection                   │
│  ⌘\  Toggle sidebar                   │
└─────────────────────────────────────────┘
```

### 4.2 New Connection Dialog

```
┌──────────────────────────────────────────────────┐
│  New Connection                              [✕]  │
├──────────────────────────────────────────────────┤
│  Protocol   [RDP ▾]  [SSH]  [VNC]                │
│                                                    │
│  Name        [prod-web-01                    ]     │
│  Host        [192.168.1.100                  ]     │
│  Port        [3389       ]                         │
│                                                    │
│  Credential  [+ New credential ▾              ]    │
│              [── alice (saved) ──            ]     │
│              [── bob (saved) ────            ]     │
│                                                    │
│  Group       [Production ▾                   ]     │
│  Tags        [web] [prod] [+]                      │
│                                                    │
│  ▸ Advanced (resolution, color depth, etc.)        │
│                                                    │
│       [Cancel]           [Save & Connect]          │
└──────────────────────────────────────────────────┘
```

### 4.3 Active RDP Session

```
┌─────────────────────────────────────────────────┐
│ [≡] [+ New] [●web-01 ×] [●db-01 ×]   [⚙] [─□✕]│
├────────────────────────────────────────────────-┤
│                                                  │
│   [Full Windows/Linux desktop renders here]      │
│                                                  │
│   Resolution: matches SpotDesk window size       │
│   Input: all keyboard/mouse captured             │
│   Release focus: Ctrl+Alt+Break                  │
│                                                  │
│                                                  │
└──────────────────[status: ● Connected 24ms]──────┘
```

Session toolbar (appears on hover at top of session pane, auto-hides):
`[Fit Window] [Full Screen ⌘⇧F] [Screenshot] [Send Ctrl+Alt+Del] [Transfer Files] [Disconnect]`

### 4.4 SSH Terminal View

```
┌──────────────────────────────────────────────────┐
│ [≡] [+ New] [●web-01×] [●db-01: SSH ×]          │
├──────────────────────────────────────────────────┤
│ JetBrains Mono 13px, background #0F1117          │
│                                                   │
│ user@db-primary:~$ sudo systemctl status nginx    │
│ ● nginx.service - A high performance web server  │
│    Loaded: loaded (/lib/systemd/system/nginx...  │
│    Active: active (running) since Mon 2026-01... │
│                                                   │
│ user@db-primary:~$ █                             │
│                                                   │
│                                                   │
│                                                   │
└──────── [● Connected] [db-primary] [SSH] ────────┘
```

Terminal features:
- Full VT100/xterm-256color support
- Click URL to open in browser
- Right-click → Copy / Paste / Clear / Split Pane
- Split pane: vertical or horizontal (⌘D / ⌘⇧D)
- Search in terminal: ⌘F

### 4.5 Settings Screen

Sections: General | Appearance | Vault & Sync | Trusted Devices | SSH Keys | Shortcuts | About

**Vault & Sync section (OAuth-first — no master password shown by default):**
```
Vault & Sync
─────────────────────────────────────────────
Identity         ● github.com/you  (GitHub OAuth)
                 [Disconnect]

Vault status     ● Unlocked · derived from GitHub identity
                 [Lock Now]  [Lock on screen lock  ○]

Git remote       github.com/you/spotdesk-vault  (auto-configured)
                 [Change repo]

Last synced      2 minutes ago  [Sync Now ↑]
Auto-sync        [Every 5 min ▾]

Encryption       AES-256-GCM · Argon2id · per-device key envelope

─── Advanced ───────────────────────────────
                 [Switch to master password mode]
                 Use this only if you cannot use GitHub/Bitbucket OAuth.
─────────────────────────────────────────────
```

**Trusted Devices section:**
```
Trusted Devices
─────────────────────────────────────────────
These devices can unlock your vault.

● MacBook Pro (work)     this device    Added 2026-03-01
● Windows PC (home)                     Added 2026-03-10
○ Old Laptop                            Added 2025-11-05  [Revoke]

[+ Approve new device]
─────────────────────────────────────────────
```

### 4.6 Import Wizard (from Devolutions RDM)

```
Import from Remote Desktop Manager        Step 1 of 3

  ┌─────────────────────────────────────┐
  │                                     │
  │   Drag your .rdm file here          │
  │   or click to browse                │
  │                                     │
  └─────────────────────────────────────┘

  Supports: .rdm  .rdp  .rdg  .csv

  [Cancel]                        [Next →]
```

Step 2: Enter master key if encrypted, preview entry list with checkboxes.
Step 3: Map to SpotDesk groups, confirm import.

---

## 5. Dark/Light Mode Spec

### 5.1 Implementation in AvaloniaUI

```xml
<!-- App.axaml -->
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://SpotDesk.UI/Styles/DarkTheme.axaml"/>
</Application.Styles>

<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceInclude Source="avares://SpotDesk.UI/Styles/ColorTokens.axaml"/>
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

```csharp
// ThemeService.cs
public class ThemeService
{
    public void SetTheme(AppTheme theme)
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = theme switch
        {
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }
}

public enum AppTheme { Dark, Light, System }
```

### 5.2 Dynamic Resources Pattern

Always use `DynamicResource` not `StaticResource` for theme tokens:

```xml
<!-- ✓ Correct — updates on theme switch -->
<Border Background="{DynamicResource BgSurface}">

<!-- ✗ Wrong — baked in at startup -->
<Border Background="{StaticResource BgSurface}">
```

### 5.3 Session Pane in Light Mode

The session pane (RDP/VNC framebuffer) has no theme — it shows the remote desktop as-is.
The surrounding chrome (tab bar, sidebar) switches theme. This is correct and expected.

---

## 6. Component Library

### 6.1 ConnectionListItem.axaml

```xml
<UserControl x:Class="SpotDesk.UI.Controls.ConnectionListItem">
  <Grid ColumnDefinitions="24,*,Auto,Auto" Height="40"
        Background="Transparent" Cursor="Hand">
    
    <!-- Status dot -->
    <Ellipse Grid.Column="0" Width="8" Height="8"
             Fill="{Binding StatusColor}" VerticalAlignment="Center"
             Margin="8,0,0,0"/>
    
    <!-- Name + host -->
    <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="8,0">
      <TextBlock Text="{Binding DisplayName}" FontSize="14"
                 Foreground="{DynamicResource TextPrimary}"/>
      <TextBlock Text="{Binding Host}" FontSize="11"
                 Foreground="{DynamicResource TextTertiary}"/>
    </StackPanel>
    
    <!-- Protocol badge -->
    <Border Grid.Column="2" CornerRadius="4" Padding="4,2"
            Background="{DynamicResource BgHover}" Margin="4,0">
      <TextBlock Text="{Binding Protocol}" FontSize="11"
                 Foreground="{DynamicResource TextSecondary}"
                 FontFamily="{DynamicResource FontMono}"/>
    </Border>
    
    <!-- Connect button (shown on hover via trigger) -->
    <Button Grid.Column="3" Content="Connect" FontSize="12"
            Command="{Binding ConnectCommand}" Margin="4,0"
            IsVisible="{Binding IsHovered}"/>
  </Grid>
</UserControl>
```

### 6.2 SessionTab.axaml

```xml
<UserControl x:Class="SpotDesk.UI.Controls.SessionTab">
  <Border Padding="12,0" MinWidth="120" MaxWidth="200" Height="40"
          BorderThickness="0,0,0,2"
          BorderBrush="{Binding IsActive, 
            Converter={StaticResource ActiveTabBrushConverter}}">
    <Grid ColumnDefinitions="16,*,8,16">
      <!-- Protocol icon (SVG path) -->
      <Path Grid.Column="0" Data="{Binding ProtocolIcon}"
            Fill="{DynamicResource TextSecondary}" Width="14" Height="14"/>
      
      <!-- Hostname -->
      <TextBlock Grid.Column="1" Text="{Binding DisplayName}"
                 FontSize="13" VerticalAlignment="Center"
                 Margin="6,0" TextTrimming="CharacterEllipsis"/>
      
      <!-- Status dot -->
      <Ellipse Grid.Column="2" Width="6" Height="6"
               Fill="{Binding StatusColor}" VerticalAlignment="Center"/>
      
      <!-- Close button -->
      <Button Grid.Column="3" Content="✕" FontSize="11"
              Command="{Binding CloseCommand}"
              Classes="ghost-button"/>
    </Grid>
  </Border>
</UserControl>
```

### 6.3 VaultUnlockDialog.axaml

```xml
<Window x:Class="SpotDesk.UI.Dialogs.VaultUnlockDialog"
        Width="380" Height="220" CanResize="False"
        WindowStartupLocation="CenterOwner">
  <StackPanel Margin="32" Spacing="16">
    <TextBlock Text="Unlock vault" FontSize="18" FontWeight="Medium"/>
    <TextBlock Text="Enter your master password to access credentials."
               FontSize="13" Foreground="{DynamicResource TextSecondary}"
               TextWrapping="Wrap"/>
    <TextBox PasswordChar="●" Watermark="Master password"
             x:Name="PasswordBox" FontSize="14"/>
    <CheckBox Content="Remember for this session" IsChecked="{Binding RememberSession}"/>
    <Button Content="Unlock" Command="{Binding UnlockCommand}"
            HorizontalAlignment="Stretch" Classes="primary-button"
            IsDefault="True"/>
  </StackPanel>
</Window>
```

---

## 7. UX Principles for All-Day Use

### 7.1 Eye Strain Prevention

- Default theme: **Dark** (BgBase #0F1117 — not pure black, reduces halation)
- Sidebar dimmer than session pane chrome — hierarchy through luminance
- Status colors use muted versions at rest, saturated only for alerts
- Terminal: background #0F1117, foreground #E8EAF0 — 9:1 contrast ratio
- No pure white text on dark — use #E8EAF0 (slight warmth)

### 7.2 Keyboard-First Design

| Shortcut         | Action                          |
|------------------|---------------------------------|
| ⌘K / Ctrl+K      | Global search                   |
| ⌘N / Ctrl+N      | New connection                  |
| ⌘W / Ctrl+W      | Close active tab                |
| ⌘\ / Ctrl+\      | Toggle sidebar                  |
| ⌘1–9 / Ctrl+1–9  | Switch to tab N                 |
| ⌘⇧F / Ctrl+Shft+F| Toggle full screen session      |
| Ctrl+Alt+Break   | Release keyboard focus from RDP |
| ⌘R / Ctrl+R      | Reconnect active session        |
| ⌘, / Ctrl+,      | Open settings                   |
| ⌘⇧S / Ctrl+Shft+S| Force Git sync                  |

### 7.3 Session Continuity

- Tab switching must be < 50ms — reattach existing framebuffer, never reconnect
- On accidental disconnect: auto-reconnect with 3s countdown + cancel button
- Session state persisted in memory for the app lifetime — only evicted on explicit close
- "Reconnect All" button in sidebar header for morning reconnection ritual

### 7.4 Fatigue-Reducing Micro-UX

- Connection list groups auto-collapse after 10+ entries — one group visible at a time
- Last active group reopens on launch
- Tab order remembered across app restarts (saved to local prefs, not vault)
- Search history: last 5 queries saved locally
- Quick-connect from search: type hostname, hit Enter — connects without opening dialog

### 7.5 Status Transparency

Status bar (bottom of session pane):
```
● Connected  ·  prod-web-01  ·  RDP  ·  24ms  ·  1920×1080  ·  AVC444
```
Latency shown as colored number: green <50ms, amber 50–150ms, red >150ms.

---

## 8. Architecture Overview

### 8.1 Project Structure

```
SpotDesk.sln
├── src/
│   ├── SpotDesk.Core/
│   │   ├── Models/
│   │   │   ├── ConnectionEntry.cs
│   │   │   ├── CredentialEntry.cs
│   │   │   ├── ConnectionGroup.cs
│   │   │   └── SessionState.cs
│   │   ├── Crypto/
│   │   │   ├── VaultCrypto.cs          # AES-256-GCM encrypt/decrypt
│   │   │   ├── KeyDerivation.cs        # Argon2id — device key + master key
│   │   │   └── DeviceIdService.cs      # stable per-device fingerprint
│   │   ├── Vault/
│   │   │   ├── VaultService.cs         # main orchestrator (UnlockAsync etc.)
│   │   │   ├── VaultModel.cs           # VaultFile, DeviceEnvelope, VaultEntry
│   │   │   └── SessionLockService.cs   # holds masterKey in pinned memory
│   │   ├── Sync/
│   │   │   ├── GitSyncService.cs       # LibGit2Sharp push/pull
│   │   │   └── ConflictResolver.cs     # last-write-wins by updatedAt
│   │   ├── Auth/
│   │   │   ├── OAuthService.cs         # PKCE loopback orchestrator
│   │   │   ├── GitHubOAuth.cs          # GitHub identity + token
│   │   │   ├── BitbucketOAuth.cs       # Bitbucket identity + token
│   │   │   ├── KeychainService.cs      # OS keychain abstraction
│   │   │   └── MasterPasswordFallback.cs # optional fallback path
│   │   └── Import/
│   │       ├── DevolutionsImporter.cs  # .rdm XML parser
│   │       ├── RdpFileImporter.cs      # .rdp key=value parser
│   │       └── ImportResult.cs
│   │
│   ├── SpotDesk.Protocols/
│   │   ├── IRdpBackend.cs
│   │   ├── IRdpSession.cs
│   │   ├── Windows/
│   │   │   └── WindowsRdpBackend.cs    # AxMSTscLib COM [net10.0-windows]
│   │   ├── FreeRdp/
│   │   │   ├── FreeRdpBackend.cs       # P/Invoke [net10.0]
│   │   │   └── FreeRdpNative.cs        # [LibraryImport] declarations
│   │   ├── Ssh/
│   │   │   ├── SshSessionManager.cs    # SSH.NET
│   │   │   ├── SshSession.cs
│   │   │   └── Terminal/
│   │   │       ├── Vt100Parser.cs      # VT100/xterm-256color
│   │   │       └── TerminalBuffer.cs   # System.IO.Pipelines
│   │   └── Vnc/
│   │       └── VncSessionManager.cs    # RemoteViewing
│   │
│   ├── SpotDesk.UI/
│   │   ├── App.axaml + App.axaml.cs
│   │   ├── Assets/
│   │   │   └── Fonts/ (Inter, JetBrains Mono)
│   │   ├── Styles/
│   │   │   ├── ColorTokens.axaml
│   │   │   ├── DarkTheme.axaml
│   │   │   ├── LightTheme.axaml
│   │   │   └── Controls.axaml
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── ConnectionTreeViewModel.cs
│   │   │   ├── SessionTabViewModel.cs
│   │   │   ├── SettingsViewModel.cs
│   │   │   └── ImportWizardViewModel.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml
│   │   │   ├── WelcomeView.axaml
│   │   │   ├── RdpView.axaml
│   │   │   ├── SshView.axaml
│   │   │   ├── VncView.axaml
│   │   │   └── SettingsView.axaml
│   │   ├── Controls/
│   │   │   ├── ConnectionListItem.axaml
│   │   │   ├── SessionTab.axaml
│   │   │   ├── StatusBar.axaml
│   │   │   ├── SearchBox.axaml
│   │   │   └── ProtocolBadge.axaml
│   │   └── Dialogs/
│   │       ├── OAuthConnectDialog.axaml      # first-time OAuth flow
│   │       ├── ApproveDeviceDialog.axaml     # approve new device
│   │       ├── NewConnectionDialog.axaml
│   │       ├── CredentialEditorDialog.axaml
│   │       └── ImportWizard.axaml
│   │
│   └── SpotDesk.App/
│       ├── Program.cs               # Entry point, DI bootstrap
│       └── PlatformBootstrap.cs     # OS-specific init
│
└── tests/
    ├── SpotDesk.Core.Tests/
    └── SpotDesk.Protocols.Tests/
```

### 8.2 Dependency Injection Bootstrap

```csharp
// Program.cs
var services = new ServiceCollection()
    // Auth & key management
    .AddSingleton<IKeychainService, KeychainService>()
    .AddSingleton<IDeviceIdService, DeviceIdService>()
    .AddSingleton<IKeyDerivationService, KeyDerivationService>()
    .AddSingleton<IOAuthService, OAuthService>()
    .AddSingleton<ISessionLockService, SessionLockService>()
    // Vault & sync
    .AddSingleton<IVaultService, VaultService>()
    .AddSingleton<IGitSyncService, GitSyncService>()
    // Protocols
    .AddSingleton<ISessionManager, SessionManager>()
    .AddSingleton<IRdpBackend>(sp =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsRdpBackend()
            : new FreeRdpBackend())
    // UI
    .AddSingleton<ThemeService>()
    .AddTransient<MainWindowViewModel>()
    .BuildServiceProvider();
```

### 8.3 Session Manager (Performance Core)

```csharp
// SessionManager.cs — keeps sessions alive across tab switches
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, ISession> _sessions = new();

    // Attach existing session to a new view — no reconnect
    public ISession GetOrCreate(ConnectionEntry entry)
    {
        return _sessions.GetOrAdd(entry.Id, id => CreateSession(entry));
    }

    // Evict only on explicit close
    public void Close(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
            session.Dispose();
    }

    // DNS prefetch on startup
    public async Task PrefetchDnsAsync(IEnumerable<ConnectionEntry> entries)
    {
        await Parallel.ForEachAsync(entries, async (entry, ct) =>
            await Dns.GetHostAddressesAsync(entry.Host, ct));
    }
}
```

---

## 9. Linux-Specific Notes

### 9.1 FreeRDP on Linux

The same `FreeRdpBackend.cs` used on macOS works on Linux. The native library name differs:

```csharp
// FreeRdpNative.cs
internal static partial class FreeRdpNative
{
    private const string LibName =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "freerdp3.dll" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "libfreerdp3.dylib" :
                                                               "libfreerdp3.so.3";

    [LibraryImport(LibName)]
    internal static partial IntPtr freerdp_new();
}
```

### 9.2 AvaloniaUI on Linux

AvaloniaUI 11 supports both X11 and Wayland on Linux natively.
```xml
<!-- SpotDesk.App.csproj -->
<PackageReference Include="Avalonia.X11" Version="11.*" />
```

No separate Wayland package needed — AvaloniaUI auto-detects display server.

### 9.3 Linux Distribution

Package as:
- `.deb` for Debian/Ubuntu: `dotnet publish` → `dpkg-deb`
- `.rpm` for Fedora/RHEL
- AppImage for universal distribution (recommended for first release)
- Flatpak as stretch goal

### 9.4 SSH Agent Integration (Linux/macOS)

```csharp
// SshSession.cs — use system SSH agent if available
var agent = SshAgent.TryConnect(); // reads $SSH_AUTH_SOCK
if (agent != null)
    authMethods.Add(new SshAgentAuthenticationMethod(username, agent));
```

### 9.5 Keychain on Linux

Linux has two common secrets backends depending on the desktop environment:

```csharp
// KeychainService.cs — Linux implementation
// Attempt libsecret (GNOME/most distros), fall back to encrypted file
public class LinuxKeychainService : IKeychainService
{
    public string? Retrieve(string key)
    {
        // Try D-Bus org.freedesktop.secrets (libsecret / KWallet)
        if (DbusSecretsAvailable())
            return DbusRetrieve(key);
        // Fallback: AES-encrypted file in ~/.config/spotdesk/keystore
        return EncryptedFileRetrieve(key);
    }
}
```

The fallback encrypted file uses the machine's `/etc/machine-id` as a KDF input so it is device-bound even without a secrets daemon running.

---

## 10. Auth & Vault Key Design

### 10.1 Philosophy — No Master Password by Default

SpotDesk eliminates the master password prompt entirely for users who authenticate via GitHub or Bitbucket OAuth. The vault key is **derived** from the user's stable OAuth identity combined with a device fingerprint. The user authenticates once per device via their browser — from then on, every app launch unlocks silently in under 100ms.

```
User launches SpotDesk
        │
        ▼
Keychain has OAuth token?
   YES ──────────────────────────────────────────────────────┐
        │                                                     │
        ▼                                                     ▼
 Fetch GitHub userId          OAuthConnectDialog shown
 (cached 24h in memory)       User clicks "Connect GitHub"
        │                     Browser OAuth → token returned
        ▼                     Token stored in OS keychain
 DeriveDeviceKey(userId,            │
   deviceId)                        ▼
        │                    Same DeriveDeviceKey() path
        ▼
 Load vault.json
 Find DeviceEnvelope matching deviceId
 DecryptMasterKey(envelope, deviceKey)
        │
        ▼
 masterKey in pinned memory — vault unlocked
 App opens normally, no prompt shown
```

### 10.2 Vault File Structure (`vault.json`)

```json
{
  "version": 2,
  "kdf": "argon2id:3:65536:4",
  "devices": [
    {
      "deviceId": "a3f8c2...",
      "deviceName": "MacBook Pro (work)",
      "encryptedMasterKey": "<base64 AES-256-GCM ciphertext>",
      "iv": "<base64 12-byte IV>",
      "addedAt": "2026-03-01T08:00:00Z"
    },
    {
      "deviceId": "d91b44...",
      "deviceName": "Windows PC (home)",
      "encryptedMasterKey": "<base64 AES-256-GCM ciphertext>",
      "iv": "<base64 12-byte IV>",
      "addedAt": "2026-03-10T19:30:00Z"
    }
  ],
  "entries": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "iv": "<base64 12-byte IV>",
      "ciphertext": "<base64 AES-256-GCM ciphertext>"
    }
  ]
}
```

**What is safe to store in Git:** everything in `vault.json`. The `deviceId` is non-sensitive (it identifies a device slot, not a person). The `encryptedMasterKey` is safe because decrypting it requires both the device's derived key AND the specific `deviceId` used as KDF input — neither of which is in the repo.

**What never touches Git:** the OAuth token, the derived device key, and the plaintext master key.

### 10.3 Key Derivation Chain

```
GitHub userId (long)  +  deviceId (SHA-256 of machine data)
           │
           ▼
     Argon2id KDF  (iterations=3, memory=65536, parallelism=4)
     salt = fixed app constant (non-secret)
           │
           ▼
     deviceKey (32 bytes)  ← unique per user+device pair
           │
           ▼
     AES-256-GCM decrypt DeviceEnvelope.encryptedMasterKey
           │
           ▼
     masterKey (32 bytes)  ← encrypts all vault entries
           │
           ▼
     AES-256-GCM decrypt each VaultEntry.ciphertext
           │
           ▼
     plaintext credential JSON
```

### 10.4 Device ID Computation

```csharp
// DeviceIdService.cs
public class DeviceIdService : IDeviceIdService
{
    public string GetDeviceId()
    {
        var raw = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? File.ReadAllText("/etc/machine-id").Trim()
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? GetMacOsSerialNumber()   // IOKit IOPlatformSerialNumber
            : GetWindowsMachineGuid(); // HKLM\SOFTWARE\Microsoft\Cryptography

        // Hash it so we never store raw machine identifiers
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(raw + "spotdesk-v1")));
    }
}
```

### 10.5 New Device Onboarding

When the user installs SpotDesk on a second machine:

1. App opens → no keychain entry → shows `OAuthConnectDialog`
2. User clicks "Connect GitHub" → OAuth → same GitHub account → same `userId`
3. `deviceId` is different (new machine) → no matching `DeviceEnvelope` in `vault.json`
4. App shows: *"This is a new device. Open SpotDesk on a trusted device to approve it."*
5. On the trusted device: notification appears → Settings → Trusted Devices → `[+ Approve new device]`
6. Trusted device decrypts `masterKey`, re-encrypts it with the new device's `deviceKey`, pushes new `DeviceEnvelope` to Git
7. New device pulls, finds its envelope, unlocks — done

**No passwords exchanged between devices at any point.**

### 10.6 OS Keychain Storage

| Platform | Backend | Key stored under |
|---|---|---|
| Windows | Credential Manager (`CredWrite`) | `spotdesk:oauth:github` |
| macOS | Keychain Access (`SecKeychainAddGenericPassword`) | `spotdesk:oauth:github` |
| Linux (GNOME) | libsecret / D-Bus `org.freedesktop.secrets` | `spotdesk:oauth:github` |
| Linux (KDE) | KWallet via D-Bus | `spotdesk:oauth:github` |
| Linux (none) | AES-encrypted file `~/.config/spotdesk/keystore` | — |

### 10.7 Session Lock Behaviour

| Event | Behaviour |
|---|---|
| App minimised to tray | Vault stays unlocked (masterKey in memory) |
| App window hidden | Vault stays unlocked |
| OS screen locked | Optional: wipe masterKey (user setting, default OFF) |
| App reopened after screen lock | Re-read token from keychain → re-derive → re-decrypt, < 100ms |
| User clicks "Lock Now" | masterKey wiped from pinned memory immediately |
| Token expired / revoked | App detects 401 on next sync → shows re-auth prompt, one click |

### 10.8 Master Password Fallback

Available under Settings → Vault & Sync → Advanced → "Switch to master password mode". Uses the same `VaultModel` and AES-256-GCM — only the key derivation input changes:

```csharp
// MasterPasswordFallback.cs
byte[] masterKey = KeyDerivation.Argon2id(
    password: Encoding.UTF8.GetBytes(userEnteredPassword),
    salt: vaultFile.Salt,  // random 32 bytes, stored in vault.json
    iterations: 3, memory: 65536, parallelism: 4);
```

With "Trust this device" checked, the master password is stored in the OS keychain under `spotdesk:master` and the app unlocks silently on reopen — same zero-friction behaviour as OAuth mode.

---

## 11. Milestone Prompts (Ready-to-Use Vibe Coding)

Copy each prompt directly into the vibe coding session. Do them in order.

---

### Milestone 1 — OAuth-Derived Vault Key (Vault + Crypto + Auth, combined)

> **Replaces the old separate Vault and OAuth milestones.** This is the foundation — build it first, everything else depends on it.

```
Build the SpotDesk.Core vault, cryptography, and auth module in C# targeting .NET 10,
NativeAOT-compatible. There is NO master password by default — the vault key is derived
from the user's GitHub/Bitbucket OAuth identity + a stable device fingerprint.

=== Device ID ===

DeviceIdService.cs : IDeviceIdService
  GetDeviceId() → string
  - Linux:   read /etc/machine-id, trim whitespace
  - macOS:   IOKit IOPlatformSerialNumber via P/Invoke
  - Windows: HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid via Registry
  - Hash result: Convert.ToHexString(SHA256.HashData(UTF8(raw + "spotdesk-v1")))
  - Cache result in memory after first call

=== Key Derivation ===

KeyDerivationService.cs : IKeyDerivationService
  DeriveDeviceKey(githubUserId: long, deviceId: string) → byte[32]
  - Argon2id via Konscious.Security.Cryptography.Argon2
    iterations=3, memory=65536, parallelism=4
  - input = UTF8($"{githubUserId}:{deviceId}")
  - salt = fixed UTF8("spotdesk-device-key-v1") — non-secret, just domain separation
  - This is the DEVICE KEY — it encrypts only the masterKey envelope, not entries

=== Vault Model ===

VaultModel.cs — all record types, System.Text.Json source-gen, AOT-safe:
  record VaultFile {
    int Version,                  // = 2
    string Kdf,                   // "argon2id:3:65536:4"
    string? Salt,                 // only used in master-password fallback mode
    DeviceEnvelope[] Devices,
    VaultEntry[] Entries
  }
  record DeviceEnvelope {
    string DeviceId,              // hex SHA-256, non-sensitive
    string DeviceName,            // user-editable label e.g. "MacBook Pro (work)"
    string EncryptedMasterKey,    // base64 AES-256-GCM(deviceKey, masterKey)
    string IV,                    // base64 12-byte nonce
    DateTimeOffset AddedAt
  }
  record VaultEntry {
    Guid Id,
    string IV,                    // base64 12-byte nonce, unique per entry
    string Ciphertext             // base64 AES-256-GCM(masterKey, UTF8(payloadJson))
  }

=== Crypto ===

VaultCrypto.cs — all methods static, AOT-safe, using System.Security.Cryptography.AesGcm:
  GenerateMasterKey() → byte[32]          // RandomNumberGenerator.GetBytes(32)
  EncryptMasterKey(masterKey, deviceKey) → (ciphertext: byte[], iv: byte[12])
  DecryptMasterKey(ciphertext, iv, deviceKey) → byte[32]
                                          // throws AuthenticationTagMismatchException on wrong key
  EncryptEntry(payload: string, masterKey) → (ciphertext: byte[], iv: byte[12])
  DecryptEntry(ciphertext: byte[], iv: byte[12], masterKey) → string

=== OS Keychain ===

IKeychainService interface: Store(key, value), Retrieve(key) → string?, Delete(key)

WindowsKeychainService  : uses CredWrite/CredRead P/Invoke (Credential Manager)
MacOsKeychainService    : uses SecKeychainAddGenericPassword P/Invoke (Security.framework)
LinuxKeychainService    : tries D-Bus org.freedesktop.secrets (libsecret/KWallet),
                          falls back to AES-encrypted file ~/.config/spotdesk/keystore
                          (key for the fallback file = SHA-256(/etc/machine-id))

Keychain keys used:
  "spotdesk:oauth:github"    — GitHub access token
  "spotdesk:oauth:bitbucket" — Bitbucket access token
  "spotdesk:master"          — master password (fallback mode only)

=== OAuth ===

OAuthService.cs : IOAuthService
  AuthenticateGitHubAsync() → GitHubIdentity { UserId: long, Login: string, AccessToken: string }
  - PKCE flow via IdentityModel.OidcClient
  - Open system browser, listen on http://localhost:{random-port}/callback
  - After token: GET https://api.github.com/user → extract id (long) + login
  - Store token in OS keychain under "spotdesk:oauth:github"
  - Cache UserId in memory for 24h to avoid repeated API calls

  AuthenticateBitbucketAsync() → BitbucketIdentity { UserId: string, Username: string, AccessToken: string }
  - Same PKCE pattern, Bitbucket API v2

  IsAuthenticatedAsync(provider) → bool   // checks keychain
  RevokeAsync(provider)                   // deletes from keychain

=== Session Lock ===

SessionLockService.cs : ISessionLockService
  - Hold masterKey in GCHandle.Alloc(pinned) to prevent GC movement
  - IsUnlocked → bool
  - GetMasterKey() → ReadOnlySpan<byte>   // only callable when IsUnlocked
  - Lock()  — zeroes and frees pinned memory
  - Subscribe to OS screen-lock events (optional, user setting)

=== Vault Service (main orchestrator) ===

VaultService.cs : IVaultService

  UnlockAsync(vaultPath) → UnlockResult (Success | NeedsOAuth | NeedsDeviceApproval | Failed)
    1. Check keychain for GitHub token
    2. If found: get UserId (from cache or API call)
    3. deviceKey = DeriveDeviceKey(userId, deviceId)
    4. Load vault.json; find DeviceEnvelope where DeviceId == current deviceId
    5. If found: DecryptMasterKey → SessionLockService.SetMasterKey → return Success
    6. If vault.json not found (first run): return NeedsOAuth → trigger FirstTimeSetupAsync
    7. If no matching DeviceEnvelope: return NeedsDeviceApproval

  FirstTimeSetupAsync(identity, vaultPath, repoUrl)
    1. GenerateMasterKey()
    2. Derive deviceKey for this device
    3. EncryptMasterKey → first DeviceEnvelope
    4. Write vault.json with empty Entries[]
    5. Git init + push to repoUrl (using OAuth token as credential)

  AddDeviceAsync(newDeviceId, newDeviceName)
    // Called on currently-unlocked trusted device to approve a new device
    1. masterKey = SessionLockService.GetMasterKey()
    2. Derive newDeviceKey = DeriveDeviceKey(userId, newDeviceId)
    3. EncryptMasterKey(masterKey, newDeviceKey) → new DeviceEnvelope
    4. Append to vault.json Devices[], save, push

  RevokeDeviceAsync(deviceId)
    // Remove a device envelope — that device can no longer unlock
    Remove matching DeviceEnvelope, save, push

  AddEntryAsync(payload: string) → VaultEntry
  UpdateEntryAsync(id: Guid, payload: string) → VaultEntry
  RemoveEntryAsync(id: Guid)
  GetAllEntriesAsync() → IReadOnlyList<(Guid Id, string Payload)>
    // All decrypt using SessionLockService.GetMasterKey()

=== Master Password Fallback ===

MasterPasswordFallback.cs
  DeriveKeyFromPassword(password: string, salt: byte[]) → byte[32]
  - Same Argon2id params (iterations=3, memory=65536, parallelism=4)
  - salt stored in VaultFile.Salt (random 32 bytes, generated once)
  - Used instead of DeriveDeviceKey when vault is in password mode
  - With "Trust this device": store password in keychain "spotdesk:master"

=== Tests (xUnit + NSubstitute) ===

- VaultCrypto: encrypt/decrypt round-trip; wrong key throws; tampered ciphertext throws
- KeyDerivation: same inputs → same key (deterministic); different deviceId → different key
- DeviceIdService: mock OS-specific calls; assert hex string format
- VaultService: mock IKeychainService + IDeviceIdService; test all UnlockResult branches
- AddDevice: verify new device can decrypt after approval
- RevokeDevice: verify revoked deviceId can no longer produce valid key
- SessionLockService: GetMasterKey() throws when locked; Lock() zeroes memory
- All types AOT-safe: no reflection, all JSON via source-gen JsonSerializerContext

Project: SpotDesk.Core, net10.0, NativeAOT-compatible
Packages: Konscious.Security.Cryptography.Argon2, IdentityModel.OidcClient,
          Microsoft.Extensions.DependencyInjection, xunit, NSubstitute
```

---

### Milestone 2 — Git Sync

```
Build GitSyncService.cs in SpotDesk.Core using LibGit2Sharp targeting .NET 10.

Requirements:
- Clone a private repo to a local path on first run
- Pull latest on app open (fast-forward only)
- Commit + Push after every vault save with message "spotdesk: sync [timestamp]"
- ConflictResolver.cs: on merge conflict in vault.json, parse both versions, 
  keep newer entry per Id (compare updatedAt timestamp inside decrypted payload)
- ISyncStatus with events: SyncStarted, SyncCompleted, SyncFailed, ConflictResolved
- Support custom Git remote URL (any bare repo), GitHub, Bitbucket
- Handle offline gracefully: queue sync, retry with exponential backoff when online
- Unit tests with in-memory Git repo using LibGit2Sharp test helpers
```

---

### Milestone 3 — RDM Importer

```
Build the import module in SpotDesk.Core for Devolutions Remote Desktop Manager, .NET 10.

Requirements:
- DevolutionsImporter.cs: parse .rdm files (XML format used by Devolutions RDM)
  The .rdm file is XML with <DataEntries> root containing <Connection> elements.
  Each Connection has: Name, Host, Port, ConnectionType (RDPConfigured=1, SSH=66, 
  VNC=12), UserName, Password (may be base64 encoded or plaintext), Group, Tags
- Handle encrypted .rdm files: user supplies master key, decrypt using RDM's 
  AES encryption before XML parsing
- RdpFileImporter.cs: parse standard Windows .rdp files (key:type:value format)
  Extract: full address, username, desktopwidth, desktopheight, session bpp
- ImportResult.cs: record with Connections[], Credentials[], Warnings[], Errors[]
- Map imported entries to SpotDesk ConnectionEntry and CredentialEntry models
- Strip/sanitize passwords: import into SpotDesk vault immediately (re-encrypted)
- Unit tests with sample .rdm and .rdp fixture files
```

---

### Milestone 4 — AvaloniaUI Shell + Theme

```
Build the SpotDesk.UI shell using AvaloniaUI 11 on .NET 10.

Requirements:
- MainWindow.axaml: 3-column layout — sidebar (240px, collapsible) + tab bar 
  (40px) + session pane
- ColorTokens.axaml: full dark/light token set as per design spec
  Dark BgBase=#0F1117, BgSurface=#171B26, AccentPrimary=#3B82F6
  Light BgBase=#F4F5F7, BgSurface=#FFFFFF
- ThemeService.cs: Dark/Light/System with DynamicResource bindings
- ConnectionListItem.axaml: status dot + name + host + protocol badge + 
  connect-on-hover button
- SessionTab.axaml: protocol icon + name + status dot + close
- SidebarView.axaml: collapsible groups (TreeView), recents, search box
- WelcomeView.axaml: shown when no session is open
- All fonts: Inter (body), JetBrains Mono (terminal/hostnames) — embed in assets
- CommunityToolkit.Mvvm for all ViewModels (source gen, AOT-safe)
- Keyboard shortcuts: Ctrl+K search, Ctrl+N new, Ctrl+\ toggle sidebar, 
  Ctrl+1-9 tab switch
```

---

### Milestone 5 — SSH Session + Terminal

```
Build the SSH session and terminal emulator in SpotDesk.Protocols and SpotDesk.UI, .NET 10.

Requirements:
- SshSession.cs using SSH.NET: connect, authenticate (password + private key + 
  SSH agent via $SSH_AUTH_SOCK on Linux/macOS)
- SshSessionManager.cs: connection pool, reuse transport for multiple channels
- Vt100Parser.cs: parse VT100 + xterm-256color escape sequences using 
  System.IO.Pipelines for zero-copy handoff
- TerminalBuffer.cs: 2D char/attr grid with scrollback (10,000 lines default)
- SshView.axaml: Avalonia custom control rendering TerminalBuffer using 
  WriteableBitmap — JetBrains Mono 13px
- Features: 256-color, bold/italic/underline, cursor blink, selection + copy,  
  right-click context menu (Copy/Paste/Clear/Search), URL detection + click
- Terminal search: Ctrl+F highlights matches in buffer
- Split pane: Ctrl+D vertical, Ctrl+Shift+D horizontal (two SshViews in a Grid)
- StatusBar: shows connected status, hostname, latency ping, session uptime
```

---

### Milestone 6 — RDP Session (Windows)

```
Build WindowsRdpBackend.cs for Windows using AxMSTscLib COM interop, .NET 10.

Requirements:
- Target: net10.0-windows only, [SupportedOSPlatform("windows")]
- Use AxMSTscLib (Microsoft.Rdp.Interop NuGet or direct COM reference)
- WindowsRdpBackend : IRdpBackend
- IRdpSession with: Connect(ConnectionEntry, CredentialEntry), Disconnect(), 
  GetFrameBuffer() → WriteableBitmap, SendKeyEvent(), SendMouseEvent()
- Dynamic resolution: resize session when SpotDesk window resizes (UpdateSessionDisplaySettings)
- RdpView.axaml: Avalonia NativeControlHost wrapping the ActiveX control
- Connection quality: report latency via IMsTscAdvancedSettings
- File transfer: enable drive redirection (IMsTscSecuredSettings.StartProgram)
- Auto-reconnect: handle IMsTscAxEvents.OnDisconnected with retry logic
- Performance flags: /rfx /gfx:AVC444 /compression — use RemoteFX when available
```

---

### Milestone 7 — RDP Session (macOS + Linux)

```
Build FreeRdpBackend.cs for macOS and Linux using FreeRDP 3.x P/Invoke, .NET 10.

Requirements:
- FreeRdpNative.cs: [LibraryImport] declarations for freerdp3 
  (libfreerdp3.dylib on macOS, libfreerdp3.so.3 on Linux)
  Functions: freerdp_new, freerdp_free, freerdp_connect, freerdp_disconnect,
  freerdp_settings_set_string, freerdp_settings_set_uint32,
  freerdp_update_get_pointer (for frame buffer access)
- FreeRdpBackend : IRdpBackend — same interface as WindowsRdpBackend
- Frame buffer: access via gdi_get_dirty_region(), copy to Avalonia WriteableBitmap
- Codec: prefer AVC444, fallback to RemoteFX, fallback to RDP classic
- RdpView.axaml on non-Windows: WriteableBitmap rendered in Avalonia Image control,
  updated at 60fps via DispatcherTimer
- Input: forward keyboard (with scan codes) and mouse events to FreeRDP input queue
- Platform detection: inject correct lib name at runtime via RuntimeInformation
```

---

### Milestone 8 — Connection Tree + Search

```
Build the full connection tree UI in SpotDesk.UI, .NET 10.

Requirements:
- ConnectionTreeViewModel.cs (CommunityToolkit.Mvvm):
    - ObservableCollection<ConnectionGroup> Groups
    - ObservableCollection<ConnectionEntry> Recents (last 10)
    - FrozenDictionary<Guid, ConnectionEntry> for O(1) lookup
    - SearchQuery string → filtered flat list (fuzzy match on Name + Host + Tags)
    - DragDrop reordering between groups
    - ContextMenu: Connect | Edit | Duplicate | Delete | Pin to Favorites
- Groups: expand/collapse, drag connections between groups
- Search (Ctrl+K): floating overlay, fuzzy search, keyboard navigation, 
  Enter to connect top result
- Inline rename: double-click name → text box
- Batch operations: select multiple → Delete All / Move to Group
- Quick-connect: type IP in search → hit Enter → connects without dialog
  (auto-detect protocol by port: 3389=RDP, 22=SSH, 5900=VNC)
- Sort options: alphabetical, last connected, protocol, group
```

---

### Milestone 9 — Tab Session Manager

```
Build the tab session management system in SpotDesk.UI, .NET 10.

Requirements:
- SessionManager.cs (singleton): ConcurrentDictionary<Guid, ISession>
  - GetOrCreate(ConnectionEntry): returns existing session if alive, creates new
  - Tab switch: < 50ms — reattach existing framebuffer, never reconnect
  - Dispose(sessionId): called only on explicit tab close
  - PrefetchDnsAsync(entries): parallel DNS resolution on app launch
  - TcpPrewarmAsync(entry): begin TCP handshake on hover (cancel if not used)
- SessionTabViewModel.cs: DisplayName, Protocol, StatusColor, IsActive, 
  ConnectCommand, CloseCommand, ReconnectCommand
- Auto-reconnect: on disconnect, show countdown "Reconnecting in 3s..." + Cancel
- Session state persisted to localprefs (not vault): tab order, last active tab
- Reconnect All button: morning workflow — one click reconnects all tabs
- Tab overflow: horizontal scroll when > 8 tabs, no dropdown
- Status bar: latency (colored: green<50ms, amber<150ms, red>150ms), resolution, codec
```

---

### Milestone 10 — Settings + Sync UI

```
Build the Settings screen and Git sync UI in SpotDesk.UI, .NET 10.

Requirements:
- SettingsView.axaml: tabbed layout — General | Appearance | Vault & Sync |
  Trusted Devices | SSH Keys | Shortcuts | About
- General: default protocol, default group, auto-reconnect toggle,
  terminal font size slider
- Appearance: Dark/Light/System toggle, accent color picker (6 presets),
  sidebar width slider, terminal font size
- Vault & Sync section (OAuth-first, no master password shown by default):
    - Identity row: GitHub avatar + login + "●Connected" badge or [Connect GitHub] button
    - Vault status: "● Unlocked · derived from GitHub identity" + [Lock Now] button
    - "Lock on screen lock" toggle (default OFF)
    - Git remote URL (auto-set from OAuth, editable) + [Sync Now ↑] button
    - Last synced timestamp
    - Auto-sync interval dropdown (off / 1 min / 5 min / 15 min / on save)
    - Encryption info read-only: "AES-256-GCM · Argon2id · per-device key envelope"
    - Advanced section (collapsed by default):
        [Switch to master password mode] — secondary text button
- Trusted Devices section (separate tab):
    - List of DeviceEnvelope entries from vault.json
    - Columns: Device Name | Added | This device? | [Revoke]
    - [+ Approve new device] button — opens ApproveDeviceDialog
    - ApproveDeviceDialog.axaml: shows pending device name + deviceId,
      [Approve] button calls VaultService.AddDeviceAsync, pushes updated vault
- SSH Keys: list of known key files, Add / Remove, test connection
- Shortcuts: editable keybinding table
- OAuthConnectDialog.axaml: shown on first launch (no keychain token found)
    - "Connect SpotDesk to GitHub" heading
    - Explanation: "Your vault is encrypted using your GitHub identity.
      No master password required."
    - [Connect with GitHub] primary button (opens browser)
    - [Connect with Bitbucket] secondary button
    - [Use master password instead] text link (shows MasterPasswordSetupDialog)
- MasterPasswordSetupDialog.axaml: fallback path only
    - Password field + confirm field
    - "Trust this device" checkbox (default ON — stores in OS keychain)
    - [Set up vault] button
```

---

### Milestone 11 — Import Wizard UI

```
Build the 3-step import wizard for Devolutions RDM in SpotDesk.UI, .NET 10.

Requirements:
- ImportWizardViewModel.cs with Step enum: SelectFile | Configure | Confirm
- Step 1: file picker (drag & drop or browse), detect format (.rdm/.rdp/.rdg),
  show file info (size, format, entry count estimate)
- Step 2: if .rdm is encrypted → RDM master key input field + test button
  (note: this is Devolutions' own key, not SpotDesk's — SpotDesk has no master
  password by default; explain this clearly in the UI label)
  Preview entry list: DataGrid with Name | Host | Protocol | Group columns
  Checkboxes for select/deselect individual entries, Select All / None
- Step 3: group mapping — dropdown to map RDM groups → SpotDesk groups
  (create new group option), conflict handling if name exists (skip/rename/overwrite)
  Summary: "Importing 47 connections, 12 credentials"
- Progress view: animated progress bar per entry, show errors inline
- Result view: "✓ 45 imported, 2 skipped (duplicates)" + Open Connection Tree button
- Error handling: malformed XML → friendly error, wrong RDM key → clear message
- Back/Next navigation, all steps keyboard navigable
- On completion: imported credentials are immediately re-encrypted with SpotDesk
  masterKey and pushed to Git vault — user never sees plaintext
```

---

*SpotDesk Blueprint v2.0 — Eggspot Company Limited, Ho Chi Minh City, Vietnam*
*Built with .NET 10 · AvaloniaUI 11 · C# 13*
*Auth: OAuth-derived vault key · No master password required*
