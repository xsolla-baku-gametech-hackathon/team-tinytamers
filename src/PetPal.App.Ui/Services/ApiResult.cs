namespace PetPal.App.Ui.Services;

/// <summary>
/// API çağırışının nəticəsi. Ekranlar exception tutmaq əvəzinə
/// <see cref="Succeeded"/> yoxlayır və <see cref="Error"/> mesajını uşağa göstərir.
/// </summary>
public class ApiResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string Error { get; init; } = string.Empty;
    public int StatusCode { get; init; }

    public static ApiResult<T> Ok(T? value, int statusCode = 200) =>
        new() { Succeeded = true, Value = value, StatusCode = statusCode };

    public static ApiResult<T> Fail(string error, int statusCode = 0) =>
        new() { Succeeded = false, Error = error, StatusCode = statusCode };
}
