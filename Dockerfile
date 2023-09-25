FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal AS base
WORKDIR /app
EXPOSE 5091
EXPOSE 5092

ENV ASPNETCORE_URLS=http://+:5091
ENV ASPNETCORE_ENVIRONMENT=Development

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS build
WORKDIR /
COPY ["OFM.Infrastructure.WebAPI/OFM.Infrastructure.WebAPI.csproj", "OFM.Infrastructure.WebAPI/"]
RUN dotnet restore "OFM.Infrastructure.WebAPI/OFM.Infrastructure.WebAPI.csproj"
COPY . .
WORKDIR "/OFM.Infrastructure.WebAPI"
RUN dotnet build "OFM.Infrastructure.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OFM.Infrastructure.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OFM.Infrastructure.WebAPI.dll"]