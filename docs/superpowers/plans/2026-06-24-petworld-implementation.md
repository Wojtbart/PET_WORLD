# PetWorld Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zbudować PetWorld — sklep dla zwierząt z AI chatem (Writer-Critic, max 3 iteracje), Blazor Server UI, MySQL, uruchamiany przez `docker compose up`.

**Architecture:** Onion/Clean Architecture — 4 projekty .NET. Domain (centrum, zero zależności) → Application → Infrastructure → Web. Microsoft Agent Framework obsługuje Writer-Critic loop w Infrastructure. Composition root (Program.cs w Web) drukuje zależności przez DI.

**Tech Stack:** .NET 8, Blazor Server, Microsoft.Agents.AI + Microsoft.Extensions.AI.OpenAI, OpenAI gpt-4o-mini, EF Core 8 + Pomelo MySQL, MudBlazor, MediatR 12, Docker Compose, xUnit + Moq.

## Global Constraints

- Target framework: `net8.0`
- Blazor Server (nie WebAssembly)
- Port aplikacji: 5000 (mapowany na 8080 wewnątrz kontenera)
- API key OpenAI w `appsettings.json` pod kluczem `OpenAI:ApiKey`
- Model OpenAI: `gpt-4o-mini`
- Wszystkie odpowiedzi AI po polsku
- Writer-Critic: max 3 iteracje, zawsze
- MySQL hasło: `petworld`, baza: `petworlddb`

---

### Task 1: Solution scaffold

**Files:**
- Create: `PetWorld.sln`
- Create: `PetWorld.Domain/PetWorld.Domain.csproj`
- Create: `PetWorld.Application/PetWorld.Application.csproj`
- Create: `PetWorld.Infrastructure/PetWorld.Infrastructure.csproj`
- Create: `PetWorld.Web/PetWorld.Web.csproj`
- Create: `PetWorld.Tests/PetWorld.Tests.csproj`
- Create: `.gitignore`

**Interfaces:**
- Produces: gotowa struktura solution z referencjami i NuGet packages

- [ ] **Step 1: Zainicjuj git i .gitignore**

```bash
git init
dotnet new gitignore
```

- [ ] **Step 2: Utwórz solution i projekty**

```bash
dotnet new sln -n PetWorld
dotnet new classlib -n PetWorld.Domain -f net8.0
dotnet new classlib -n PetWorld.Application -f net8.0
dotnet new classlib -n PetWorld.Infrastructure -f net8.0
dotnet new blazorserver -n PetWorld.Web -f net8.0
dotnet new xunit -n PetWorld.Tests -f net8.0
```

- [ ] **Step 3: Dodaj projekty do solution**

```bash
dotnet sln add PetWorld.Domain/PetWorld.Domain.csproj
dotnet sln add PetWorld.Application/PetWorld.Application.csproj
dotnet sln add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj
dotnet sln add PetWorld.Web/PetWorld.Web.csproj
dotnet sln add PetWorld.Tests/PetWorld.Tests.csproj
```

- [ ] **Step 4: Ustaw referencje (Onion Architecture)**

```bash
# Application widzi Domain
dotnet add PetWorld.Application/PetWorld.Application.csproj reference PetWorld.Domain/PetWorld.Domain.csproj

# Infrastructure widzi Domain + Application
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj reference PetWorld.Domain/PetWorld.Domain.csproj
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj reference PetWorld.Application/PetWorld.Application.csproj

# Web widzi Application (logika) + Infrastructure (tylko dla DI w Program.cs)
dotnet add PetWorld.Web/PetWorld.Web.csproj reference PetWorld.Application/PetWorld.Application.csproj
dotnet add PetWorld.Web/PetWorld.Web.csproj reference PetWorld.Infrastructure/PetWorld.Infrastructure.csproj

# Tests widzi Application + Domain
dotnet add PetWorld.Tests/PetWorld.Tests.csproj reference PetWorld.Application/PetWorld.Application.csproj
dotnet add PetWorld.Tests/PetWorld.Tests.csproj reference PetWorld.Domain/PetWorld.Domain.csproj
```

- [ ] **Step 5: Zainstaluj NuGet packages**

```bash
# Application
dotnet add PetWorld.Application/PetWorld.Application.csproj package MediatR --version 12.*

# Infrastructure — MAF + EF Core + MySQL
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.Agents.AI --version 1.*
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.Agents.AI.OpenAI --version 1.* --prerelease
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.Extensions.AI.OpenAI --prerelease
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package OpenAI --version 2.*
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.EntityFrameworkCore --version 8.*
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Pomelo.EntityFrameworkCore.MySql --version 8.*
dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.*

# Web — Blazor + MudBlazor
dotnet add PetWorld.Web/PetWorld.Web.csproj package MudBlazor

# Tests
dotnet add PetWorld.Tests/PetWorld.Tests.csproj package Moq
dotnet add PetWorld.Tests/PetWorld.Tests.csproj package Microsoft.NET.Test.Sdk
```

- [ ] **Step 6: Usuń placeholder klasy**

```bash
rm PetWorld.Domain/Class1.cs
rm PetWorld.Application/Class1.cs
rm PetWorld.Infrastructure/Class1.cs
```

- [ ] **Step 7: Build i weryfikacja**

```bash
dotnet build PetWorld.sln
```

Oczekiwane: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add .
git commit -m "feat: initialize solution with Onion Architecture project structure"
```

---

### Task 2: Domain layer

**Files:**
- Create: `PetWorld.Domain/Entities/ChatMessage.cs`
- Create: `PetWorld.Domain/Interfaces/IChatRepository.cs`
- Create: `PetWorld.Domain/Interfaces/IAgentService.cs`

**Interfaces:**
- Produces:
  - `ChatMessage` — encja bazy danych
  - `IChatRepository` — `SaveAsync(ChatMessage)`, `GetAllAsync() → List<ChatMessage>`
  - `IAgentService` — `GenerateResponseAsync(question) → AgentResult`
  - `AgentResult` — `record(string Answer, int Iterations)`

- [ ] **Step 1: Utwórz ChatMessage entity**

`PetWorld.Domain/Entities/ChatMessage.cs`:
```csharp
namespace PetWorld.Domain.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Utwórz IChatRepository**

`PetWorld.Domain/Interfaces/IChatRepository.cs`:
```csharp
using PetWorld.Domain.Entities;

namespace PetWorld.Domain.Interfaces;

public interface IChatRepository
{
    Task SaveAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetAllAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Utwórz IAgentService + AgentResult**

`PetWorld.Domain/Interfaces/IAgentService.cs`:
```csharp
namespace PetWorld.Domain.Interfaces;

public record AgentResult(string Answer, int Iterations);

public interface IAgentService
{
    Task<AgentResult> GenerateResponseAsync(string question, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Build Domain**

```bash
dotnet build PetWorld.Domain/PetWorld.Domain.csproj
```

Oczekiwane: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add PetWorld.Domain/
git commit -m "feat: add Domain layer - ChatMessage entity, IChatRepository, IAgentService"
```

---

### Task 3: Application layer

**Files:**
- Create: `PetWorld.Application/UseCases/SendMessage/SendMessageCommand.cs`
- Create: `PetWorld.Application/UseCases/SendMessage/SendMessageHandler.cs`
- Create: `PetWorld.Application/UseCases/GetHistory/GetHistoryQuery.cs`
- Create: `PetWorld.Application/UseCases/GetHistory/GetHistoryHandler.cs`
- Create: `PetWorld.Tests/SendMessageHandlerTests.cs`

**Interfaces:**
- Consumes: `IAgentService`, `AgentResult`, `IChatRepository`, `ChatMessage`
- Produces:
  - `SendMessageCommand(string Question)` → MediatR → `AgentResult`
  - `GetHistoryQuery()` → MediatR → `List<ChatMessage>`

- [ ] **Step 1: Napisz failing test**

`PetWorld.Tests/SendMessageHandlerTests.cs`:
```csharp
using Moq;
using PetWorld.Application.UseCases.SendMessage;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Tests;

public class SendMessageHandlerTests
{
    private readonly Mock<IAgentService> _agentMock = new();
    private readonly Mock<IChatRepository> _repoMock = new();

    [Fact]
    public async Task Handle_SavesChatMessage_AndReturnsAgentResult()
    {
        _agentMock
            .Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult("Polecamy Royal Canin 15kg za 289 zł.", 2));

        ChatMessage? saved = null;
        _repoMock
            .Setup(x => x.SaveAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatMessage, CancellationToken>((msg, _) => saved = msg)
            .Returns(Task.CompletedTask);

        var handler = new SendMessageHandler(_agentMock.Object, _repoMock.Object);
        var result = await handler.Handle(new SendMessageCommand("Jaka karma dla psa?"), CancellationToken.None);

        Assert.Equal("Polecamy Royal Canin 15kg za 289 zł.", result.Answer);
        Assert.Equal(2, result.Iterations);
        Assert.NotNull(saved);
        Assert.Equal("Jaka karma dla psa?", saved!.Question);
        Assert.Equal(2, saved.Iterations);
    }
}
```

- [ ] **Step 2: Uruchom test — potwierdź FAIL**

```bash
dotnet test PetWorld.Tests/PetWorld.Tests.csproj
```

Oczekiwane: FAIL — `SendMessageHandler` not found.

- [ ] **Step 3: Utwórz SendMessageCommand**

`PetWorld.Application/UseCases/SendMessage/SendMessageCommand.cs`:
```csharp
using MediatR;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Application.UseCases.SendMessage;

public record SendMessageCommand(string Question) : IRequest<AgentResult>;
```

- [ ] **Step 4: Utwórz SendMessageHandler**

`PetWorld.Application/UseCases/SendMessage/SendMessageHandler.cs`:
```csharp
using MediatR;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Application.UseCases.SendMessage;

public class SendMessageHandler : IRequestHandler<SendMessageCommand, AgentResult>
{
    private readonly IAgentService _agentService;
    private readonly IChatRepository _repository;

    public SendMessageHandler(IAgentService agentService, IChatRepository repository)
    {
        _agentService = agentService;
        _repository = repository;
    }

    public async Task<AgentResult> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var result = await _agentService.GenerateResponseAsync(request.Question, cancellationToken);

        await _repository.SaveAsync(new ChatMessage
        {
            Question = request.Question,
            Answer = result.Answer,
            Iterations = result.Iterations,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return result;
    }
}
```

- [ ] **Step 5: Utwórz GetHistoryQuery**

`PetWorld.Application/UseCases/GetHistory/GetHistoryQuery.cs`:
```csharp
using MediatR;
using PetWorld.Domain.Entities;

namespace PetWorld.Application.UseCases.GetHistory;

public record GetHistoryQuery() : IRequest<List<ChatMessage>>;
```

- [ ] **Step 6: Utwórz GetHistoryHandler**

`PetWorld.Application/UseCases/GetHistory/GetHistoryHandler.cs`:
```csharp
using MediatR;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Application.UseCases.GetHistory;

public class GetHistoryHandler : IRequestHandler<GetHistoryQuery, List<ChatMessage>>
{
    private readonly IChatRepository _repository;

    public GetHistoryHandler(IChatRepository repository) => _repository = repository;

    public Task<List<ChatMessage>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
        => _repository.GetAllAsync(cancellationToken);
}
```

- [ ] **Step 7: Uruchom testy — potwierdź PASS**

```bash
dotnet test PetWorld.Tests/PetWorld.Tests.csproj
```

Oczekiwane: `1 passed`.

- [ ] **Step 8: Commit**

```bash
git add PetWorld.Application/ PetWorld.Tests/
git commit -m "feat: add Application layer - SendMessage and GetHistory use cases with MediatR"
```

---

### Task 4: Infrastructure — Persistence (MySQL + EF Core)

**Files:**
- Create: `PetWorld.Infrastructure/Persistence/AppDbContext.cs`
- Create: `PetWorld.Infrastructure/Persistence/ChatRepository.cs`

**Interfaces:**
- Consumes: `ChatMessage`, `IChatRepository`
- Produces:
  - `AppDbContext : DbContext` — `DbSet<ChatMessage> ChatMessages`
  - `ChatRepository : IChatRepository` — EF Core implementacja SaveAsync + GetAllAsync

- [ ] **Step 1: Utwórz AppDbContext**

`PetWorld.Infrastructure/Persistence/AppDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PetWorld.Domain.Entities;

namespace PetWorld.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Question).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Answer).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Iterations).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
```

- [ ] **Step 2: Utwórz ChatRepository**

`PetWorld.Infrastructure/Persistence/ChatRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;
using PetWorld.Infrastructure.Persistence;

namespace PetWorld.Infrastructure.Persistence;

public class ChatRepository : IChatRepository
{
    private readonly AppDbContext _context;

    public ChatRepository(AppDbContext context) => _context = context;

    public async Task SaveAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ChatMessage>> GetAllAsync(CancellationToken cancellationToken = default)
        => _context.ChatMessages
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
}
```

- [ ] **Step 3: Build Infrastructure**

```bash
dotnet build PetWorld.Infrastructure/PetWorld.Infrastructure.csproj
```

Oczekiwane: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add PetWorld.Infrastructure/Persistence/
git commit -m "feat: add Infrastructure persistence - AppDbContext and ChatRepository"
```

---

### Task 5: Infrastructure — Agent Service (MAF Writer-Critic)

**Files:**
- Create: `PetWorld.Infrastructure/Agents/CriticResult.cs`
- Create: `PetWorld.Infrastructure/Agents/AgentService.cs`

**Interfaces:**
- Consumes: `IAgentService`, `AgentResult` z `PetWorld.Domain.Interfaces`
- Produces: `AgentService : IAgentService` — Writer-Critic pętla z MAF, max 3 iteracje

- [ ] **Step 1: Utwórz CriticResult**

`PetWorld.Infrastructure/Agents/CriticResult.cs`:
```csharp
using System.Text.Json.Serialization;

namespace PetWorld.Infrastructure.Agents;

internal record CriticResult(
    [property: JsonPropertyName("approved")] bool Approved,
    [property: JsonPropertyName("feedback")] string Feedback
);
```

- [ ] **Step 2: Utwórz AgentService**

`PetWorld.Infrastructure/Agents/AgentService.cs`:
```csharp
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Infrastructure.Agents;

public class AgentService : IAgentService
{
    private const string WriterPrompt = """
        Jesteś pomocnikiem sklepu PetWorld. Pomagasz klientom wybrać produkty dla zwierząt domowych.

        Katalog produktów:
        - Royal Canin Adult Dog 15kg | Karma dla psów | 289 zł | Premium karma dla dorosłych psów średnich ras
        - Whiskas Adult Kurczak 7kg | Karma dla kotów | 129 zł | Sucha karma dla dorosłych kotów z kurczakiem
        - Tetra AquaSafe 500ml | Akwarystyka | 45 zł | Uzdatniacz wody do akwarium, neutralizuje chlor
        - Trixie Drapak XL 150cm | Akcesoria dla kotów | 399 zł | Wysoki drapak z platformami i domkiem
        - Kong Classic Large | Zabawki dla psów | 69 zł | Wytrzymała zabawka do napełniania smakołykami
        - Ferplast Klatka dla chomika | Gryzonie | 189 zł | Klatka 60x40cm z wyposażeniem
        - Flexi Smycz automatyczna 8m | Akcesoria dla psów | 119 zł | Smycz zwijana dla psów do 50kg
        - Brit Premium Kitten 8kg | Karma dla kotów | 159 zł | Karma dla kociąt do 12 miesiąca życia
        - JBL ProFlora CO2 Set | Akwarystyka | 549 zł | Kompletny zestaw CO2 dla roślin akwariowych
        - Vitapol Siano dla królików 1kg | Gryzonie | 25 zł | Naturalne siano łąkowe, podstawa diety

        Odpowiadaj po polsku. Gdy pytanie dotyczy zakupu lub doboru produktu, zarekomenduj konkretny produkt z katalogu wraz z ceną.
        """;

    private const string CriticPrompt = """
        Oceniasz odpowiedzi asystenta sklepu PetWorld.
        Zwróć WYŁĄCZNIE JSON bez żadnych dodatkowych znaków, w formacie:
        {"approved": true, "feedback": ""}

        Odrzuć odpowiedź (approved: false) gdy:
        - odpowiedź nie dotyczy zadanego pytania
        - pytanie dotyczy zakupu produktu, ale brak konkretnej rekomendacji z ceną
        - odpowiedź jest krótsza niż 2 zdania i nie zawiera wystarczających informacji
        """;

    private readonly AIAgent _writer;
    private readonly AIAgent _critic;

    public AgentService(string apiKey, string model)
    {
        var client = new OpenAIClient(apiKey);

        _writer = client
            .GetChatClient(model)
            .AsIChatClient()
            .CreateAIAgent(instructions: WriterPrompt, name: "Writer");

        _critic = client
            .GetChatClient(model)
            .AsIChatClient()
            .CreateAIAgent(instructions: CriticPrompt, name: "Critic");
    }

    public async Task<AgentResult> GenerateResponseAsync(string question, CancellationToken cancellationToken = default)
    {
        string answer = string.Empty;
        string feedback = string.Empty;

        for (int iteration = 1; iteration <= 3; iteration++)
        {
            var writerInput = iteration == 1
                ? question
                : $"Pytanie klienta: {question}\n\nFeedback od recenzenta: {feedback}\n\nPopraw swoją odpowiedź uwzględniając feedback.";

            answer = await _writer.RunAsync(writerInput);

            var criticInput = $"Pytanie klienta: {question}\n\nOdpowiedź asystenta: {answer}";
            var criticResponse = await _critic.RunAsync(criticInput);

            var criticResult = ParseCriticResponse(criticResponse);
            if (criticResult.Approved)
                return new AgentResult(answer, iteration);

            feedback = criticResult.Feedback;
        }

        return new AgentResult(answer, 3);
    }

    private static CriticResult ParseCriticResponse(string response)
    {
        try
        {
            var cleaned = response.Trim().Trim('`');
            if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[4..].Trim();
            return JsonSerializer.Deserialize<CriticResult>(cleaned)
                   ?? new CriticResult(true, string.Empty);
        }
        catch
        {
            return new CriticResult(true, string.Empty);
        }
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build PetWorld.Infrastructure/PetWorld.Infrastructure.csproj
```

Oczekiwane: `Build succeeded. 0 Error(s)`

> Jeśli błąd `AsIChatClient` not found — zainstaluj: `dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.Extensions.AI.OpenAI --prerelease`
> Jeśli błąd `CreateAIAgent` not found — zainstaluj: `dotnet add PetWorld.Infrastructure/PetWorld.Infrastructure.csproj package Microsoft.Agents.AI.OpenAI --prerelease`

- [ ] **Step 4: Commit**

```bash
git add PetWorld.Infrastructure/Agents/
git commit -m "feat: add AgentService with Writer-Critic loop using Microsoft Agent Framework"
```

---

### Task 6: Web — Bootstrap (Program.cs, MudBlazor, appsettings)

**Files:**
- Modify: `PetWorld.Web/Program.cs`
- Modify: `PetWorld.Web/appsettings.json`
- Modify: `PetWorld.Web/Pages/_Host.cshtml`
- Modify: `PetWorld.Web/Shared/MainLayout.razor`
- Modify: `PetWorld.Web/Pages/Index.razor`

**Interfaces:**
- Consumes: `AppDbContext`, `ChatRepository`, `AgentService`, `SendMessageHandler`, `GetHistoryHandler`
- Produces: aplikacja startuje, DI skonfigurowany, MudBlazor aktywny, navbar działa

- [ ] **Step 1: Zaktualizuj appsettings.json**

`PetWorld.Web/appsettings.json` (zastąp całą zawartość):
```json
{
  "OpenAI": {
    "ApiKey": "WSTAW_SWÓJ_KLUCZ_TUTAJ",
    "Model": "gpt-4o-mini"
  },
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=petworlddb;User=root;Password=petworld;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

> Zamień `WSTAW_SWÓJ_KLUCZ_TUTAJ` na swój klucz OpenAI (`sk-...`).
> Uwaga: connection string z `localhost` = lokalny dev. Docker-compose nadpisuje go zmienną środowiskową `ConnectionStrings__Default`.

- [ ] **Step 2: Zastąp Program.cs**

`PetWorld.Web/Program.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PetWorld.Application.UseCases.GetHistory;
using PetWorld.Domain.Interfaces;
using PetWorld.Infrastructure.Agents;
using PetWorld.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
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
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

- [ ] **Step 3: Dodaj MudBlazor do _Host.cshtml**

Otwórz `PetWorld.Web/Pages/_Host.cshtml`. W sekcji `<head>` dodaj przed `</head>`:
```html
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
```

Przed `</body>` dodaj:
```html
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

- [ ] **Step 4: Zastąp MainLayout.razor**

`PetWorld.Web/Shared/MainLayout.razor`:
```razor
@inherits LayoutComponentBase

<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1" Color="Color.Primary">
        <MudText Typo="Typo.h6" Class="mr-4">PetWorld</MudText>
        <MudNavLink Href="/chat" Match="NavLinkMatch.All" Style="color:white">Chat</MudNavLink>
        <MudNavLink Href="/historia" Style="color:white">Historia</MudNavLink>
    </MudAppBar>
    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="mt-6">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>
```

- [ ] **Step 5: Zastąp Index.razor**

`PetWorld.Web/Pages/Index.razor`:
```razor
@page "/"
@inject NavigationManager Nav

@code {
    protected override void OnInitialized() => Nav.NavigateTo("/chat");
}
```

- [ ] **Step 6: Build Web**

```bash
dotnet build PetWorld.Web/PetWorld.Web.csproj
```

Oczekiwane: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add PetWorld.Web/
git commit -m "feat: configure Web project - Program.cs, DI registration, MudBlazor setup"
```

---

### Task 7: Web — Chat page

**Files:**
- Create: `PetWorld.Web/Pages/Chat.razor`

**Interfaces:**
- Consumes: `SendMessageCommand(string Question)` → MediatR → `AgentResult(Answer, Iterations)`
- Produces: strona `/chat` — input + przycisk Wyślij + wyświetlenie odpowiedzi + liczba iteracji

- [ ] **Step 1: Utwórz Chat.razor**

`PetWorld.Web/Pages/Chat.razor`:
```razor
@page "/chat"
@using MediatR
@using PetWorld.Application.UseCases.SendMessage
@using PetWorld.Domain.Interfaces
@inject IMediator Mediator

<PageTitle>PetWorld — Chat</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">Chat z asystentem PetWorld</MudText>

<MudTextField @bind-Value="_question"
              Label="Twoje pytanie"
              Variant="Variant.Outlined"
              Lines="3"
              Class="mb-3"
              Disabled="@_isLoading" />

<MudButton Variant="Variant.Filled"
           Color="Color.Primary"
           OnClick="SendMessage"
           Disabled="@(_isLoading || string.IsNullOrWhiteSpace(_question))">
    @if (_isLoading)
    {
        <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
        <span>Przetwarzam...</span>
    }
    else
    {
        <span>Wyślij</span>
    }
</MudButton>

@if (_result is not null)
{
    <MudCard Class="mt-4">
        <MudCardContent>
            <MudText Typo="Typo.subtitle2" Color="Color.Primary" Class="mb-2">
                Odpowiedź (iteracje Writer-Critic: @_result.Iterations/3)
            </MudText>
            <MudText Style="white-space: pre-wrap">@_result.Answer</MudText>
        </MudCardContent>
    </MudCard>
}

@if (_error is not null)
{
    <MudAlert Severity="Severity.Error" Class="mt-4">@_error</MudAlert>
}

@code {
    private string _question = string.Empty;
    private AgentResult? _result;
    private string? _error;
    private bool _isLoading;

    private async Task SendMessage()
    {
        _error = null;
        _result = null;
        _isLoading = true;

        try
        {
            _result = await Mediator.Send(new SendMessageCommand(_question));
            _question = string.Empty;
        }
        catch (Exception ex)
        {
            _error = $"Błąd: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build PetWorld.Web/PetWorld.Web.csproj
```

Oczekiwane: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add PetWorld.Web/Pages/Chat.razor
git commit -m "feat: add Chat page with Writer-Critic response display"
```

---

### Task 8: Web — Historia page

**Files:**
- Create: `PetWorld.Web/Pages/History.razor`

**Interfaces:**
- Consumes: `GetHistoryQuery()` → MediatR → `List<ChatMessage>`
- Produces: strona `/historia` — MudDataGrid z kolumnami: Data, Pytanie, Odpowiedź, Iteracje

- [ ] **Step 1: Utwórz History.razor**

`PetWorld.Web/Pages/History.razor`:
```razor
@page "/historia"
@using MediatR
@using PetWorld.Application.UseCases.GetHistory
@using PetWorld.Domain.Entities
@inject IMediator Mediator

<PageTitle>PetWorld — Historia</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">Historia rozmów</MudText>

@if (_messages is null)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (!_messages.Any())
{
    <MudText Color="Color.Secondary">Brak historii. Zadaj pierwsze pytanie w zakładce Chat.</MudText>
}
else
{
    <MudDataGrid Items="@_messages" Dense="true" Bordered="true" Striped="true">
        <Columns>
            <PropertyColumn Property="x => x.CreatedAt" Title="Data" Format="dd.MM.yyyy HH:mm" />
            <PropertyColumn Property="x => x.Question" Title="Pytanie" />
            <PropertyColumn Property="x => x.Answer" Title="Odpowiedź" />
            <PropertyColumn Property="x => x.Iterations" Title="Iteracje" />
        </Columns>
    </MudDataGrid>
}

@code {
    private List<ChatMessage>? _messages;

    protected override async Task OnInitializedAsync()
    {
        _messages = await Mediator.Send(new GetHistoryQuery());
    }
}
```

- [ ] **Step 2: Build całego solution**

```bash
dotnet build PetWorld.sln
```

Oczekiwane: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add PetWorld.Web/Pages/History.razor
git commit -m "feat: add History page with MudDataGrid"
```

---

### Task 9: Docker

**Files:**
- Create: `Dockerfile`
- Create: `docker-compose.yml`
- Create: `.dockerignore`

**Interfaces:**
- Produces: `docker compose up` → aplikacja na `http://localhost:5000`

- [ ] **Step 1: Utwórz Dockerfile**

`Dockerfile` (w głównym katalogu, obok `PetWorld.sln`):
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PetWorld.Domain/PetWorld.Domain.csproj PetWorld.Domain/
COPY PetWorld.Application/PetWorld.Application.csproj PetWorld.Application/
COPY PetWorld.Infrastructure/PetWorld.Infrastructure.csproj PetWorld.Infrastructure/
COPY PetWorld.Web/PetWorld.Web.csproj PetWorld.Web/

RUN dotnet restore PetWorld.Web/PetWorld.Web.csproj

COPY . .
RUN dotnet publish PetWorld.Web/PetWorld.Web.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PetWorld.Web.dll"]
```

- [ ] **Step 2: Utwórz docker-compose.yml**

`docker-compose.yml`:
```yaml
services:
  db:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: petworld
      MYSQL_DATABASE: petworlddb
    ports:
      - "3306:3306"
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-ppetworld"]
      interval: 5s
      timeout: 5s
      retries: 10

  web:
    build: .
    ports:
      - "5000:8080"
    environment:
      - ConnectionStrings__Default=Server=db;Database=petworlddb;User=root;Password=petworld;
    depends_on:
      db:
        condition: service_healthy
```

- [ ] **Step 3: Utwórz .dockerignore**

`.dockerignore`:
```
**/bin/
**/obj/
**/.vs/
*.user
.git/
docs/
PetWorld.Tests/
```

- [ ] **Step 4: Zbuduj i przetestuj Docker lokalnie**

```bash
docker compose up --build
```

Oczekiwane: `web-1 | Now listening on: http://[::]:8080`

Otwórz `http://localhost:5000` i zweryfikuj:
- [ ] Strona `/chat` ładuje się
- [ ] Wpisz "Jaka karma dla psa?" → wyślij → odpowiedź z "Iteracje: X/3"
- [ ] Strona `/historia` pokazuje DataGrid z pytaniem

- [ ] **Step 5: Commit**

```bash
git add Dockerfile docker-compose.yml .dockerignore
git commit -m "feat: add Dockerfile and docker-compose for one-command deployment"
```

---

### Task 10: README i GitHub

**Files:**
- Create: `README.md`

- [ ] **Step 1: Utwórz README.md**

`README.md`:
```markdown
# PetWorld — AI Chat dla sklepu zoologicznego

Sklep internetowy z AI chatem opartym na systemie Writer-Critic (Microsoft Agent Framework).  
Budowany w Blazor Server z Onion/Clean Architecture.

## Szybki start

### Wymagania
- Docker Desktop

### Uruchomienie

1. Wstaw klucz API OpenAI w `PetWorld.Web/appsettings.json`:
   ```json
   { "OpenAI": { "ApiKey": "sk-twój-klucz" } }
   ```

2. ```bash
   docker compose up
   ```

3. Aplikacja: http://localhost:5000

## Architektura

Onion/Clean Architecture — 4 projekty .NET:

| Projekt | Rola | Zależności |
|---|---|---|
| `PetWorld.Domain` | Encje, interfejsy | Brak (centrum) |
| `PetWorld.Application` | Use cases (MediatR) | → Domain |
| `PetWorld.Infrastructure` | MySQL, MAF agents | → Domain, Application |
| `PetWorld.Web` | Blazor Server UI | → Application |

## System AI — Writer-Critic

Każde pytanie przechodzi przez pętlę (max 3 iteracje):

1. **Writer Agent** (MAF `AIAgent`) — generuje odpowiedź z rekomendacją produktu
2. **Critic Agent** (MAF `AIAgent`) — ocenia i zwraca `{"approved": bool, "feedback": "..."}`
3. Jeśli `approved = false` — Writer dostaje feedback i pisze ponownie
4. UI wyświetla ostateczną odpowiedź + liczbę iteracji

## Strony

- `/chat` — chat z asystentem PetWorld
- `/historia` — historia rozmów (DataGrid: Data, Pytanie, Odpowiedź, Iteracje)

## Zmienne środowiskowe (Docker)

| Zmienna | Wartość domyślna |
|---|---|
| `ConnectionStrings__Default` | ustawiona w docker-compose.yml |
| `OpenAI__ApiKey` | z appsettings.json |
```

- [ ] **Step 2: Utwórz publiczne repo na GitHub**

Na stronie GitHub.com:
1. Kliknij `+` → `New repository`
2. Nazwa: `petworld`
3. Ustaw na **Public**
4. Nie dodawaj README (już masz)
5. Kliknij `Create repository`

- [ ] **Step 3: Wypchnij kod**

```bash
git add README.md
git commit -m "docs: add README with setup instructions and architecture overview"

git remote add origin https://github.com/TWÓJ_LOGIN/petworld.git
git branch -M main
git push -u origin main
```

- [ ] **Step 4: Sprawdź repo publicznie**

Otwórz `https://github.com/TWÓJ_LOGIN/petworld` w przeglądarce jako niezalogowany użytkownik. Upewnij się że:
- [ ] Repo jest publiczne
- [ ] README.md jest widoczny
- [ ] Kod jest dostępny

---

## Podsumowanie struktury plików po implementacji

```
PetWorld/
├── PetWorld.sln
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
├── .gitignore
├── README.md
├── PetWorld.Domain/
│   ├── Entities/ChatMessage.cs
│   └── Interfaces/
│       ├── IAgentService.cs      ← zawiera też AgentResult record
│       └── IChatRepository.cs
├── PetWorld.Application/
│   └── UseCases/
│       ├── SendMessage/
│       │   ├── SendMessageCommand.cs
│       │   └── SendMessageHandler.cs
│       └── GetHistory/
│           ├── GetHistoryQuery.cs
│           └── GetHistoryHandler.cs
├── PetWorld.Infrastructure/
│   ├── Agents/
│   │   ├── AgentService.cs
│   │   └── CriticResult.cs
│   └── Persistence/
│       ├── AppDbContext.cs
│       └── ChatRepository.cs
├── PetWorld.Web/
│   ├── Pages/
│   │   ├── _Host.cshtml
│   │   ├── Index.razor
│   │   ├── Chat.razor
│   │   └── History.razor
│   ├── Shared/MainLayout.razor
│   ├── Program.cs
│   └── appsettings.json
└── PetWorld.Tests/
    └── SendMessageHandlerTests.cs
```
