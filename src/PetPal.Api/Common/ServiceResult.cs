namespace PetPal.Api.Common;

/// <summary>
/// Servis qatının nəticəsi. Exception-larla axın idarə etmək əvəzinə
/// endpoint-lər bu tipi HTTP statusuna çevirir.
/// </summary>
public class ServiceResult<T>
{
    private ServiceResult() { }

    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public string Error { get; private init; } = string.Empty;
    public ServiceErrorKind ErrorKind { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Succeeded = true, Value = value };

    public static ServiceResult<T> Fail(string error, ServiceErrorKind kind = ServiceErrorKind.Validation) =>
        new() { Succeeded = false, Error = error, ErrorKind = kind };

    public static ServiceResult<T> NotFound(string error) => Fail(error, ServiceErrorKind.NotFound);

    public static ServiceResult<T> Forbidden(string error) => Fail(error, ServiceErrorKind.Forbidden);

    public static ServiceResult<T> Conflict(string error) => Fail(error, ServiceErrorKind.Conflict);
}

public enum ServiceErrorKind
{
    Validation = 0,
    NotFound = 1,
    Forbidden = 2,
    Conflict = 3
}
