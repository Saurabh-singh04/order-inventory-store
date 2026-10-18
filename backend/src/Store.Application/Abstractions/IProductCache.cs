namespace Store.Application.Abstractions;

public interface IProductCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken);

    /// <summary>Drops every cached product list.</summary>
    void Invalidate();
}
