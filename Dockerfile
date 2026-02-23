# ── Stage 1: Build ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Download Tailwind CSS standalone CLI and daisyUI for layer caching
RUN mkdir -p src/Sigil.Server/Tools src/Sigil.Server/Styles \
 && curl -fsSLo src/Sigil.Server/Tools/tailwindcss \
      "https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-x64" \
 && chmod +x src/Sigil.Server/Tools/tailwindcss \
 && curl -fsSLo src/Sigil.Server/Styles/daisyui.mjs \
      "https://github.com/saadeghi/daisyui/releases/latest/download/daisyui.mjs" \
 && curl -fsSLo src/Sigil.Server/Styles/daisyui-theme.mjs \
      "https://github.com/saadeghi/daisyui/releases/latest/download/daisyui-theme.mjs"

# Copy project files first for layer caching
COPY src/Sigil.Domain/Sigil.Domain.csproj                   src/Sigil.Domain/
COPY src/Sigil.Application/Sigil.Application.csproj         src/Sigil.Application/
COPY src/Sigil.Infrastructure/Sigil.Infrastructure.csproj   src/Sigil.Infrastructure/
COPY src/Sigil.Server.Client/Sigil.Server.Client.csproj     src/Sigil.Server.Client/
COPY src/Sigil.Server/Sigil.Server.csproj                   src/Sigil.Server/
COPY src/Sigil.Server/Build/                                src/Sigil.Server/Build/

RUN dotnet restore src/Sigil.Server/Sigil.Server.csproj

# Copy all source
COPY src/ src/

# Publish
RUN dotnet publish src/Sigil.Server/Sigil.Server.csproj \
      -c Release \
      -o /app/publish \
      --no-restore


# ── Stage 2: Runtime ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Sigil.Server.dll"]
