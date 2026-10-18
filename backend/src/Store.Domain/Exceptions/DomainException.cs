namespace Store.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule inside the domain model is violated.
/// Mapped to 400 Bad Request by the API.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
