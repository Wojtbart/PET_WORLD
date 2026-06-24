# PetWorld — Design Document
**Data:** 2026-06-24  
**Stack:** Blazor Server, Onion/Clean Architecture, Microsoft Agent Framework, MySQL, Docker Compose

---

## Cel

Sklep internetowy PetWorld z AI chatem. Klient zadaje pytanie → system Writer-Critic generuje odpowiedź → zapis do historii.

---

## Architektura — Onion/Clean Architecture

Cztery projekty .NET w jednym solution. Zależności idą tylko do środka (Domain nie zna nikogo).

```
PetWorld.Domain          ← centrum, zero zewnętrznych zależności
PetWorld.Application     ← zależy tylko od Domain
PetWorld.Infrastructure  ← zależy od Application + Domain
PetWorld.Web             ← Blazor Server, zależy od Application
```

### Zasada Onion
- Domain definiuje interfejsy (IChatRepository, IAgentService)
- Infrastructure implementuje te interfejsy (MySQL, MAF)
- Application orkiestruje logikę biznesową (WriterCriticOrchestrator)
- Web wywołuje Application przez dependency injection

---

## Warstwy — szczegóły

### PetWorld.Domain
```
Entities/
  ChatMessage.cs       — Id, Question, Answer, Iterations, CreatedAt
  Product.cs           — Id, Name, Category, Price, Description
Interfaces/
  IChatRepository.cs   — SaveAsync, GetAllAsync
  IAgentService.cs     — GenerateResponseAsync(question) → WriterCriticResult
```
Brak NuGet packages. Czyste C# classes.

### PetWorld.Application
```
UseCases/
  SendMessage/
    SendMessageCommand.cs       — record z Question
    SendMessageHandler.cs       — wywołuje IAgentService → IChatRepository
  GetHistory/
    GetHistoryQuery.cs
    GetHistoryHandler.cs        — wywołuje IChatRepository.GetAllAsync
DTOs/
  WriterCriticResult.cs         — Answer, Iterations
```
Zależy od: Domain. NuGet: MediatR.

### PetWorld.Infrastructure
```
Persistence/
  AppDbContext.cs               — EF Core DbContext
  ChatRepository.cs             — implementuje IChatRepository
Agents/
  AgentService.cs               — implementuje IAgentService, używa MAF
Migrations/                     — EF Core migrations
```
Zależy od: Application, Domain. NuGet: Microsoft.Agents.AI, EF Core, Pomelo.MySQL.

### PetWorld.Web (Blazor Server)
```
Pages/
  Chat.razor                    — pole tekstowe + przycisk Wyślij + wyświetlenie odpowiedzi + liczba iteracji
  History.razor                 — DataGrid z kolumnami: Data, Pytanie, Odpowiedź, Iteracje
Program.cs                      — DI registration, middleware
appsettings.json                — OpenAI ApiKey, connection string
```
Zależy od: Application. NuGet: MudBlazor (DataGrid).

---

## Writer-Critic Flow (Microsoft Agent Framework)

```
1. User → Chat.razor → SendMessageCommand
2. SendMessageHandler → IAgentService.GenerateResponseAsync(question)
3. AgentService (Infrastructure):
   
   PĘTLA (max 3 iteracje):
   a. Writer Agent (ChatCompletionAgent):
      - Instructions: rola doradcy PetWorld + katalog 10 produktów
      - Input: pytanie + ewentualny feedback z poprzedniej iteracji
      - Output: tekst odpowiedzi
   
   b. Critic Agent (ChatCompletionAgent):
      - Instructions: oceń odpowiedź, zwróć JSON {approved, feedback}
      - Input: pytanie + odpowiedź Writera
      - Output: { "approved": true/false, "feedback": "..." }
   
   c. Jeśli approved = true → STOP
      Jeśli iteracja = 3 → STOP
      Else → Writer dostaje feedback, iteracja++

4. Zwróć WriterCriticResult { Answer, Iterations }
5. SendMessageHandler → ChatRepository.SaveAsync(chatMessage)
6. Chat.razor wyświetla odpowiedź + "Iteracje: X/3"
```

### Konfiguracja MAF w kodzie
```csharp
// Infrastructure/Agents/AgentService.cs
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o-mini", apiKey)
    .Build();

var writer = new ChatCompletionAgent {
    Name = "Writer",
    Instructions = writerPrompt,  // rola + katalog produktów
    Kernel = kernel
};

var critic = new ChatCompletionAgent {
    Name = "Critic",
    Instructions = criticPrompt,  // instrukcja oceny + format JSON
    Kernel = kernel
};
```

### Writer System Prompt
```
Jesteś pomocnikiem sklepu PetWorld. Pomagasz klientom wybrać produkty.

Katalog produktów:
- Royal Canin Adult Dog 15kg | Karma dla psów | 289 zł
- Whiskas Adult Kurczak 7kg | Karma dla kotów | 129 zł
- Tetra AquaSafe 500ml | Akwarystyka | 45 zł
- Trixie Drapak XL 150cm | Akcesoria dla kotów | 399 zł
- Kong Classic Large | Zabawki dla psów | 69 zł
- Ferplast Klatka dla chomika | Gryzonie | 189 zł
- Flexi Smycz automatyczna 8m | Akcesoria dla psów | 119 zł
- Brit Premium Kitten 8kg | Karma dla kotów | 159 zł
- JBL ProFlora CO2 Set | Akwarystyka | 549 zł
- Vitapol Siano dla królików 1kg | Gryzonie | 25 zł

Odpowiadaj po polsku. Rekomenduj konkretne produkty z katalogu gdy to możliwe.
```

### Critic System Prompt
```
Oceniasz odpowiedzi asystenta sklepu PetWorld.
Zwróć WYŁĄCZNIE JSON w formacie:
{"approved": true/false, "feedback": "co poprawić lub empty string gdy approved"}

Kryteria odrzucenia:
- odpowiedź nie dotyczy pytania
- brak rekomendacji produktu gdy pytanie dotyczy zakupu
- odpowiedź jest zbyt krótka lub niejasna
```

---

## Baza danych MySQL

```sql
CREATE TABLE ChatMessages (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    Question    TEXT NOT NULL,
    Answer      TEXT NOT NULL,
    Iterations  INT NOT NULL,
    CreatedAt   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

EF Core Code-First — migracje generowane automatycznie przy starcie aplikacji.

---

## Konfiguracja API Key

`appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-twój-klucz-tutaj",
    "Model": "gpt-4o-mini"
  },
  "ConnectionStrings": {
    "Default": "Server=db;Database=petworlddb;User=root;Password=petworld;"
  }
}
```

Gdy `ApiKey` = pusty string → `MockAgentService` zamiast prawdziwego → statyczne odpowiedzi testowe.

---

## Docker Compose

```yaml
services:
  db:
    image: mysql:8
    environment:
      MYSQL_ROOT_PASSWORD: petworld
      MYSQL_DATABASE: petworlddb
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 5s
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

Aplikacja dostępna na http://localhost:5000 po `docker compose up`.

---

## UI — strony Blazor

### Chat.razor
- Pole tekstowe (pytanie klienta)
- Przycisk "Wyślij"
- Sekcja odpowiedzi: tekst + "Iteracje: X/3"
- Loading spinner podczas oczekiwania na AI

### History.razor
- DataGrid (MudBlazor MudDataGrid)
- Kolumny: Data | Pytanie | Odpowiedź | Liczba iteracji
- Dane ładowane przy wejściu na stronę

---

## Dependency Injection (Program.cs)

```csharp
// Rejestracja warstw
builder.Services.AddDbContext<AppDbContext>(...);
builder.Services.AddScoped<IChatRepository, ChatRepository>();

// Mock lub prawdziwy AgentService zależnie od ApiKey
if (string.IsNullOrEmpty(apiKey))
    builder.Services.AddScoped<IAgentService, MockAgentService>();
else
    builder.Services.AddScoped<IAgentService, AgentService>();

builder.Services.AddMediatR(typeof(SendMessageHandler));
```

---

## Kryteria sukcesu

- [ ] `docker compose up` uruchamia aplikację na http://localhost:5000
- [ ] Chat zadaje pytanie i otrzymuje odpowiedź z liczbą iteracji
- [ ] Historia pokazuje wszystkie poprzednie pytania w DataGrid
- [ ] Kod jest podzielony na 4 projekty zgodnie z Onion Architecture
- [ ] Writer-Critic iteruje max 3 razy
