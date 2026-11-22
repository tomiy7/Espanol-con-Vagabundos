FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Prvo samo csproj fajlovi, da se restore kesira dok se zavisnosti ne promene.
COPY ["Services/Groups/GroupsService.API/GroupsService.API.csproj", "Services/Groups/GroupsService.API/"]
COPY ["Services/Groups/GroupsService.Application/GroupsService.Application.csproj", "Services/Groups/GroupsService.Application/"]
COPY ["Services/Groups/GroupsService.Domain/GroupsService.Domain.csproj", "Services/Groups/GroupsService.Domain/"]
COPY ["Services/Groups/GroupsService.Infrastructure/GroupsService.Infrastructure.csproj", "Services/Groups/GroupsService.Infrastructure/"]
RUN dotnet restore "Services/Groups/GroupsService.API/GroupsService.API.csproj"
COPY ["Services/Groups/GroupsService.API/", "Services/Groups/GroupsService.API/"]
COPY ["Services/Groups/GroupsService.Application/", "Services/Groups/GroupsService.Application/"]
COPY ["Services/Groups/GroupsService.Domain/", "Services/Groups/GroupsService.Domain/"]
COPY ["Services/Groups/GroupsService.Infrastructure/", "Services/Groups/GroupsService.Infrastructure/"]
WORKDIR "/src/Services/Groups/GroupsService.API"
RUN dotnet build "./GroupsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GroupsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GroupsService.API.dll"]
