# PetWorld — AI Chat dla sklepu zoologicznego

Sklep internetowy z AI chatem opartym na systemie Writer-Critic (Microsoft Agent Framework).
Zbudowany w Blazor Server z Onion/Clean Architecture.

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

Onion/Clean Architecture — 4 projekty .NET. Zależności idą tylko do centrum (Domain nie zna żadnej biblioteki zewnętrznej).

```
PetWorld.Domain          ← centrum, zero zewnętrznych zależności
    Entities/ChatMessage.cs
    Interfaces/IChatRepository.cs
    Interfaces/IAgentService.cs

PetWorld.Application     ← zależy tylko od Domain
    UseCases/SendMessage/SendMessageCommand.cs + SendMessageHandler.cs
    UseCases/GetHistory/GetHistoryQuery.cs + GetHistoryHandler.cs

PetWorld.Infrastructure  ← zależy od Domain + Application
    Persistence/AppDbContext.cs + ChatRepository.cs  (MySQL + EF Core)
    Agents/AgentService.cs                           (Microsoft Agent Framework)

PetWorld.Web             ← Blazor Server, zależy od Application
    Components/Pages/Chat.razor
    Components/Pages/History.razor
```

## System AI — Writer-Critic

Każde pytanie przechodzi przez pętlę (max 3 iteracje):

1. **Writer Agent** (`AIAgent` z MAF) — generuje odpowiedź z rekomendacją produktu z katalogu
2. **Critic Agent** (`AIAgent` z MAF) — ocenia odpowiedź, zwraca `{"approved": bool, "feedback": "..."}`
3. Jeśli `approved = false` — Writer dostaje feedback i pisze odpowiedź ponownie
4. Po max 3 iteracjach (lub po zatwierdzeniu) — zapis do MySQL i wyświetlenie w UI

## Strony

- `/chat` — chat z asystentem PetWorld (pole tekstowe + przycisk Wyślij + odpowiedź z licznikiem iteracji)
- `/historia` — historia rozmów (DataGrid: Data, Pytanie, Odpowiedź, Iteracje)

## Konfiguracja

| Parametr | Lokalizacja | Opis |
|---|---|---|
| `OpenAI:ApiKey` | `appsettings.json` | Klucz API OpenAI |
| `OpenAI:Model` | `appsettings.json` | Model (domyślnie `gpt-4o-mini`) |
| `ConnectionStrings:Default` | env/docker-compose | Connection string MySQL |

## Stack technologiczny

- **.NET 8** — Blazor Server
- **Microsoft.Agents.AI 1.11.0** — framework dla agentów AI
- **MediatR 12** — CQRS pattern w warstwie Application
- **EF Core 8 + Pomelo MySQL** — warstwa persystencji
- **MudBlazor** — komponenty UI
- **Docker Compose** — uruchomienie jedną komendą
