using Hato.Api;
using Hato.Api.Endpoints;
using Hato.Modules.Livestock.Infrastructure;

using Hato.Modules.Production.Infrastructure;
using Hato.Modules.Inventory.Infrastructure;
using Hato.Modules.People.Infrastructure;
using Hato.Modules.Breeding.Infrastructure;
using Hato.Modules.Tasks.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddLivestockModule(builder.Configuration);
builder.Services.AddProductionModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddPeopleModule(builder.Configuration);
builder.Services.AddBreedingModule(builder.Configuration);
builder.Services.AddTasksModule(builder.Configuration);

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
app.MapMilkingEndpoints();
app.MapInventoryEndpoints();
app.MapPeopleEndpoints();
app.MapBreedingEndpoints();
app.MapTasksEndpoints();

app.Run();

public partial class Program;
