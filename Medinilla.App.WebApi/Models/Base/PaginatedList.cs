namespace Medinilla.App.WebApi.Models.Base;

/// <summary>
/// Represents a paginated list of items.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public class PaginatedList<T> : List<T>
{
    /// <summary>
    /// Gets the current page index.
    /// </summary>
    public int PageIndex { get; private set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaginatedList{T}"/> class.
    /// </summary>
    /// <param name="items">The items to include in the list.</param>
    /// <param name="count">The total number of items.</param>
    /// <param name="pageIndex">The current page index.</param>
    /// <param name="pageSize">The size of each page.</param>
    public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);

        this.AddRange(items);
    }

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageIndex > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageIndex < TotalPages;

    /// <summary>
    /// Creates a paginated list asynchronously.
    /// </summary>
    /// <param name="source">The source queryable.</param>
    /// <param name="pageIndex">The current page index.</param>
    /// <param name="pageSize">The size of each page.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paginated list.</returns>
    public static async Task<PaginatedList<T>> CreateAsync(IEnumerable<T> source, int pageIndex, int pageSize)
    {
        return await Task.Run(() =>
        {
            var count = source.Count();
            var items = source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        });
    }
}
