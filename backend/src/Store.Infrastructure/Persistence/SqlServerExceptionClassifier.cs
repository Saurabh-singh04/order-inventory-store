using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Store.Application.Abstractions;

namespace Store.Infrastructure.Persistence;

public sealed class SqlServerExceptionClassifier : IDbExceptionClassifier
{
    // https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int Deadlock = 1205;

    public bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        FindSqlException(exception) is { Number: UniqueIndexViolation or UniqueConstraintViolation };

    public bool IsDeadlock(Exception exception) =>
        FindSqlException(exception) is { Number: Deadlock };

    private static SqlException? FindSqlException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException sql)
                return sql;
            exception = exception.InnerException;
        }

        return null;
    }
}
