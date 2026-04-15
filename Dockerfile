#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Build stage
FROM marathonpetroleum/dotnet-sdk:8.0 AS build
WORKDIR /src

# Copy NuGet config first for better layer caching
COPY Infra/CI/config/telerik/nuget.config ./nuget.config

# Copy all project files needed for restore (in dependency order)
COPY MPC.PlanSched.Shared/MPC.PlanSched.Shared.Common/*.csproj ./MPC.PlanSched.Shared/MPC.PlanSched.Shared.Common/
COPY MPC.PlanSched.Shared/MPC.PlanSched.Shared.Notification/*.csproj ./MPC.PlanSched.Shared/MPC.PlanSched.Shared.Notification/
COPY MPC.PlanSched.Shared/MPC.PlanSched.Shared.Semantic/*.csproj ./MPC.PlanSched.Shared/MPC.PlanSched.Shared.Semantic/
COPY MPC.PlanSched.Shared/MPC.PlanSched.Shared.Service.Schema/*.csproj ./MPC.PlanSched.Shared/MPC.PlanSched.Shared.Service.Schema/

# Copy MPC.PlanSched project files (dependencies of UI project)
COPY MPC.PlanSched/MPC.PlanSched.Common/*.csproj ./MPC.PlanSched/MPC.PlanSched.Common/
COPY MPC.PlanSched/MPC.PlanSched.Model/*.csproj ./MPC.PlanSched/MPC.PlanSched.Model/
COPY MPC.PlanSched/MPC.PlanSched.ORM/*.csproj ./MPC.PlanSched/MPC.PlanSched.ORM/
COPY MPC.PlanSched/MPC.PlanSched.Service/*.csproj ./MPC.PlanSched/MPC.PlanSched.Service/

# Copy main UI project file
COPY MPC.PlanSched/MPC.PlanSched.UI/*.csproj ./MPC.PlanSched/MPC.PlanSched.UI/

# Restore dependencies
WORKDIR /src/MPC.PlanSched/MPC.PlanSched.UI
RUN --mount=type=secret,id=feed-accesstoken \
    export FEED_ACCESSTOKEN=$(cat /run/secrets/feed-accesstoken) \
    && export DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0 \
    && export NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED=true \
    && export VSS_NUGET_EXTERNAL_FEED_ENDPOINTS="{\"endpointCredentials\": [{\"endpoint\":\"https://marathonpetroleum.pkgs.visualstudio.com/_packaging/MPC_Dependencies/nuget/v3/index.json\", \"username\":\"docker\", \"password\":\"${FEED_ACCESSTOKEN}\"}, {\"endpoint\":\"https://pkgs.dev.azure.com/marathonpetroleum/_packaging/Telerik_Container_Feed/nuget/v3/index.json\", \"username\":\"docker\", \"password\":\"${FEED_ACCESSTOKEN}\"}]}" \
    && dotnet restore "MPC.PlanSched.UI.csproj" \
        --configfile "/src/nuget.config" \
        --verbosity detailed \
    && rm "/src/nuget.config"

# Copy everything else and build
WORKDIR /src
COPY . .
WORKDIR "/src/MPC.PlanSched/MPC.PlanSched.UI"
RUN dotnet build "MPC.PlanSched.UI.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "MPC.PlanSched.UI.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# Final runtime stage
FROM marathonpetroleum/dotnet-aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443
ENV ASPNETCORE_URLS=http://*:8080

# Copy the published application
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "MPC.PlanSched.UI.dll"]
