using Microsoft.AspNetCore.Identity;
using PqrsSaas.Domain.Entities;

namespace PqrsSaas.Infrastructure.Security;

/// <summary>
/// Envoltorio delgado sobre PasswordHasher<T> de ASP.NET Core Identity.
/// No se usa el sistema de Identity completo (UserManager, etc.) — solo
/// el algoritmo de hashing, para no meter la complejidad de Identity
/// completo en un proyecto con este presupuesto de tiempo.
/// Se usan hashers separados por tipo para que un hash de agente no sirva
/// para un superadmin (y viceversa).
/// </summary>
public class PasswordService
{
    private readonly PasswordHasher<User> _userHasher = new();
    private readonly PasswordHasher<SuperAdmin> _superAdminHasher = new();

    public string Hash(User user, string plainPassword) => _userHasher.HashPassword(user, plainPassword);

    public bool Verify(User user, string plainPassword)
    {
        var result = _userHasher.VerifyHashedPassword(user, user.PasswordHash, plainPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    public string Hash(SuperAdmin superAdmin, string plainPassword) => _superAdminHasher.HashPassword(superAdmin, plainPassword);

    public bool Verify(SuperAdmin superAdmin, string plainPassword)
    {
        var result = _superAdminHasher.VerifyHashedPassword(superAdmin, superAdmin.PasswordHash, plainPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
