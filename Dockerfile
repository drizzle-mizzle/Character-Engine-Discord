FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY /src .

WORKDIR /submodules
COPY /submodules .

WORKDIR /src
RUN dotnet publish -c Release -o "/src/publish"


FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /src/publish .

RUN chmod +x CharacterEngineDiscord

ENTRYPOINT ["/app/CharacterEngineDiscord"]