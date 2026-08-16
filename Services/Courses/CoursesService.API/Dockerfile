FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Courses/CoursesService.API/CoursesService.API.csproj", "Services/Courses/CoursesService.API/"]
RUN dotnet restore "Services/Courses/CoursesService.API/CoursesService.API.csproj"
COPY . .
WORKDIR "/src/Services/Courses/CoursesService.API"
RUN dotnet build "./CoursesService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CoursesService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CoursesService.API.dll"]
