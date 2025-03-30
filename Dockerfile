# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src
COPY /src .

WORKDIR /submodules
COPY /submodules .

WORKDIR /src/CharacterEngineDiscord.Migrator
RUN dotnet build
RUN dotnet run

WORKDIR /src
RUN dotnet publish -c Release -o "/src/publish"


# publish
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS publish
WORKDIR /app
COPY --from=build /src/publish .

RUN chmod +x CharacterEngineDiscord


# launch
ENTRYPOINT ["/app/CharacterEngineDiscord"]