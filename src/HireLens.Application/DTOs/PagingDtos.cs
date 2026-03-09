namespace HireLens.Application.DTOs;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount <= 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
