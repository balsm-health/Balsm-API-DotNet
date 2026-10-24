using Balsm.SharedKernel.Results;

namespace Balsm.SharedKernel.Api;

public sealed class ApiResponse<T>
{
    public T? Data { get; init; }
    public ApiError? Error { get; init; }

    public static ApiResponse<T> Ok(T data) => new() { Data = data };
    public static ApiResponse<T> Fail(ApiError error) => new() { Error = error };

    public static ApiResponse<T> FromResult(Result<T> result, string correlationId) =>
        result.IsSuccess
            ? Ok(result.Value!)
            : Fail(new ApiError(result.Error!.Code, result.Error.Message, correlationId));
}

public sealed record ApiError(string Code, string Message, string CorrelationId);
