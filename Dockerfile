FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build
WORKDIR /app

COPY *.csproj .
COPY *.sln .
RUN dotnet restore ./CharacterEngineDiscord.sln

COPY . .
RUN dotnet publish ./Character-Engine-Discord.csproj -c Release -o out --self-contained true

FROM mcr.microsoft.com/dotnet/runtime:7.0-bullseye-slim AS runtime
COPY --from=build /app/out/ ./
COPY config.json ./
COPY storage ./

RUN apt-get update -y && apt-get install -y libgtk-3-dev libnotify-dev libgconf-2-4 libnss3 libxss1 libasound2

RUN echo '#!/bin/bash \n ./Character-Engine-Discord' > ./entrypoint.sh
RUN chmod +x ./entrypoint.sh
ENTRYPOINT ["./entrypoint.sh"]
VOLUME /storage
