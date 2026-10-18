# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY Store.sln ./
COPY src/Store.Domain/Store.Domain.csproj src/Store.Domain/
COPY src/Store.Application/Store.Application.csproj src/Store.Application/
COPY src/Store.Infrastructure/Store.Infrastructure.csproj src/Store.Infrastructure/
COPY src/Store.Api/Store.Api.csproj src/Store.Api/
RUN dotnet restore src/Store.Api/Store.Api.csproj

COPY src/ src/
RUN dotnet publish src/Store.Api/Store.Api.csproj -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app ./
USER app
ENTRYPOINT ["dotnet", "Store.Api.dll"]
