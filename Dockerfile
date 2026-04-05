# Base runtime image for .NET 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
# O Render injeta a porta web automaticamente, mas o ASP.NET 8 já mapeia 8080 por padrão
ENV ASPNETCORE_HTTP_PORTS=8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os arquivos de projeto para otimizar o cache de dependências (Restore)
COPY ["HoopGameNight.sln", "./"]
COPY ["HoopGameNight.Api/HoopGameNight.Api.csproj", "HoopGameNight.Api/"]
COPY ["HoopGameNight.Core/HoopGameNight.Core.csproj", "HoopGameNight.Core/"]
COPY ["HoopGameNight.Infrastructure/HoopGameNight.Infrastructure.csproj", "HoopGameNight.Infrastructure/"]
COPY ["HoopGameNight.Tests/HoopGameNight.Tests.csproj", "HoopGameNight.Tests/"]

RUN dotnet restore "HoopGameNight.sln"

# Copia o resto do código (O `.dockerignore` impedirá o NodeModules/FrontEnd de travar a build)
COPY . .
WORKDIR "/src/HoopGameNight.Api"
RUN dotnet build "HoopGameNight.Api.csproj" -c Release -o /app/build

# Publicação (Remoção de lixo de build)
FROM build AS publish
RUN dotnet publish "HoopGameNight.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Imagem Final de Produção 
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Previne crashes de Data/PontoFlutuante em culturas do Render
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Mantém o relógio no horário correto do BR 
ENV TZ=America/Sao_Paulo

ENTRYPOINT ["dotnet", "HoopGameNight.Api.dll"]
