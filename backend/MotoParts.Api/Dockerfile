# См. статью по ссылке https://aka.ms/customizecontainer, чтобы узнать как настроить контейнер отладки и как Visual Studio использует этот Dockerfile для создания образов для ускорения отладки.

# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["backend/MotoParts.Api/MotoParts.Api.csproj", "backend/MotoParts.Api/"]
RUN dotnet restore "./backend/MotoParts.Api/MotoParts.Api.csproj"
COPY . .
WORKDIR "/src/backend/MotoParts.Api"
RUN dotnet build "./MotoParts.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MotoParts.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Создаём папку под фото ДО монтирования volume: том с уже существующим
# в образе каталогом при первом запуске наследует его владельца ($APP_UID,
# см. USER выше) — без этого шага volume мог бы примонтироваться как root,
# и AdminController не смог бы писать сюда файлы от непривилегированного пользователя.
RUN mkdir -p wwwroot/ZipPhotos

ENTRYPOINT ["dotnet", "MotoParts.Api.dll"]