using Microsoft.EntityFrameworkCore;

namespace Store.Application.Abstractions;

/// <summary>
/// Provider specific checks on database errors, so the application layer
/// doesn't depend on SQL Server error numbers.
/// </summary>
public interface IDbExceptionClassifier
{
    bool IsUniqueConstraintViolation(DbUpdateException exception);
    bool IsDeadlock(Exception exception);
}
