namespace Store.Application.Common;

/// <summary>Base type for errors that map to a specific HTTP status code.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }

    public abstract int StatusCode { get; }
    public abstract string Title { get; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.")
    {
    }

    public override int StatusCode => 404;
    public override string Title => "Not found";
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message, string? code = null) : base(message)
    {
        Code = code;
    }

    /// <summary>Machine readable reason, e.g. "out_of_stock" - lets the UI pick a message.</summary>
    public string? Code { get; }

    public override int StatusCode => 409;
    public override string Title => "Conflict";
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You are not allowed to perform this action.") : base(message)
    {
    }

    public override int StatusCode => 403;
    public override string Title => "Forbidden";
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public override int StatusCode => 401;
    public override string Title => "Unauthorized";
}

public sealed class ValidationException : AppException
{
    public ValidationException(string message, IDictionary<string, string[]>? errors = null) : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public IDictionary<string, string[]> Errors { get; }

    public override int StatusCode => 400;
    public override string Title => "Validation failed";
}

/// <summary>Same Idempotency-Key re-used with a different request body.</summary>
public sealed class IdempotencyKeyReuseException : AppException
{
    public IdempotencyKeyReuseException()
        : base("This Idempotency-Key was already used for a different order request.")
    {
    }

    public override int StatusCode => 422;
    public override string Title => "Idempotency key reused";
}
