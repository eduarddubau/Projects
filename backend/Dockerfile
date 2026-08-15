# STAGE 1: Base/Dev
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

RUN mkdir -p /tmp/dotnet_home /tmp/nuget_cache /tmp/build && \
    chmod -R 777 /tmp/dotnet_home /tmp/nuget_cache /tmp/build

ENV DOTNET_CLI_HOME=/tmp/dotnet_home
ENV NUGET_PACKAGES=/tmp/nuget_cache

RUN dotnet tool install dotnet-ef --tool-path /usr/local/share/dotnet-tools
ENV PATH="${PATH}:/usr/local/share/dotnet-tools"

# Context is the repo root: backend/Directory.Build.props imports the one above
# it, so a backend-only context fails restore with MSB4020.
COPY ["Directory.Build.props", "global.json", ".editorconfig", "./"]
COPY ["backend/backend.csproj", "backend/Directory.Build.props", "backend/"]
RUN dotnet restore "backend/backend.csproj"

COPY backend/ backend/
WORKDIR /src/backend
RUN dotnet build "backend.csproj" -c Debug -o /app/build

# STAGE 2: Publish (Production only)
FROM build AS publish
RUN dotnet publish "backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# STAGE 3: Final Runtime (Production only)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
EXPOSE 8080
USER root
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
USER $APP_UID
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Backend.dll"]
