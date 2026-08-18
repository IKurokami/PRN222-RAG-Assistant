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

# Npgsql probes the system GSSAPI/Kerberos library during startup. The slim
# ASP.NET runtime image does not include it by default, which produces noisy
# libgssapi_krb5.so.2 load errors on Render even when password auth succeeds.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080

COPY --from=build /app/publish .

# Render injects PORT (normally 10000). Local Docker/Compose keeps the existing
# 8080 behavior when PORT is not present.
ENTRYPOINT ["sh", "-c", "exec dotnet PRN222.RagAssistant.dll --urls http://0.0.0.0:${PORT:-8080}"]
