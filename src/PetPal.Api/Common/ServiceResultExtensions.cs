using PetPal.Shared.Dtos;

namespace PetPal.Api.Common;

public static class ServiceResultExtensions
{
    /// <summary>Servis nəticəsini standart HTTP cavabına çevirir.</summary>
    public static IResult ToHttpResult<T>(this ServiceResult<T> result)
    {
        if (result.Succeeded)
            return Results.Ok(result.Value);

        var payload = new ApiErrorResponse { Message = result.Error };

        return result.ErrorKind switch
        {
            ServiceErrorKind.NotFound => Results.NotFound(payload),
            ServiceErrorKind.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
            ServiceErrorKind.Conflict => Results.Conflict(payload),
            _ => Results.BadRequest(payload)
        };
    }
}
