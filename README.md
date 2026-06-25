# PetWorld

Sklep zoologiczny z asystentem AI opartym na systemie **Writer-Critic** (Microsoft Agent Framework).
Blazor Server · Onion Architecture · MySQL · Docker

---

## Uruchomienie

> **Wymagania:** Docker Desktop + klucz API OpenAI w `PetWorld.Web/appsettings.json`

```bash
docker compose up
```

Aplikacja: **http://localhost:5000**

---

## Architektura

Onion/Clean Architecture — 4 warstwy, zależności tylko do centrum:

```
Domain          ← zero zewnętrznych zależności (encje, interfejsy)
Application     ← logika biznesowa, CQRS z MediatR
Infrastructure  ← MySQL (EF Core), Microsoft Agent Framework
Web             ← Blazor Server UI (MudBlazor)
```

## System AI — Writer-Critic (max 3 iteracje)

```
Pytanie → Writer (generuje odpowiedź)
              ↓
         Critic (ocenia: approved/feedback)
              ↓
     tak → zwróć odpowiedź
     nie → Writer poprawia z feedbackiem
```

## Stack

| Technologia | Rola |
|---|---|
| .NET 8 Blazor Server | UI + hosting |
| Microsoft.Agents.AI 1.11.0 | Agenty Writer i Critic |
| MediatR 12 | CQRS (Commands/Queries) |
| EF Core 8 + Pomelo | Persystencja MySQL |
| MudBlazor 9.5 | Komponenty UI |
| Docker Compose | Deployment jedną komendą |
