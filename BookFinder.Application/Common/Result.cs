namespace BookFinder.Application.Common;

public readonly record struct Result<T>
{
    public T? Value { get; }
    public ResultError? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result(T value) { Value = value; Error = null; }
    private Result(ResultError error) { Value = default; Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(ResultError error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(ResultError error) => Failure(error);
}

public sealed record ResultError(string Code, string Message)
{
    public static readonly ResultError LlmUnavailable =
        new("llm.unavailable", "LLM extraction failed; falling back to normalized query.");
    public static readonly ResultError LlmInvalidResponse =
        new("llm.invalid_response", "LLM returned an unparsable response.");
    public static readonly ResultError OpenLibraryUnavailable =
        new("openlibrary.unavailable", "Open Library is currently unreachable.");
    public static readonly ResultError NoCandidates =
        new("search.no_candidates", "No matching books found.");
    public static ResultError Validation(string message) =>
        new("validation.failed", message);
}
