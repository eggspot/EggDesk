using System.Security.Cryptography;
using System.Text;
using NSubstitute;
using SpotDesk.Core.Crypto;

namespace SpotDesk.Core.Tests.TestHelpers;

/// <summary>
/// Fast test double for IKeyDerivationService.
/// Uses SHA-256 instead of Argon2id to keep tests quick.
/// Do NOT use in tests that specifically validate KDF correctness or timing.
/// </summary>
public static class FastKeyDerivationService
{
    public static IKeyDerivationService Create()
    {
        var kdf = Substitute.For<IKeyDerivationService>();

        kdf.DeriveDeviceKey(Arg.Any<long>(), Arg.Any<string>())
           .Returns(ci =>
           {
               var input = Encoding.UTF8.GetBytes($"device:{ci.Arg<long>()}:{ci.Arg<string>()}");
               return SHA256.HashData(input);
           });

        kdf.DeriveFromPassword(Arg.Any<string>(), Arg.Any<byte[]>())
           .Returns(ci =>
           {
               var pw = Encoding.UTF8.GetBytes(ci.Arg<string>());
               var salt = ci.Arg<byte[]>();
               return SHA256.HashData([.. pw, .. salt]);
           });

        return kdf;
    }
}
