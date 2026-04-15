FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to improve layer caching
COPY Walos.sln ./
COPY src/Walos.API/Walos.API.csproj src/Walos.API/
COPY src/Walos.Application/Walos.Application.csproj src/Walos.Application/
COPY src/Walos.Domain/Walos.Domain.csproj src/Walos.Domain/
COPY src/Walos.Infrastructure/Walos.Infrastructure.csproj src/Walos.Infrastructure/
COPY tests/Walos.Tests/Walos.Tests.csproj tests/Walos.Tests/

RUN dotnet restore Walos.sln

# Copy the rest of the source and publish the API
COPY . .
RUN dotnet publish src/Walos.API/Walos.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Walos.API.dll"]
