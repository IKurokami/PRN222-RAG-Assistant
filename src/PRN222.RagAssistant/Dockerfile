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

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PRN222.RagAssistant.dll"]
