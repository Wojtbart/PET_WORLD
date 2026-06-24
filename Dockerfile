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
