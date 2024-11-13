# Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /app
COPY . /app

# Install EF Core CLI tools globally
RUN dotnet tool install --global dotnet-ef

# Set the PATH to ensure dotnet-ef is available
ENV PATH="${PATH}:/root/.dotnet/tools"

# Build the application
RUN dotnet publish src/CharacterEngineDiscord/CharacterEngineDiscord.csproj -c Release -o out

# Create startup script with correct migration parameters
RUN echo '#!/bin/bash\n\
set -e\n\
\n\
# Check if migrations exist\n\
if [ ! -d "src/CharacterEngineDiscord.Models/Migrations" ]; then\n\
    echo "No migrations found. Creating initial migration..."\n\
    dotnet ef migrations add InitialCreate \\\n\
        --project src/CharacterEngineDiscord.Models/CharacterEngineDiscord.Models.csproj \\\n\
        --startup-project src/CharacterEngineDiscord/CharacterEngineDiscord.csproj\n\
fi\n\
\n\
# Apply any pending migrations\n\
echo "Applying any pending migrations..."\n\
dotnet ef database update \\\n\
    --project src/CharacterEngineDiscord.Models/CharacterEngineDiscord.Models.csproj \\\n\
    --startup-project src/CharacterEngineDiscord/CharacterEngineDiscord.csproj \\\n\
    --connection "$DATABASE_CONNECTION_STRING"\n\
\n\
# Start the application\n\
echo "Starting application..."\n\
exec dotnet out/CharacterEngineDiscord.dll' > /app/start.sh && \
    chmod +x /app/start.sh

# Use the script as the entry point
ENTRYPOINT ["/app/start.sh"]