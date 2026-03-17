# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**SpotDesk by Eggspot** — Cross-platform remote desktop manager.
Stack: **.NET 10 · AvaloniaUI 11 · C# 13**
Source of truth: `SpotDesk_Blueprint.md` in repo root.

---

## Build & Run

```bash
# Restore packages
dotnet restore SpotDesk.sln

# Build entire solution
dotnet build SpotDesk.sln

# Run the app (Windows)
dotnet run --project src/SpotDesk.App

# Run tests (all)
dotnet test SpotDesk.sln

# Run single test project
dotnet test tests/SpotDesk.Core.Tests

# Run a single test by name
dotnet test tests/SpotDesk.Core.Tests --filter "FullyQualifiedName~VaultCrypto"

# Publish — Windows (NativeAOT)
dotnet publish src/SpotDesk.App -r win-x64 -c Release

# Publish — macOS (ReadyToRun)
dotnet publish src/SpotDesk.App -r osx-arm64 -c Release

# Publish — Linux AppImage
dotnet publish src/SpotDesk.App -r linux-x64 -c Release
```

---

## Solution Structure

```
SpotDesk.sln
├── src/
│   ├── SpotDesk.Core/        # Domain logic — vault, crypto, sync, auth, import
│   ├── SpotDesk.Protocols/   # RDP/SSH/VNC backends
│   ├── SpotDesk.UI/          # AvaloniaUI views, controls, view-models
│   └── SpotDesk.App/         # Entry point + DI bootstrap + platform init
└── tests/
    ├── SpotDesk.Core.Tests/
    └── SpotDesk.Protocols.Tests/
```

---

## Architecture

### Layer Boundaries

- **SpotDesk.Core** — no UI references, no protocol references. Pure domain.
- **SpotDesk.Protocols** — depends on Core (for `ConnectionEntry`, `CredentialEntry`). No UI.
- **SpotDesk.UI** — depends on Core + Protocols. All ViewModels use `CommunityToolkit.Mvvm` source-gen.
- **SpotDesk.App** — wires DI, bootstraps platform, references all three.

### Key Subsystems

**Vault & Crypto** (`SpotDesk.Core/Vault/`, `SpotDesk.Core/Crypto/`)
No master password by default. Vault key = `Argon2id(githubUserId + deviceId)`.
`VaultService.UnlockAsync()` is the entry point — returns `UnlockResult` enum.
Master key is held pinned in memory via `SessionLockService` (GCHandle).
All vault JSON types use System.Text.Json source-gen for NativeAOT compatibility.

**Auth** (`SpotDesk.Core/Auth/`)
OAuth PKCE flow implemented manually (no OidcClient — GitHub/Bitbucket don't expose OIDC discovery).
Flow: generate verifier+SHA-256 challenge → open system browser → `HttpListener` loopback on random port → validate `state` → POST token exchange.
GitHub token URL: `github.com/login/oauth/access_token` — PKCE, no `client_secret` needed.
Bitbucket token URL: `bitbucket.org/site/oauth2/access_token` — Basic auth with `client_id:client_secret`.
Secrets via env vars: `SPOTDESK_GITHUB_CLIENT_ID`, `SPOTDESK_BITBUCKET_CLIENT_ID`, `SPOTDESK_BITBUCKET_CLIENT_SECRET`.
OS keychain abstracted via `IKeychainService` (Windows: CredWrite, macOS: Keychain, Linux: libsecret → encrypted file fallback).
`OAuthService` caches identity in memory for 24h.

**Git Sync** (`SpotDesk.Core/Sync/`)
`GitSyncService` uses LibGit2Sharp. Fast-forward-only pull. Commit message: `"spotdesk: sync [timestamp]"`.
`ConflictResolver`: last-write-wins by `updatedAt` field inside decrypted entry payload.

**Session Manager** (`SpotDesk.Protocols/` + `SpotDesk.UI/ViewModels/`)
`SessionManager` is a `ConcurrentDictionary<Guid, ISession>` singleton.
Tab switch must be <50ms — reattaches existing framebuffer, never reconnects.
Sessions evicted only on explicit close. TCP pre-warm on hover.

**RDP Backends**
- Windows: `WindowsRdpBackend` — AxMSTscLib COM interop, `[net10.0-windows]` only.
- macOS + Linux: `FreeRdpBackend` — P/Invoke into `libfreerdp3`. Shared binary.
- Both implement `IRdpBackend` / `IRdpSession`. DI injects the right one at runtime.
- Linux and macOS share `FreeRdpBackend.cs`; lib name resolved via `RuntimeInformation`.

**SSH Terminal** (`SpotDesk.Protocols/Ssh/`)
SSH.NET for transport. `Vt100Parser` + `TerminalBuffer` for rendering.
Terminal rendered into `WriteableBitmap` using JetBrains Mono 13px.
`System.IO.Pipelines` for zero-copy VT stream handoff.

**UI Theme**
All theme tokens defined in `Styles/ColorTokens.axaml`. Always use `DynamicResource` (not `StaticResource`) so theme switching works.
Default: Dark. `ThemeService.SetTheme(AppTheme)` drives `RequestedThemeVariant`.

---

## Platform Notes

### Windows
- NativeAOT binary target.
- RDP via AxMSTscLib COM — requires Windows SDK.
- OS keychain: Windows Credential Manager (`CredWrite`/`CredRead`).

### macOS
- ReadyToRun binary.
- RDP via FreeRDP 3.x dylib (`libfreerdp3.dylib`).
- Device ID: `IOKit IOPlatformSerialNumber`.

### Linux
- Requires: `libfreerdp3 libssh2-1 libvncserver-dev libice6 libsm6 libfontconfig1`
- RDP via `libfreerdp3.so.3` — same code path as macOS.
- AvaloniaUI auto-detects X11 vs Wayland (via `Avalonia.X11`).
- Keychain: libsecret (D-Bus `org.freedesktop.secrets`) → encrypted file at `~/.config/spotdesk/keystore`.
- Device ID: `/etc/machine-id`.
- Package as AppImage for first release.

---

## Design Constraints

- **No reflection, no dynamic dispatch** — all types must be NativeAOT safe. JSON via source-gen `JsonSerializerContext`.
- **All ViewModels**: `CommunityToolkit.Mvvm` with source gen (`[ObservableProperty]`, `[RelayCommand]`).
- **Transitions**: 120ms ease-out. Never animate what the user is actively controlling.
- **No spinner for anything under 200ms.**
- Tab switch <50ms; vault unlock after re-derive <100ms.
- Status dot colors: `StatusConnected=#22C55E`, `StatusConnecting=#F59E0B`, `StatusError=#EF4444`, `StatusIdle=#6B7280`.

---

## Key Packages

| Package | Purpose |
|---|---|
| `Avalonia`, `Avalonia.X11` | UI framework |
| `CommunityToolkit.Mvvm` | ViewModels (source-gen, AOT-safe) |
| `Konscious.Security.Cryptography.Argon2` | Argon2id KDF |
| `SSH.NET` | SSH transport |
| `LibGit2Sharp` | Git sync |
| `RemoteViewing` | VNC |
| `xunit`, `NSubstitute` | Tests |

---

## Vault File Safety

`vault.json` is **safe to commit to Git**. It contains only AES-256-GCM ciphertext.
What never touches Git: OAuth token, derived device key, plaintext master key.
Device approval flow: trusted device re-encrypts masterKey with new device's derived key, pushes new `DeviceEnvelope`.
