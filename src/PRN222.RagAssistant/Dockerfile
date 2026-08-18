FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet tool restore \
    && dotnet libman restore \
    && dotnet restore src/PRN222.RagAssistant/PRN222.RagAssistant.csproj \
    && dotnet publish src/PRN222.RagAssistant/PRN222.RagAssistant.csproj \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

# Render injects PORT (normally 10000). Local Docker/Compose keeps the existing
# 8080 behavior when PORT is not present.
ENTRYPOINT ["sh", "-c", "exec dotnet PRN222.RagAssistant.dll --urls http://0.0.0.0:${PORT:-8080}"]
