# SpotDesk — Automated Testing Guide
> Self-healing vibe coding loop: write → run → verify → fix → repeat until green

---

## Table of Contents
1. Philosophy — the self-healing loop
2. Test stack setup
3. The master test runner script
4. Per-milestone test contracts
5. Self-healing prompt templates
6. CI/CD pipeline (GitHub Actions)
7. Test coverage targets
8. Debugging recipes

---

## 1. Philosophy — the self-healing loop

The goal is a prompt pattern where Claude:
1. **Writes** the implementation code
2. **Runs** the tests via `dotnet test`
3. **Reads** the failure output
4. **Diagnoses** the root cause
5. **Fixes** only the failing code
6. **Re-runs** until all tests are green
7. **Reports** a final green summary

This loop runs entirely without human input between steps. You paste one prompt, come back to a green build.

```
YOU paste prompt
      │
      ▼
Claude writes code + tests
      │
      ▼
Claude runs: dotnet test --logger "console;verbosity=detailed"
      │
      ├─ ALL GREEN ──→ Claude reports summary, stops
      │
      └─ FAILURES ──→ Claude reads output
                            │
                            ▼
                      Claude identifies root cause
                      (compile error / logic error / missing mock / wrong assertion)
                            │
                            ▼
                      Claude edits ONLY failing file(s)
                            │
                            ▼
                      Claude re-runs dotnet test
                            │
                      (loop, max 5 iterations)
```

**Hard rules for Claude during the loop:**
- Never skip a failing test — fix it or explain exactly why it cannot be fixed
- Never comment out assertions to make tests pass
- Never change test expectations to match wrong implementation — fix the implementation
- Never add `[Ignore]` or `Skip` without a written justification
- After 5 failed iterations, stop and output a diagnostic report instead of guessing

---

## 2. Test Stack Setup

### 2.1 Packages (add to all test projects)

```xml
<!-- SpotDesk.Core.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../SpotDesk.Core/SpotDesk.Core.csproj"/>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk"          Version="17.*"/>
    <PackageReference Include="xunit"                           Version="2.*"/>
    <PackageReference Include="xunit.runner.visualstudio"       Version="3.*"/>
    <PackageReference Include="NSubstitute"                     Version="5.*"/>
    <PackageReference Include="FluentAssertions"                Version="7.*"/>
    <PackageReference Include="Bogus"                           Version="35.*"/>
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.*"/>
  </ItemGroup>
</Project>
```

### 2.2 Shared test utilities

```csharp
// TestHelpers/VaultFixture.cs
// Provides a fresh in-memory vault + mock keychain for every test class
public sealed class VaultFixture : IDisposable
{
    public string VaultPath { get; } = Path.Combine(
        Path.GetTempPath(), $"spotdesk-test-{Guid.NewGuid():N}");

    public IKeychainService Keychain { get; } = Substitute.For<IKeychainService>();
    public IDeviceIdService DeviceId { get; } = Substitute.For<IDeviceIdService>();
    public IKeyDerivationService KeyDerivation { get; } = new KeyDerivationService();

    public VaultFixture()
    {
        Directory.CreateDirectory(VaultPath);
        DeviceId.GetDeviceId().Returns("test-device-abc123");
    }

    public void Dispose() => Directory.Delete(VaultPath, recursive: true);
}

// TestHelpers/FakeIdentity.cs
public static class FakeIdentity
{
    public static GitHubIdentity GitHub(long userId = 99999L, string login = "testuser") =>
        new(userId, login, "ghp_fake_token_for_testing");
}
```

### 2.3 Run all tests command

```bash
dotnet test SpotDesk.sln \
  --configuration Debug \
  --logger "console;verbosity=detailed" \
  --logger "trx;LogFileName=results.trx" \
  --results-directory ./TestResults \
  -- xunit.parallelizeAssembly=true
```

---

## 3. The Master Test Runner Script

Save this as `scripts/test-loop.sh` (or `test-loop.ps1` on Windows).
Claude should call this script and parse its output during the self-healing loop.

### 3.1 Bash version (macOS / Linux)

```bash
#!/usr/bin/env bash
# scripts/test-loop.sh
# Usage: ./scripts/test-loop.sh [--milestone M1]

set -euo pipefail

FILTER=${1:-""}
MAX_ITERATIONS=5
ITERATION=0
PASS=0

run_tests() {
  local filter_arg=""
  if [[ -n "$FILTER" ]]; then
    filter_arg="--filter Category=$FILTER"
  fi

  dotnet test SpotDesk.sln \
    --configuration Debug \
    --logger "console;verbosity=detailed" \
    --no-build \
    $filter_arg \
    2>&1
}

# Build once upfront
echo "=== Building solution ==="
dotnet build SpotDesk.sln --configuration Debug 2>&1
if [[ $? -ne 0 ]]; then
  echo "BUILD FAILED — fix compile errors before running tests"
  exit 1
fi

while [[ $ITERATION -lt $MAX_ITERATIONS ]]; do
  ITERATION=$((ITERATION + 1))
  echo ""
  echo "=== Test run $ITERATION / $MAX_ITERATIONS ==="

  OUTPUT=$(run_tests)
  echo "$OUTPUT"

  if echo "$OUTPUT" | grep -q "Failed: 0"; then
    PASS=1
    break
  fi

  echo ""
  echo "=== Failures detected — Claude should read above and fix ==="
done

if [[ $PASS -eq 1 ]]; then
  echo ""
  echo "✓ ALL TESTS PASSED on iteration $ITERATION"
  exit 0
else
  echo ""
  echo "✗ Still failing after $MAX_ITERATIONS iterations — diagnostic report needed"
  exit 1
fi
```

### 3.2 PowerShell version (Windows)

```powershell
# scripts/test-loop.ps1
param([string]$Milestone = "")

$MaxIterations = 5
$Iteration = 0
$Pass = $false

function Run-Tests {
    $filter = if ($Milestone) { "--filter Category=$Milestone" } else { "" }
    $cmd = "dotnet test SpotDesk.sln --configuration Debug --logger `"console;verbosity=detailed`" --no-build $filter"
    Invoke-Expression $cmd
    return $LASTEXITCODE
}

Write-Host "=== Building solution ==="
dotnet build SpotDesk.sln --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED — fix compile errors before running tests"
    exit 1
}

while ($Iteration -lt $MaxIterations) {
    $Iteration++
    Write-Host "`n=== Test run $Iteration / $MaxIterations ==="
    $exit = Run-Tests
    if ($exit -eq 0) { $Pass = $true; break }
    Write-Host "`n=== Failures detected — Claude should read above and fix ==="
}

if ($Pass) {
    Write-Host "`n✓ ALL TESTS PASSED on iteration $Iteration"
    exit 0
} else {
    Write-Host "`n✗ Still failing after $MaxIterations iterations"
    exit 1
}
```

---

## 4. Per-Milestone Test Contracts

These define exactly what tests MUST be green before a milestone is considered done.
Use `[Trait("Category", "M1")]` etc. so tests can be filtered per milestone.

---

### M1 — Vault + Crypto + Auth

```
MUST PASS:

VaultCrypto
  [M1] EncryptEntry_ThenDecrypt_RoundTrip
  [M1] DecryptEntry_WrongMasterKey_ThrowsAuthTag
  [M1] DecryptEntry_TamperedCiphertext_ThrowsAuthTag
  [M1] GenerateMasterKey_Returns32Bytes
  [M1] GenerateMasterKey_CalledTwice_ReturnsDifferentValues
  [M1] EncryptMasterKey_ThenDecrypt_RoundTrip
  [M1] DecryptMasterKey_WrongDeviceKey_ThrowsAuthTag

KeyDerivation
  [M1] DeriveDeviceKey_SameInputs_DeterministicOutput
  [M1] DeriveDeviceKey_DifferentDeviceId_DifferentKey
  [M1] DeriveDeviceKey_DifferentUserId_DifferentKey
  [M1] DeriveDeviceKey_Returns32Bytes

DeviceIdService
  [M1] GetDeviceId_ReturnsNonEmptyHexString
  [M1] GetDeviceId_CalledTwice_ReturnsSameValue
  [M1] GetDeviceId_Format_Is64CharHex

KeychainService (mock)
  [M1] Store_ThenRetrieve_ReturnsSameValue
  [M1] Retrieve_NonExistentKey_ReturnsNull
  [M1] Delete_ExistingKey_RetrieveReturnsNull

VaultModel (serialization)
  [M1] VaultFile_SerializeDeserialize_PreservesAllFields
  [M1] DeviceEnvelope_SerializeDeserialize_PreservesAllFields
  [M1] VaultEntry_SerializeDeserialize_PreservesAllFields

VaultService
  [M1] UnlockAsync_WithValidToken_ReturnsSuccess
  [M1] UnlockAsync_NoKeychainToken_ReturnsNeedsOAuth
  [M1] UnlockAsync_NoMatchingEnvelope_ReturnsNeedsDeviceApproval
  [M1] FirstTimeSetupAsync_CreatesVaultFile
  [M1] FirstTimeSetupAsync_VaultHasOneDeviceEnvelope
  [M1] AddDeviceAsync_NewEnvelopeDecryptable
  [M1] RevokeDeviceAsync_EnvelopeRemovedFromVault
  [M1] AddEntry_ThenGetAll_ReturnsEntry
  [M1] UpdateEntry_ChangesPayload
  [M1] RemoveEntry_EntryNotInGetAll

SessionLockService
  [M1] GetMasterKey_WhenLocked_ThrowsInvalidOperation
  [M1] Lock_ZeroesMemory
  [M1] SetMasterKey_ThenGetMasterKey_ReturnsCorrectBytes
  [M1] IsUnlocked_AfterLock_ReturnsFalse

MasterPasswordFallback
  [M1] DeriveKeyFromPassword_SameInputs_DeterministicOutput
  [M1] DeriveKeyFromPassword_DifferentPasswords_DifferentKeys
  [M1] DeriveKeyFromPassword_Returns32Bytes
```

---

### M2 — Git Sync

```
MUST PASS:

GitSyncService
  [M2] Clone_NewRepo_CreatesLocalDirectory
  [M2] Pull_BehindRemote_FastForwards
  [M2] CommitAndPush_AfterVaultSave_CreatesCommit
  [M2] CommitAndPush_CommitMessage_ContainsTimestamp
  [M2] Offline_QueuesSyncAndRetries
  [M2] Offline_ExponentialBackoff_WaitsCorrectDuration

ConflictResolver
  [M2] Resolve_TwoVersions_KeepsNewerByUpdatedAt
  [M2] Resolve_SameUpdatedAt_KeepsCurrentDevice
  [M2] Resolve_NewEntryOnRemote_MergesIn
  [M2] Resolve_DeletedOnRemote_RemovesLocally
```

---

### M3 — RDM Importer

```
MUST PASS:

DevolutionsImporter
  [M3] Import_UnencryptedRdm_ParsesConnections
  [M3] Import_UnencryptedRdm_ParsesCredentials
  [M3] Import_EncryptedRdm_WrongKey_ReturnsError
  [M3] Import_EncryptedRdm_CorrectKey_ParsesConnections
  [M3] Import_MalformedXml_ReturnsParseError
  [M3] Import_RdpTypeCode1_MapsToRdpProtocol
  [M3] Import_SshTypeCode66_MapsToSshProtocol
  [M3] Import_VncTypeCode12_MapsToVncProtocol
  [M3] Import_GroupHierarchy_PreservesNesting

RdpFileImporter
  [M3] Import_StandardRdpFile_ParsesHostAndPort
  [M3] Import_StandardRdpFile_ParsesUsername
  [M3] Import_StandardRdpFile_ParsesResolution
  [M3] Import_EmptyFile_ReturnsError
  [M3] Import_MissingHost_ReturnsError
```

---

### M4 — AvaloniaUI Shell

```
MUST PASS (headless UI tests via Avalonia.Headless.XUnit):

MainWindowViewModel
  [M4] ViewModel_InitialState_NoActiveSessions
  [M4] ViewModel_AddSession_TabAppears
  [M4] ViewModel_CloseTab_TabRemoved
  [M4] ViewModel_SwitchTab_UpdatesActiveSession

ConnectionTreeViewModel
  [M4] Groups_Load_PopulatesFromVault
  [M4] Search_EmptyQuery_ShowsAllConnections
  [M4] Search_MatchingQuery_FiltersResults
  [M4] Search_NoMatch_ShowsEmptyState
  [M4] AddGroup_AppearsInTree
  [M4] RemoveConnection_DisappearsFromTree

ThemeService
  [M4] SetTheme_Dark_UpdatesRequestedThemeVariant
  [M4] SetTheme_Light_UpdatesRequestedThemeVariant
  [M4] SetTheme_System_UsesDefault
```

---

### M5 — SSH Terminal

```
MUST PASS:

Vt100Parser
  [M5] Parse_PlainText_WritesToBuffer
  [M5] Parse_CursorMoveEscape_UpdatesCursorPosition
  [M5] Parse_ClearScreenEscape_ClearsBuffer
  [M5] Parse_256ColorEscape_SetsCorrectColorIndex
  [M5] Parse_BoldEscape_SetsBoldAttribute
  [M5] Parse_UrlSequence_DetectsHyperlink

TerminalBuffer
  [M5] Buffer_WriteAtPosition_CorrectCell
  [M5] Buffer_ScrollDown_ShiftsLines
  [M5] Buffer_Scrollback_RetainsLines
  [M5] Buffer_SelectionRange_ReturnsCorrectText
  [M5] Buffer_Clear_EmptiesVisible

SshSession (integration, mocked server)
  [M5] Connect_ValidCredentials_EstablishesSession
  [M5] Connect_WrongPassword_ThrowsAuthException
  [M5] Connect_PrivateKey_Authenticates
  [M5] SendInput_EchoedInBuffer
  [M5] Disconnect_SessionEnds
```

---

### M6 — RDP Windows

```
MUST PASS (Windows only, [SupportedOSPlatform("windows")]):

WindowsRdpBackend
  [M6] Backend_Create_DoesNotThrow
  [M6] Session_Connect_FiresConnectedEvent
  [M6] Session_GetFrameBuffer_ReturnsNonNullBitmap
  [M6] Session_Resize_UpdatesResolution
  [M6] Session_Disconnect_FiresDisconnectedEvent
  [M6] Session_AutoReconnect_TriggersOnUnexpectedDisconnect
```

---

### M7 — RDP macOS + Linux

```
MUST PASS:

FreeRdpBackend
  [M7] Native_LibraryLoads_DoesNotThrow
  [M7] Backend_Create_DoesNotThrow
  [M7] Session_Connect_FiresConnectedEvent
  [M7] Session_GetFrameBuffer_ReturnsNonNullBitmap
  [M7] Session_Disconnect_FiresDisconnectedEvent
  [M7] LibraryName_OnMacOs_ContainsDylib
  [M7] LibraryName_OnLinux_ContainsSo3
```

---

### M8 — Connection Tree + Search

```
MUST PASS:

ConnectionTreeViewModel (extended)
  [M8] QuickConnect_ByPort3389_DefaultsToRdp
  [M8] QuickConnect_ByPort22_DefaultsToSsh
  [M8] QuickConnect_ByPort5900_DefaultsToVnc
  [M8] DragDrop_MovesConnectionBetweenGroups
  [M8] InlineRename_SavesNewName
  [M8] BatchDelete_RemovesAllSelected
  [M8] Sort_Alphabetical_CorrectOrder
  [M8] Sort_LastConnected_CorrectOrder
  [M8] PinToFavorites_AppearsInFavoritesGroup
```

---

### M9 — Tab Session Manager

```
MUST PASS:

SessionManager
  [M9] GetOrCreate_SameEntry_ReturnsSameInstance
  [M9] GetOrCreate_DifferentEntry_ReturnsDifferentInstance
  [M9] TabSwitch_DoesNotReconnect
  [M9] Close_ExplicitOnly_DisposesSession
  [M9] PrefetchDns_ResolvesAllHosts
  [M9] TcpPrewarm_CancelledIfUnused
  [M9] AutoReconnect_CountdownThenReconnects
  [M9] AutoReconnect_UserCancels_StopsRetry
  [M9] Latency_Green_Below50ms
  [M9] Latency_Amber_50to150ms
  [M9] Latency_Red_Above150ms
```

---

### M10 — Settings + Sync UI

```
MUST PASS:

SettingsViewModel
  [M10] OAuthConnected_ShowsIdentityRow
  [M10] OAuthNotConnected_ShowsConnectButton
  [M10] LockNow_CallsSessionLockService
  [M10] LockOnScreenLock_Toggle_SavesPreference
  [M10] SyncNow_CallsGitSyncService
  [M10] AutoSyncInterval_Change_SavesPreference
  [M10] TrustedDevices_LoadsFromVaultDevices
  [M10] ApproveDevice_CallsAddDeviceAsync
  [M10] RevokeDevice_CallsRevokeDeviceAsync
  [M10] SwitchToMasterPassword_ShowsFallbackDialog
```

---

### M11 — Import Wizard

```
MUST PASS:

ImportWizardViewModel
  [M11] Step1_DetectRdmFormat_ShowsRdmLabel
  [M11] Step1_DetectRdpFormat_ShowsRdpLabel
  [M11] Step1_UnknownFormat_ShowsError
  [M11] Step2_EncryptedRdm_ShowsKeyField
  [M11] Step2_UnencryptedRdm_SkipsKeyField
  [M11] Step2_WrongKey_ShowsClearErrorMessage
  [M11] Step2_SelectAll_ChecksAllEntries
  [M11] Step3_GroupMapping_PreservesHierarchy
  [M11] Step3_ConflictSkip_SkipsDuplicate
  [M11] Step3_ConflictRename_RenamesEntry
  [M11] Import_OnComplete_EncryptsWithSpotDeskKey
  [M11] Import_OnComplete_PushesToGit
  [M11] Import_ProgressEvents_FiredPerEntry
```

---

## 5. Self-Healing Prompt Templates

Copy and paste these directly into Claude. Each is a complete, standalone prompt for a specific milestone that enforces the test loop.

---

### Template — Generic self-healing loop

```
You are building [FEATURE] for SpotDesk (.NET 10, C#13, NativeAOT-compatible).

Follow this exact loop — do not skip any step:

STEP 1 — WRITE
Write the implementation and all tests listed in the test contract below.
Place tests in SpotDesk.Core.Tests/ (or SpotDesk.UI.Tests/ for UI).
Tag every test with [Trait("Category", "M[N]")].

STEP 2 — BUILD
Run: dotnet build SpotDesk.sln --configuration Debug
If build fails: read ALL compiler errors, fix them, re-run build.
Do not proceed to test until build is clean.

STEP 3 — TEST
Run: dotnet test SpotDesk.sln --filter Category=M[N] --logger "console;verbosity=detailed"

STEP 4 — READ OUTPUT
Read every line of test output. For each failure note:
- Test name
- Expected vs actual values
- Stack trace file + line number
- Whether it is a logic error, missing mock setup, or wrong assertion

STEP 5 — FIX
Fix only the root cause. Rules:
- Never comment out assertions
- Never add [Skip] without written justification
- Never change test expectations to match wrong implementation
- Fix the implementation, not the test

STEP 6 — RE-RUN
Go back to STEP 3. Repeat until all tests pass.
Maximum 5 iterations. If still failing after 5, output a diagnostic report.

STEP 7 — REPORT
When all green, output:
  ✓ [N] tests passed
  ✓ 0 failed
  ✓ Build: clean
  List every file created or modified.

[PASTE TEST CONTRACT HERE]
```

---

### Template — Milestone 1 (Vault + Crypto + Auth)

```
You are implementing Milestone 1 for SpotDesk. Follow the self-healing loop exactly.

CONTEXT: SpotDesk uses an OAuth-derived vault key. No master password by default.
The vault AES-256-GCM masterKey is encrypted per-device using a key derived from
(GitHub userId + deviceId) via Argon2id. See Section 10 of the blueprint.

STEP 1 — WRITE IMPLEMENTATION

Files to create in SpotDesk.Core/:

Crypto/VaultCrypto.cs
  - GenerateMasterKey() → byte[32]  (RandomNumberGenerator.GetBytes)
  - EncryptEntry(payload: string, masterKey: byte[]) → (byte[] ciphertext, byte[] iv)
  - DecryptEntry(ciphertext: byte[], iv: byte[], masterKey: byte[]) → string
  - EncryptMasterKey(masterKey: byte[], deviceKey: byte[]) → (byte[] ciphertext, byte[] iv)
  - DecryptMasterKey(ciphertext: byte[], iv: byte[], deviceKey: byte[]) → byte[]
  - All use System.Security.Cryptography.AesGcm, tag size 16 bytes
  - All AOT-safe — no reflection

Crypto/KeyDerivationService.cs : IKeyDerivationService
  - DeriveDeviceKey(githubUserId: long, deviceId: string) → byte[32]
  - Argon2id: Konscious.Security.Cryptography.Argon2
    iterations=3, memory=65536, parallelism=4
  - input = UTF8($"{githubUserId}:{deviceId}")
  - salt = UTF8("spotdesk-device-key-v1")

Crypto/DeviceIdService.cs : IDeviceIdService
  - GetDeviceId() → string
  - Linux: File.ReadAllText("/etc/machine-id").Trim()
  - macOS: IOKit IOPlatformSerialNumber via LibraryImport
  - Windows: Registry HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
  - Hash: Convert.ToHexString(SHA256.HashData(UTF8(raw + "spotdesk-v1")))
  - Cache result after first call

Vault/VaultModel.cs
  - All records, System.Text.Json source-gen via [JsonSerializable] context
  - record VaultFile { int Version, string Kdf, string? Salt,
      DeviceEnvelope[] Devices, VaultEntry[] Entries }
  - record DeviceEnvelope { string DeviceId, string DeviceName,
      string EncryptedMasterKey, string IV, DateTimeOffset AddedAt }
  - record VaultEntry { Guid Id, string IV, string Ciphertext }

Auth/IKeychainService.cs + KeychainService.cs
  - Interface: Store(key, value), Retrieve(key) → string?, Delete(key)
  - WindowsKeychainService: CredWrite/CredRead P/Invoke
  - MacOsKeychainService: SecKeychainAddGenericPassword P/Invoke
  - LinuxKeychainService: try libsecret D-Bus, fallback to encrypted file
    at ~/.config/spotdesk/keystore using SHA256(/etc/machine-id) as file key

Auth/OAuthService.cs : IOAuthService (stub — full browser flow not needed for unit tests)
  - AuthenticateGitHubAsync() → GitHubIdentity
  - IsAuthenticatedAsync(provider) → bool
  - RevokeAsync(provider)
  - GitHubIdentity record { long UserId, string Login, string AccessToken }

Vault/SessionLockService.cs : ISessionLockService
  - SetMasterKey(key: byte[]) — pins in GCHandle.Alloc(Pinned)
  - GetMasterKey() → ReadOnlySpan<byte> — throws InvalidOperationException if locked
  - Lock() — zeroes pinned memory, frees handle
  - IsUnlocked → bool

Vault/VaultService.cs : IVaultService
  - UnlockAsync(vaultPath) → UnlockResult enum
      { Success, NeedsOAuth, NeedsDeviceApproval, Failed }
  - FirstTimeSetupAsync(identity, vaultPath, repoUrl)
  - AddDeviceAsync(newDeviceId, newDeviceName)
  - RevokeDeviceAsync(deviceId)
  - AddEntryAsync(payload) → VaultEntry
  - UpdateEntryAsync(id, payload) → VaultEntry
  - RemoveEntryAsync(id)
  - GetAllEntriesAsync() → IReadOnlyList<(Guid Id, string Payload)>

Vault/MasterPasswordFallback.cs
  - DeriveKeyFromPassword(password: string, salt: byte[]) → byte[32]
  - Same Argon2id params, salt random 32 bytes stored in VaultFile.Salt

STEP 2 — WRITE TESTS

File: SpotDesk.Core.Tests/M1_VaultCryptoTests.cs
All tests tagged [Trait("Category", "M1")]

Write all tests from this contract:

  VaultCrypto:
  - EncryptEntry_ThenDecrypt_RoundTrip: encrypt "hello world", decrypt, assert equal
  - DecryptEntry_WrongMasterKey_ThrowsAuthTag: encrypt with key A, decrypt with key B
  - DecryptEntry_TamperedCiphertext_ThrowsAuthTag: flip one byte in ciphertext
  - GenerateMasterKey_Returns32Bytes
  - GenerateMasterKey_CalledTwice_ReturnsDifferentValues
  - EncryptMasterKey_ThenDecrypt_RoundTrip
  - DecryptMasterKey_WrongDeviceKey_ThrowsAuthTag

  KeyDerivation:
  - DeriveDeviceKey_SameInputs_DeterministicOutput
  - DeriveDeviceKey_DifferentDeviceId_DifferentKey
  - DeriveDeviceKey_DifferentUserId_DifferentKey
  - DeriveDeviceKey_Returns32Bytes

  DeviceIdService (mock OS calls via IDeviceIdService interface):
  - GetDeviceId_ReturnsNonEmptyHexString
  - GetDeviceId_CalledTwice_ReturnsSameValue
  - GetDeviceId_Format_Is64CharHex

  VaultModel serialization:
  - VaultFile_SerializeDeserialize_PreservesAllFields
  - DeviceEnvelope_SerializeDeserialize_PreservesAllFields
  - VaultEntry_SerializeDeserialize_PreservesAllFields

  VaultService (IKeychainService and IDeviceIdService mocked via NSubstitute):
  - UnlockAsync_WithValidToken_ReturnsSuccess
  - UnlockAsync_NoKeychainToken_ReturnsNeedsOAuth
  - UnlockAsync_NoMatchingEnvelope_ReturnsNeedsDeviceApproval
  - FirstTimeSetupAsync_CreatesVaultFile
  - FirstTimeSetupAsync_VaultHasOneDeviceEnvelope
  - AddDeviceAsync_NewEnvelopeDecryptable
  - RevokeDeviceAsync_EnvelopeRemovedFromVault
  - AddEntry_ThenGetAll_ReturnsEntry
  - UpdateEntry_ChangesPayload
  - RemoveEntry_EntryNotInGetAll

  SessionLockService:
  - GetMasterKey_WhenLocked_ThrowsInvalidOperation
  - Lock_ZeroesMemory
  - SetMasterKey_ThenGetMasterKey_ReturnsCorrectBytes
  - IsUnlocked_AfterLock_ReturnsFalse

  MasterPasswordFallback:
  - DeriveKeyFromPassword_SameInputs_DeterministicOutput
  - DeriveKeyFromPassword_DifferentPasswords_DifferentKeys
  - DeriveKeyFromPassword_Returns32Bytes

STEP 3 — BUILD AND RUN
dotnet build SpotDesk.sln --configuration Debug
dotnet test SpotDesk.sln --filter Category=M1 --logger "console;verbosity=detailed"

STEP 4–6 — SELF-HEAL
Read all failures. Fix root cause only. Re-run. Max 5 iterations.

STEP 7 — REPORT
When green: list all files created, test count, confirm 0 failures.
```

---

### Template — Milestone 2 (Git Sync)

```
You are implementing Milestone 2 (Git Sync) for SpotDesk. Self-healing loop applies.
Milestone 1 is already complete and passing.

IMPLEMENT:
- GitSyncService.cs : IGitSyncService using LibGit2Sharp
  - CloneAsync(repoUrl, localPath, accessToken)
  - PullAsync(localPath) — fast-forward only, no merge
  - CommitAndPushAsync(localPath, vaultJson, accessToken)
    commit message: $"spotdesk: sync {DateTimeOffset.UtcNow:O}"
  - ISyncStatus events: SyncStarted, SyncCompleted, SyncFailed, ConflictResolved

- ConflictResolver.cs
  - Resolve(localVault: VaultFile, remoteVault: VaultFile) → VaultFile
    Keep newer VaultEntry per Id by comparing updatedAt in decrypted payload JSON
    Keep all DeviceEnvelopes from both (union, deduplicated by DeviceId)

WRITE TESTS in SpotDesk.Core.Tests/M2_GitSyncTests.cs [Trait("Category","M2")]:
  - Clone_NewRepo_CreatesLocalDirectory (use temp dir + bare repo)
  - Pull_BehindRemote_FastForwards
  - CommitAndPush_AfterVaultSave_CreatesCommit
  - CommitAndPush_CommitMessage_ContainsTimestamp
  - Offline_QueuesSyncAndRetries (mock network failure, verify retry)
  - Offline_ExponentialBackoff_WaitsCorrectDuration (use FakeTimeProvider)
  - ConflictResolver_KeepsNewerByUpdatedAt
  - ConflictResolver_SameUpdatedAt_KeepsCurrent
  - ConflictResolver_NewEntryOnRemote_MergesIn
  - ConflictResolver_DeletedOnRemote_RemovesLocally

BUILD → TEST → FIX loop. Report when green.
```

---

### Template — Milestone 3 (RDM Importer)

```
You are implementing Milestone 3 (RDM Importer) for SpotDesk. Self-healing loop applies.

IMPLEMENT in SpotDesk.Core/Import/:
- DevolutionsImporter.cs : IDevolutionsImporter
  ParseAsync(filePath, rdmMasterKey: string?) → ImportResult
  The .rdm file is XML. Root: <DataEntries>. Children: <Connection>.
  Fields per Connection: Name, Host, Port, ConnectionType (1=RDP, 66=SSH, 12=VNC),
  UserName, Password (may be base64), Group, Tags, Description
  If rdmMasterKey provided: AES decrypt file before XML parsing
  
- RdpFileImporter.cs : IRdpFileImporter
  ParseAsync(filePath) → ImportResult
  Parse key:type:value lines. Extract:
    full address → Host + Port
    username → Username
    desktopwidth + desktopheight → Resolution
    session bpp → ColorDepth
    
- ImportResult.cs
  record ImportResult { ConnectionEntry[] Connections, CredentialEntry[] Credentials,
    string[] Warnings, ImportError[] Errors }
  record ImportError { string EntryName, string Message, ErrorKind Kind }
  enum ErrorKind { ParseError, DecryptionFailed, MissingRequired, Duplicate }

CREATE TEST FIXTURES in SpotDesk.Core.Tests/Fixtures/:
  - sample_unencrypted.rdm — minimal valid XML with 3 connections (RDP, SSH, VNC)
  - sample.rdp — standard Windows RDP file
  Embed as EmbeddedResource in test project.

WRITE TESTS in SpotDesk.Core.Tests/M3_ImportTests.cs [Trait("Category","M3")]:
All contracts from Section 4 of this guide.

BUILD → TEST → FIX loop. Report when green.
```

---

### Template — Integration test (cross-milestone)

```
You are writing an integration test that spans Milestones 1 and 2 for SpotDesk.
All individual milestone tests are already green.

Write SpotDesk.Core.Tests/Integration/VaultSyncIntegrationTests.cs
[Trait("Category","Integration")]

Scenarios to cover end-to-end (no mocks — use real implementations with temp dirs):

1. FullRoundTrip_FirstDevice
   - OAuth mock returns userId=12345, deviceId="device-A"
   - FirstTimeSetupAsync creates vault.json in temp dir
   - Git repo initialised in another temp dir as bare remote
   - CommitAndPush pushes vault.json to remote
   - AddEntry adds a credential
   - Pull on a second temp local clone retrieves vault.json
   - UnlockAsync on "device-A" decrypts entry — assert round-trip

2. TwoDevices_BothCanDecrypt
   - Device A creates vault (userId=12345, deviceId="device-A")
   - Device B OAuth same userId=12345, deviceId="device-B"
   - Device A approves Device B via AddDeviceAsync → push
   - Device B pulls → UnlockAsync returns Success
   - Both devices decrypt same entry — assert equal payloads

3. RevokedDevice_CannotDecrypt
   - Device A creates vault, Device B approved
   - Device A revokes Device B → push
   - Device B pulls → UnlockAsync returns NeedsDeviceApproval
   
4. ConflictResolution_TwoSimultaneousEdits
   - Device A and B both clone vault at same state
   - Device A adds entry "alpha", pushes
   - Device B adds entry "beta" (without pulling), tries to push → conflict
   - ConflictResolver merges → both "alpha" and "beta" present in final vault

BUILD → TEST → FIX loop. All 4 scenarios must pass. Report when green.
```

---

## 6. CI/CD Pipeline (GitHub Actions)

Save as `.github/workflows/ci.yml` in the SpotDesk repo.

```yaml
name: SpotDesk CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test-linux:
    name: Tests (Linux / .NET 10)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install FreeRDP + libsecret
        run: sudo apt-get install -y libfreerdp3 libssh2-1 libsecret-1-0

      - name: Restore
        run: dotnet restore SpotDesk.sln

      - name: Build
        run: dotnet build SpotDesk.sln --no-restore --configuration Debug

      - name: Unit tests (Core)
        run: |
          dotnet test SpotDesk.sln \
            --no-build \
            --configuration Debug \
            --filter "Category=M1|Category=M2|Category=M3" \
            --logger "trx;LogFileName=core-results.trx" \
            --results-directory ./TestResults

      - name: Integration tests
        run: |
          dotnet test SpotDesk.sln \
            --no-build \
            --configuration Debug \
            --filter "Category=Integration" \
            --logger "trx;LogFileName=integration-results.trx" \
            --results-directory ./TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-linux
          path: TestResults/

  test-windows:
    name: Tests (Windows / .NET 10)
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore SpotDesk.sln

      - name: Build
        run: dotnet build SpotDesk.sln --no-restore --configuration Debug

      - name: All tests including Windows RDP
        run: |
          dotnet test SpotDesk.sln `
            --no-build `
            --configuration Debug `
            --logger "trx;LogFileName=windows-results.trx" `
            --results-directory ./TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-windows
          path: TestResults/

  test-macos:
    name: Tests (macOS / .NET 10)
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install FreeRDP
        run: brew install freerdp

      - name: Restore & Build
        run: |
          dotnet restore SpotDesk.sln
          dotnet build SpotDesk.sln --no-restore --configuration Debug

      - name: Tests
        run: |
          dotnet test SpotDesk.sln \
            --no-build \
            --configuration Debug \
            --logger "trx;LogFileName=macos-results.trx" \
            --results-directory ./TestResults
```

---

## 7. Test Coverage Targets

| Area | Target | Measured by |
|---|---|---|
| `SpotDesk.Core/Crypto/` | 100% | Every branch of AES-GCM, KDF |
| `SpotDesk.Core/Vault/` | 95% | All `UnlockResult` paths |
| `SpotDesk.Core/Auth/` | 90% | Keychain store/retrieve/delete |
| `SpotDesk.Core/Import/` | 90% | All format parsers |
| `SpotDesk.Core/Sync/` | 85% | Sync + conflict paths |
| `SpotDesk.UI/ViewModels/` | 80% | Via Avalonia.Headless |
| `SpotDesk.Protocols/` | 70% | Mocked backends |

Add coverage reporting to the test run:

```bash
dotnet test SpotDesk.sln \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"./TestResults/**/coverage.cobertura.xml" \
  -targetdir:"./TestResults/CoverageReport" \
  -reporttypes:Html
```

---

## 8. Debugging Recipes

### "Test passes locally, fails in CI"
```
Likely cause: OS-specific path separator or missing native lib.
Ask Claude:
"The test [TestName] passes locally on Windows but fails on GitHub Actions Linux.
The error is: [paste error]. The test does: [describe].
Fix for cross-platform compatibility."
```

### "AesGcm throws CryptographicException in test"
```
Likely cause: tag buffer not 16 bytes, or ciphertext modified before decryption.
Ask Claude:
"AesGcm.Decrypt throws in [TestName]. The ciphertext length is [N],
tag is [N] bytes. Error: [paste]. Show me the exact byte array sizes expected."
```

### "NSubstitute mock not matching"
```
Likely cause: Arg.Any<T>() type mismatch or method not set up.
Ask Claude:
"NSubstitute returns default instead of my configured value in [TestName].
Setup: [paste mock setup]. Call site: [paste]. Fix the mock configuration."
```

### "LibGit2Sharp test leaves temp files"
```
Likely cause: test not disposing repo handle before directory delete.
Ask Claude:
"M2 Git tests leak temp directories. The cleanup in [TestName] throws
because LibGit2Sharp still has a handle open. Fix the dispose pattern."
```

### "Argon2id is slow in tests"
```
Expected — Argon2id is intentionally slow (65536 KB memory).
For unit tests, add a test-only override:
Ask Claude:
"Argon2id makes M1 tests take 30 seconds. Add an IKeyDerivationService
test double that returns deterministic keys instantly, and wire it via
NSubstitute in all M1 tests that don't specifically test KDF timing."
```

### Diagnostic report template (when loop fails after 5 iterations)

When Claude cannot fix a test after 5 iterations, it must output:

```
DIAGNOSTIC REPORT — [TestName]

Iteration history:
  1. [what was changed] → [resulting error]
  2. [what was changed] → [resulting error]
  ...

Root cause hypothesis: [best guess at actual problem]

Blocked by: [compile constraint / missing library / OS limitation / design conflict]

Recommended next step: [specific thing the human should investigate]

Tests still failing:
  - [TestName]: [exact assertion message]
```

---

*SpotDesk Testing Guide v1.0 — Eggspot Company Limited*
*Self-healing loop: write → build → test → fix → repeat*
