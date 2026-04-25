using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroService.Communication.Api.Extensions;
using ZawatSys.MicroService.Communication.Api.Middlewares;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCommunicationMicroHost(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecksWithDependencies(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

// Apply database migrations automatically on startup.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<CommunicationDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Applying Communication database migrations...");
        dbContext.Database.Migrate();
        logger.LogInformation("Communication database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the Communication database.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZawatSys Communication API v1");
    });
}

app.UseGlobalExceptionHandler();
app.UseTenantContext();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapCommunicationHealthEndpoints();

app.Run();

public partial class Program;
