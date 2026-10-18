using Store.Domain.Entities;

namespace Store.Application.Abstractions;

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public interface IPasswordHasher
{
    string Hash(User user, string password);
    PasswordCheck Verify(User user, string password);
}
