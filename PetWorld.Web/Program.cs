using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PetWorld.Application.UseCases.GetHistory;
using PetWorld.Domain.Interfaces;
using PetWorld.Infrastructure.Agents;
using PetWorld.Infrastructure.Persistence;
using PetWorld.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var connectionString = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<IChatRepository, ChatRepository>();

var apiKey = builder.Configuration["OpenAI:ApiKey"]!;
var model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";
builder.Services.AddScoped<IAgentService>(_ => new AgentService(apiKey, model));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetHistoryHandler).Assembly));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
