namespace AERIE.Application.Common.Models;

/// <summary>
/// Standardized API response wrapper used across all endpoints.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public T? Value => Data;
    public string? Message { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];
    public string? Error => Errors.FirstOrDefault();

    private Result() { }

    public static Result<T> Success(T data, string? message = null) =>
        new() { IsSuccess = true, Data = data, Message = message };

    public static Result<T> Failure(string error) =>
        new() { IsSuccess = false, Errors = [error] };

    public static Result<T> Failure(IEnumerable<string> errors) =>
        new() { IsSuccess = false, Errors = errors.ToList().AsReadOnly() };
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? Message { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];
    public string? Error => Errors.FirstOrDefault();

    private Result() { }

    public static Result Success(string? message = null) =>
        new() { IsSuccess = true, Message = message };

    public static Result Failure(string error) =>
        new() { IsSuccess = false, Errors = [error] };
}

/// <summary>
/// Server-side paginated result wrapper.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
