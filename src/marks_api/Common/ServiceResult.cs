namespace marks_api.Common;

public enum ServiceStatus
{
    Success,
    Created,
    NotFound,
    Conflict,
    BadRequest
}

public sealed class ServiceResult
{
    private ServiceResult(ServiceStatus status, string? error)
    {
        Status = status;
        Error = error;
    }

    public ServiceStatus Status { get; }

    public string? Error { get; }

    public static ServiceResult Success()
        => new(ServiceStatus.Success, null);

    public static ServiceResult NotFound(string error)
        => new(ServiceStatus.NotFound, error);

    public static ServiceResult Conflict(string error)
        => new(ServiceStatus.Conflict, error);

    public static ServiceResult BadRequest(string error)
        => new(ServiceStatus.BadRequest, error);
}

public sealed class ServiceResult<T>
{
    private ServiceResult(ServiceStatus status, T? data, string? error)
    {
        Status = status;
        Data = data;
        Error = error;
    }

    public ServiceStatus Status { get; }

    public T? Data { get; }

    public string? Error { get; }

    public static ServiceResult<T> Success(T data)
        => new(ServiceStatus.Success, data, null);

    public static ServiceResult<T> Created(T data)
        => new(ServiceStatus.Created, data, null);

    public static ServiceResult<T> NotFound(string error)
        => new(ServiceStatus.NotFound, default, error);

    public static ServiceResult<T> Conflict(string error)
        => new(ServiceStatus.Conflict, default, error);

    public static ServiceResult<T> BadRequest(string error)
        => new(ServiceStatus.BadRequest, default, error);
}
