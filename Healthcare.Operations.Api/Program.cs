using Healthcare.Operations.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<OperationsStore>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    var expectedToken = app.Configuration["ServiceToken"] ?? "local-dev-token";
    if (context.Request.Headers.Authorization != $"Bearer {expectedToken}")
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new { error = "Unauthorized service caller" }
        );
        return;
    }

    if (
        !context.Request.Headers.ContainsKey("X-Tenant-Id")
        || !context.Request.Headers.ContainsKey("X-Actor-Id")
    )
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(
            new { error = "Tenant and actor context required" }
        );
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
