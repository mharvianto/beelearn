using BeeLearn.Models;
using Microsoft.AspNetCore.Identity;

namespace BeeLearn.Services;

/// <summary>Thin wrapper around ASP.NET's PBKDF2 password hasher.</summary>
public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
