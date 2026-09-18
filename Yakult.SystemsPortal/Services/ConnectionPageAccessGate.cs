using System.Security.Cryptography;
using System.Text;
using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

/// <summary>
/// Verifies and configures the dedicated connection-page passphrase from the
/// encrypted local portal state. It is deliberately independent of the active
/// SQL database so switching databases cannot lock the admin out.
/// </summary>
public interface IConnectionPageAccessGate
{
    Task<ConnectionPageAccessState> GetStateAsync();
    Task<bool> VerifyAsync(string passphrase);
    Task ConfigureAsync(string passphrase, int? modifiedByUserId);
}

public sealed class ConnectionPageAccessGate : IConnectionPageAccessGate
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private readonly IProtectedPortalStateStore _stateStore;

    public ConnectionPageAccessGate(IProtectedPortalStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public Task<ConnectionPageAccessState> GetStateAsync()
    {
        var passphrase = _stateStore.Load().ConnectionPassphrase;
        return Task.FromResult(new ConnectionPageAccessState
        {
            IsStorageAvailable = true,
            IsConfigured = passphrase != null &&
                          passphrase.Version == 1 &&
                          passphrase.Iterations >= 100_000 &&
                          !string.IsNullOrWhiteSpace(passphrase.Salt) &&
                          !string.IsNullOrWhiteSpace(passphrase.Hash)
        });
    }

    public Task<bool> VerifyAsync(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            return Task.FromResult(false);

        var stored = _stateStore.Load().ConnectionPassphrase;
        if (stored == null || stored.Version != 1 || stored.Iterations < 100_000)
            return Task.FromResult(false);

        try
        {
            var salt = Convert.FromBase64String(stored.Salt);
            var expectedHash = Convert.FromBase64String(stored.Hash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                passphrase,
                salt,
                stored.Iterations,
                HashAlgorithmName.SHA512,
                expectedHash.Length);
            return Task.FromResult(CryptographicOperations.FixedTimeEquals(actualHash, expectedHash));
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }
    }

    public Task ConfigureAsync(string passphrase, int? modifiedByUserId)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("A passphrase is required.", nameof(passphrase));

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            Iterations,
            HashAlgorithmName.SHA512,
            HashBytes);

        var state = _stateStore.Load();
        state.ConnectionPassphrase = new ConnectionPassphraseState
        {
            Version = 1,
            Iterations = Iterations,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash)
        };
        _stateStore.Save(state);
        return Task.CompletedTask;
    }
}
