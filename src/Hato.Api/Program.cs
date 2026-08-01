using Hato.Api;
using Hato.Api.Endpoints;
using Hato.Modules.Livestock.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddLivestockModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapSpeciesEndpoints();
app.MapBreedsEndpoints();
app.MapAnimalCategoriesEndpoints();
app.MapAnimalsEndpoints();
app.MapAnimalGroupsEndpoints();
app.MapAnimalEventsEndpoints();

app.Run();

public partial class Program;
