# STAGE 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Clear the workload warning
RUN dotnet workload update

# Install EF Core Global Tool
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY ["backend.csproj", "./"]
RUN dotnet restore "backend.csproj"

COPY . .

RUN dotnet build "backend.csproj" -c Debug -o /app/build

# STAGE 2: Publish
FROM build AS publish
RUN dotnet publish "backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# STAGE 3: Final Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
EXPOSE 8080

# System-level dependencies
USER root
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# Use the default non-root user
USER $APP_UID

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "backend.dll"]
