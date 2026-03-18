# SpotDesk

**Cross-platform remote desktop manager** built with .NET 10 and AvaloniaUI.

Manage RDP, SSH, and VNC connections from a single app — with an encrypted local vault, optional GitHub/Bitbucket sync, and a clean dark UI that runs natively on Windows, macOS, and Linux.

---

## Features

- **RDP, SSH, VNC** — native backends per platform (AxMSTscLib on Windows, FreeRDP on macOS/Linux, SSH.NET terminal)
- **Encrypted vault** — AES-256-GCM with Argon2id key derivation. Two modes:
  - **Local mode** — password-only, no account required, works offline forever
  - **GitHub/Bitbucket sync** — vault synced as a private Git repo across your devices
- **Tabbed sessions** — tab switch < 50 ms; sessions stay alive in background
- **Global search** — fuzzy-match across all connections (Ctrl+K)
- **Group & tag connections** — organize by environment, protocol, or team
- **Import RDP files** — drag in `.rdp` files from Windows
- **Dark / Light / System theme**
- **NativeAOT on Windows** — single binary, no .NET runtime required

---

## Vault Modes

| | Local Mode | GitHub/Bitbucket Sync |
|---|---|---|
| Account required | None | GitHub or Bitbucket |
| Encrypted at rest | Yes (AES-256-GCM) | Yes (AES-256-GCM) |
| Sync across devices | No | Yes (private Git repo) |
| Works offline | Yes | Yes (cached locally) |
| First run | Set a master password | Sign in via Device Flow |

In **local mode** the vault lives at `~/.config/spotdesk/vault.json` and is unlocked only with your master password. Nothing leaves your machine.

---

## Getting Started

### Download

Grab the latest release for your platform from the [Releases](../../releases) page.

| Platform | Format |
|---|---|
| Windows | Single `.exe` (NativeAOT) |
| macOS | `.app` bundle (ReadyToRun) |
| Linux | `.AppImage` |

### Build from source

```bash
# Prerequisites: .NET 10 SDK

git clone https://github.com/your-org/spotdesk.git
cd spotdesk/src

# Restore packages
dotnet restore SpotDesk.sln

# Run (development)
dotnet run --project src/SpotDesk.App

# Run tests
dotnet test SpotDesk.sln

# Publish — single-file executable (any platform)
./scripts/publish.sh              # auto-detect platform
./scripts/publish.sh win-x64      # or specify explicitly
./scripts/publish.sh osx-arm64
./scripts/publish.sh linux-x64

# Or manually:
dotnet publish src/SpotDesk.App -r win-x64 -c Release
```

### Single-file delivery

SpotDesk publishes as a **single executable** — no installer, no runtime, no external dependencies (except FreeRDP on Linux/macOS for RDP support).

| Property | Value |
|---|---|
| `PublishSingleFile` | All managed code bundled into one binary |
| `SelfContained` | .NET runtime embedded — no install needed |
| `PublishTrimmed` | Unused code removed (~30% smaller) |
| `EnableCompressionInSingleFile` | Binary is compressed |
| `IncludeNativeLibrariesForSelfExtract` | Native libs (LibGit2Sharp's libgit2) bundled inside, extracted to temp on first launch (~5ms) |

The Git sync engine uses **LibGit2Sharp** (embedded native `libgit2`), so users don't need git installed. The native binary is bundled inside the single-file executable.

### Linux runtime dependencies

```bash
# For RDP support (FreeRDP):
sudo apt install libfreerdp3 libssh2-1 libvncserver-dev libice6 libsm6 libfontconfig1
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI | AvaloniaUI 11 |
| Language | C# 13 / .NET 10 |
| ViewModels | CommunityToolkit.Mvvm (source-gen, AOT-safe) |
| Encryption | AES-256-GCM + Argon2id (Konscious) |
| RDP (Windows) | AxMSTscLib COM interop |
| RDP (macOS/Linux) | FreeRDP 3.x (P/Invoke) |
| SSH | SSH.NET + custom VT100 renderer |
| VNC | RemoteViewing |
| Git sync | LibGit2Sharp |
| Tests | xUnit + NSubstitute + FluentAssertions + Avalonia.Headless |

---

## Project Structure

```
src/
  SpotDesk.Core/        # Domain: vault, crypto, auth, sync, import
  SpotDesk.Protocols/   # RDP / SSH / VNC session backends
  SpotDesk.UI/          # AvaloniaUI views, controls, view-models
  SpotDesk.App/         # Entry point, DI bootstrap, platform init
tests/
  SpotDesk.Core.Tests/
  SpotDesk.UI.Tests/
  SpotDesk.Protocols.Tests/
```

---

## Testing

SpotDesk has **200+ automated tests** organized by milestone, designed for a fast feedback loop when developing with AI assistance ("vibe coding").

### Test pyramid

```
        /  Headless UI Smoke (M5)  \      ← views render, selectors work
       / ViewModel Integration (M5) \     ← commands, state transitions, auth flows
      /  Domain Unit Tests (M1-M3)   \    ← crypto, sync, import, auth
     / Protocol Unit Tests (Protocols) \  ← terminal buffer, VT100
```

### Test categories

| Category | What it covers | Count |
|---|---|---|
| M1 | Vault, crypto, OAuth, keychain, key derivation | ~95 |
| M2 | Git sync, conflict resolution | ~10 |
| M3 | RDP/RDM file importers | ~13 |
| M4 | ViewModels, headless UI dialogs | ~50 |
| M5 | Integration: SettingsVM flows, SessionTab lifecycle, view selector, tab management | ~30 |
| Protocols | Terminal buffer, VT100 parser | ~5 |

### Running tests

```bash
# All tests
dotnet test SpotDesk.sln

# Single milestone
dotnet test SpotDesk.sln --filter "Category=M1"

# Multiple milestones
dotnet test SpotDesk.sln --filter "Category=M1|Category=M5"

# Self-healing test loop (build once, retry up to 5x)
./scripts/test-loop.sh                    # all tests
./scripts/test-loop.sh --milestone M1     # single milestone
.\scripts\test-loop.ps1 -Milestone M5     # PowerShell on Windows
```

### Vibe coding workflow

The test suite is designed so you can iterate with an AI assistant:

1. **Describe the change** you want
2. AI writes code + runs `dotnet test`
3. If tests fail → AI reads output → fixes → re-runs
4. Green = done. No manual clicking needed.

The `scripts/test-loop.sh` (or `.ps1`) automates this: build once, run tests up to 5 times, stop on green.

### CI

GitHub Actions runs all tests on **Linux, Windows, macOS** on every push and PR. Coverage reports are generated on Linux and uploaded as artifacts.

---

## Contributing

All contributions are welcome — bug fixes, features, documentation, translations.

1. Fork the repo and create a branch (`git checkout -b feat/my-feature`)
2. Make your changes and add tests
3. Run `dotnet test SpotDesk.sln` — all tests must pass
4. Open a pull request

There are no contributor license agreements. Code you contribute is licensed MIT.

---

## Security

Credentials are **never stored in plaintext**. The vault file (`vault.json`) contains only AES-256-GCM ciphertext and is safe to commit to a private Git repo.

What never leaves your device unencrypted:
- Passwords and SSH keys
- OAuth tokens
- The derived master key

If you find a security issue, please open a **private** GitHub Security Advisory rather than a public issue.

---

## Sponsoring

SpotDesk is free and open source (MIT). If it saves you time, consider sponsoring development:

- Click the **Sponsor** button at the top of this page (GitHub Sponsors)
- Or find us on Ko-fi / Buy Me a Coffee — links in the sidebar

Your support funds ongoing development, new platform support, and security audits.

---

## License

[MIT](LICENSE) — do whatever you want, no limitations.
