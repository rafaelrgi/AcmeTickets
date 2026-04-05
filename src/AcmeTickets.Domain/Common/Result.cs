namespace AcmeTickets.Domain.Common;

public record Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    protected Result(bool isSuccess, string? errorMessage)
    {
        if (!isSuccess && string.IsNullOrWhiteSpace(errorMessage) || isSuccess && ! string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentNullException(nameof(errorMessage));
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result Ok() => new(true, null);
    public static Result Fail(string errorMessage) => new(false, errorMessage);
}

public sealed record Result<T> : Result where T : notnull
{
    private readonly T? _value;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Value cannot be accessed when IsSuccess is false");

    private Result(bool isSuccess, string? errorMessage, T? value) : base(isSuccess, errorMessage)
    {
        if (isSuccess && value is null || !isSuccess && value is not null)
            throw new ArgumentNullException(nameof(value));
        _value = value;
    }

    public static Result<T> Ok(T value) => new Result<T>(true, null, value);
    public new static Result<T> Fail(string errorMessage) => new Result<T>(false, errorMessage, default);
}
