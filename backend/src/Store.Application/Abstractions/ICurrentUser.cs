using Store.Domain.Enums;

namespace Store.Application.Abstractions;

public interface ICurrentUser
{
    int UserId { get; }
    UserRole Role { get; }
    bool IsAdmin => Role == UserRole.Admin;
}
