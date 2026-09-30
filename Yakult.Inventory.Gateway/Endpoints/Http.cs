namespace Yakult.Inventory.Gateway.Endpoints;

/// <summary>Error bodies the desktop's GatewayClient reads ({ "error": "..." }).</summary>
public static class Http
{
    public static IResult Forbidden() =>
        Results.Json(new { error = "You don't have permission to do this. Ask an administrator for access." }, statusCode: StatusCodes.Status403Forbidden);

    public static IResult BadRequest(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status400BadRequest);
}
