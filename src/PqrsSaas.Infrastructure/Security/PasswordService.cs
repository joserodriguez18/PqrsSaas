using Microsoft.AspNetCore.Identity;
using PqrsSaas.Domain.Entities;

namespace PqrsSaas.Infrastructure.Security;

/// <summary>
/// Envoltorio delgado sobre PasswordHasher<T> de ASP.NET Core Identity.
/// No se usa el sistema de Identity completo (UserManager, etc.) — solo
/// el algoritmo de hashing, para no meter la complejidad de Identity
/// completo en un proyecto con este presupuesto de tiempo.
/// </summary>
public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string plainPassword) => _hasher.HashPassword(user, plainPassword);

    public bool Verify(User user, string plainPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, plainPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
