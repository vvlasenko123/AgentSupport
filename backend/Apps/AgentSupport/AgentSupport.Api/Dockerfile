FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG BUILD_CONFIGURATION=Release

COPY ["backend/Apps/AgentSupport/AgentSupport.Api/AgentSupport.Api.csproj", "backend/Apps/AgentSupport/AgentSupport.Api/"]
COPY ["backend/Apps/AgentSupport/AgentSupport.Application/AgentSupport.Application.csproj", "backend/Apps/AgentSupport/AgentSupport.Application/"]
COPY ["backend/Apps/AgentSupport/AgentSupport.Domain/AgentSupport.Domain.csproj", "backend/Apps/AgentSupport/AgentSupport.Domain/"]
COPY ["backend/Apps/AgentSupport/AgentSupport.Infrastructure/AgentSupport.Infrastructure.csproj", "backend/Apps/AgentSupport/AgentSupport.Infrastructure/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Broker/Infrastructure.Broker.csproj", "backend/Libs/Infrastructure/Infrastructure.Broker/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Database/Infrastructure.Database.csproj", "backend/Libs/Infrastructure/Infrastructure.Database/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Hosting/Infrastructure.Hosting.csproj", "backend/Libs/Infrastructure/Infrastructure.Hosting/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Logging/Infrastructure.Logging.csproj", "backend/Libs/Infrastructure/Infrastructure.Logging/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Mapper/Infrastructure.Mapper.csproj", "backend/Libs/Infrastructure/Infrastructure.Mapper/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.MinIO/Infrastructure.MinIO.csproj", "backend/Libs/Infrastructure/Infrastructure.MinIO/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Options/Infrastructure.Options.csproj", "backend/Libs/Infrastructure/Infrastructure.Options/"]
COPY ["backend/Libs/Infrastructure/Infrastructure.Swagger/Infrastructure.Swagger.csproj", "backend/Libs/Infrastructure/Infrastructure.Swagger/"]

RUN dotnet restore "backend/Apps/AgentSupport/AgentSupport.Api/AgentSupport.Api.csproj"

COPY . .

WORKDIR "/src/backend/Apps/AgentSupport/AgentSupport.Api"

RUN dotnet publish "AgentSupport.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    librdkafka1 \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AgentSupport.Api.dll"]