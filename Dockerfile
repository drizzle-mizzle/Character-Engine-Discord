FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build
WORKDIR /app

COPY *.csproj .
RUN dotnet restore ./Character-Engine-Discord.csproj

COPY . .
RUN dotnet publish -c Release -o out --self-contained true

FROM mcr.microsoft.com/dotnet/runtime:7.0-bullseye-slim AS runtime
WORKDIR /app
COPY --from=build /app/out/ ./

RUN apt-get update -y && apt-get install -y libgtk-3-dev libnotify-dev libgconf-2-4 libnss3 libxss1 libasound2

ENTRYPOINT ["dotnet", "Character-Engine-Discord.dll"]
