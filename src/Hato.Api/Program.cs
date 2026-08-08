using Hato.Api;
using Hato.Api.Endpoints;
using Hato.Modules.Breeding.Infrastructure;
using Hato.Modules.Inventory.Infrastructure;
using Hato.Modules.Livestock.Infrastructure;
using Hato.Modules.People.Infrastructure;
using Hato.Modules.Production.Infrastructure;
using Hato.Modules.Tasks.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string AdminWebCorsPolicy = "AdminWebCors";

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddLivestockModule(builder.Configuration);
builder.Services.AddProductionModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddPeopleModule(builder.Configuration, builder.Environment);
builder.Services.AddBreedingModule(builder.Configuration);
builder.Services.AddTasksModule(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(AdminWebCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200"];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors(AdminWebCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapSpeciesEndpoints();
app.MapBreedsEndpoints();
app.MapAnimalCategoriesEndpoints();
app.MapAnimalsEndpoints();
app.MapAnimalGroupsEndpoints();
app.MapAnimalEventsEndpoints();
app.MapMortalityCausesEndpoints();
app.MapAdministrationRoutesEndpoints();
app.MapTreatmentReasonsEndpoints();
app.MapHealthPlansEndpoints();
app.MapMilkingEndpoints();
app.MapInventoryEndpoints();
app.MapPeopleEndpoints();
app.MapBreedingEndpoints();
app.MapTasksEndpoints();
app.MapFarmModulesEndpoints();
app.MapSyncEndpoints();

app.Run();

public partial class Program;
