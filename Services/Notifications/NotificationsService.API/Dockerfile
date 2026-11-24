FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Prvo samo csproj fajlovi, da se restore kesira dok se zavisnosti ne promene.
COPY ["Services/Notifications/NotificationsService.API/NotificationsService.API.csproj", "Services/Notifications/NotificationsService.API/"]
COPY ["BuildingBlocks/Messaging/Messaging.csproj", "BuildingBlocks/Messaging/"]
RUN dotnet restore "Services/Notifications/NotificationsService.API/NotificationsService.API.csproj"
COPY ["Services/Notifications/NotificationsService.API/", "Services/Notifications/NotificationsService.API/"]
COPY ["BuildingBlocks/Messaging/", "BuildingBlocks/Messaging/"]
WORKDIR "/src/Services/Notifications/NotificationsService.API"
RUN dotnet build "./NotificationsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NotificationsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NotificationsService.API.dll"]
