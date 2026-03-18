using SpotDesk.Core.Auth;

namespace SpotDesk.Core.Tests.TestHelpers;

/// <summary>Shared in-memory keychain for unit tests that need a real store/retrieve/delete cycle.</summary>
public sealed class InMemoryKeychainService : IKeychainService
{
    private readonly Dictionary<string, string> _store = new();

    public void   Store(string key, string value) => _store[key] = value;
    public string? Retrieve(string key) => _store.TryGetValue(key, out var v) ? v : null;
    public void   Delete(string key) => _store.Remove(key);
}
