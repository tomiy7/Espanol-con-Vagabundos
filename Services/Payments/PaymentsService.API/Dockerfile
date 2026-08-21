FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Payments/PaymentsService.API/PaymentsService.API.csproj", "Services/Payments/PaymentsService.API/"]
COPY ["Services/Payments/PaymentsService.Application/PaymentsService.Application.csproj", "Services/Payments/PaymentsService.Application/"]
COPY ["Services/Payments/PaymentsService.Domain/PaymentsService.Domain.csproj", "Services/Payments/PaymentsService.Domain/"]
COPY ["Services/Payments/PaymentsService.Infrastructure/PaymentsService.Infrastructure.csproj", "Services/Payments/PaymentsService.Infrastructure/"]
RUN dotnet restore "Services/Payments/PaymentsService.API/PaymentsService.API.csproj"
COPY . .
WORKDIR "/src/Services/Payments/PaymentsService.API"
RUN dotnet build "./PaymentsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PaymentsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentsService.API.dll"]
