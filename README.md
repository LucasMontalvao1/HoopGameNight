<p align="center">
  <h1 align="center">🏀 Hoop Game Night</h1>
  <p align="center">
    <strong>Plataforma completa para acompanhamento de jogos da NBA em tempo real</strong>
  </p>
  <p align="center">
    <a href="#-tecnologias">Tecnologias</a> •
    <a href="#-arquitetura">Arquitetura</a> •
    <a href="#-instalação">Instalação</a> •
    <a href="#-endpoints">Endpoints</a> •
    <a href="#-testes">Testes</a> •
    <a href="#-deploy">Deploy</a>
  </p>
  <p align="center">
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8"/>
    <img src="https://img.shields.io/badge/Angular-20-DD0031?style=for-the-badge&logo=angular&logoColor=white" alt="Angular 20"/>
    <img src="https://img.shields.io/badge/MySQL-8.0-4479A1?style=for-the-badge&logo=mysql&logoColor=white" alt="MySQL"/>
    <img src="https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis&logoColor=white" alt="Redis"/>
    <img src="https://img.shields.io/badge/Docker-Ready-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker"/>
  </p>
</p>

---

## 📋 Sobre o Projeto

**Hoop Game Night** é uma aplicação full-stack desenvolvida para fãs de basquete que desejam acompanhar jogos da NBA, estatísticas de jogadores, rankings de times e muito mais — tudo em tempo real.

A plataforma consome dados da **ESPN API**, sincroniza automaticamente via **Hangfire**, entrega atualizações instantâneas via **SignalR** e conta com um **assistente de IA** integrado (powered by Groq) para responder perguntas sobre basquete.

### ✨ Principais Funcionalidades

| Funcionalidade | Descrição |
|:---|:---|
| 🏟️ **Dashboard em Tempo Real** | Visualização dos jogos do dia com placar ao vivo via SignalR |
| 📊 **Estatísticas de Jogadores** | Stats por jogo, temporada e carreira com gráficos interativos |
| 🏆 **Rankings e Líderes** | Líderes em pontos, assistências, rebotes e mais |
| 🤖 **Coach Assistant (IA)** | Assistente inteligente para perguntas sobre NBA usando Groq |
| ⚡ **Sincronização Automática** | Jobs agendados via Hangfire para dados sempre atualizados |
| 📱 **PWA (Progressive Web App)** | Instalável no celular com suporte offline |
| 🔍 **Health Checks** | Monitoramento completo de saúde da aplicação |
| 🚦 **Rate Limiting** | Proteção contra abuso da API |

---

## 🛠️ Tecnologias

### Backend

| Tecnologia | Versão | Finalidade |
|:---|:---:|:---|
| **.NET** | 8.0 | Framework principal da API |
| **Dapper** | 2.1 | Micro-ORM para acesso a dados |
| **MySQL** | 8.0 | Banco de dados relacional |
| **Redis** | 7.x | Cache distribuído |
| **Hangfire** | 1.8 | Agendamento de jobs em background |
| **SignalR** | — | Comunicação em tempo real (WebSocket) |
| **Serilog** | 9.0 | Logging estruturado |
| **Polly** | 8.6 | Resiliência e retry policies |
| **AutoMapper** | 12.0 | Mapeamento objeto-objeto |
| **FluentValidation** | 12.0 | Validação de dados |
| **Swashbuckle** | 9.0 | Documentação Swagger/OpenAPI |
| **RedLock.net** | 2.3 | Distributed locking com Redis |

### Frontend

| Tecnologia | Versão | Finalidade |
|:---|:---:|:---|
| **Angular** | 20.x | Framework SPA |
| **TypeScript** | 5.8 | Linguagem tipada |
| **Chart.js + ng2-charts** | 4.5 / 10.0 | Gráficos interativos |
| **SignalR Client** | 10.0 | Real-time no frontend |
| **Marked** | 17.0 | Renderização de Markdown (IA) |
| **Angular PWA** | 20.x | Service Worker e cache offline |
| **SCSS** | — | Estilização avançada |

### DevOps & Infraestrutura

| Tecnologia | Finalidade |
|:---|:---|
| **Docker** | Containerização da API |
| **Docker Compose** | Orquestração do Redis + Redis Commander |
| **Render** | Hospedagem em produção (Backend) |
| **Vercel** | Hospedagem em produção (Frontend) |

### Testes

| Tecnologia | Finalidade |
|:---|:---|
| **xUnit** | Framework de testes |
| **Moq** | Mocking de dependências |
| **FluentAssertions** | Assertions expressivas |
| **AutoFixture** | Geração de dados de teste |
| **Testcontainers** | Testes de integração com MySQL real |
| **Coverlet** | Cobertura de código |

---

## 🏗️ Arquitetura

O projeto segue os princípios da **Clean Architecture**, dividido em 4 camadas bem definidas:

```
HoopGameNight/
│
├── 📁 HoopGameNight.Api/              # Camada de Apresentação (API)
│   ├── Configurations/                # Configurações de serviços
│   ├── Constants/                     # Constantes da API
│   ├── Controllers/
│   │   ├── BaseApiController.cs       # Controller base com padrões REST
│   │   └── V1/                        # Controllers versionados (v1)
│   │       ├── GamesController.cs     # Endpoints de jogos
│   │       ├── TeamsController.cs     # Endpoints de times
│   │       ├── PlayersController.cs   # Endpoints de jogadores
│   │       ├── PlayerStatsController.cs
│   │       ├── GameStatsController.cs
│   │       ├── LeadersController.cs   # Endpoints de líderes/rankings
│   │       ├── AskController.cs       # Endpoint do assistente IA
│   │       ├── HealthController.cs    # Health checks detalhados
│   │       └── Admin/                 # Endpoints administrativos
│   ├── Extensions/                    # Extension methods (DI, Pipeline)
│   ├── Filters/                       # Action/Authorization filters
│   ├── HealthChecks/                  # Custom health checks
│   ├── Hubs/
│   │   └── GameHub.cs                 # SignalR Hub para jogos ao vivo
│   ├── Mappings/                      # Perfis do AutoMapper
│   ├── Middleware/                     # Middlewares customizados
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── GlobalExceptionHandlerMiddleware.cs
│   │   ├── RateLimitingMiddleware.cs
│   │   └── RequestLoggingMiddleware.cs
│   ├── Options/                       # Options pattern configs
│   ├── Services/                      # Serviços da camada API
│   ├── Validators/                    # Validadores FluentValidation
│   ├── Program.cs                     # Entry point da aplicação
│   └── appsettings.json               # Configurações
│
├── 📁 HoopGameNight.Core/            # Camada de Domínio
│   ├── Configuration/                 # Configurações de domínio
│   ├── Constants/                     # Constantes de negócio
│   ├── DTOs/                          # Data Transfer Objects
│   │   ├── AI/                        # DTOs do assistente IA
│   │   ├── External/                  # DTOs de APIs externas
│   │   ├── Request/                   # DTOs de entrada
│   │   └── Response/                  # DTOs de saída
│   ├── Enums/                         # Enumerações (PlayerPosition, etc.)
│   ├── Exceptions/                    # Exceções customizadas
│   ├── Extensions/                    # Extension methods de domínio
│   ├── Helpers/                       # Classes utilitárias
│   ├── Interfaces/                    # Contratos (Repositories, Services, AI)
│   ├── Models/
│   │   ├── Configuration/             # Models de configuração
│   │   └── Entities/                  # Entidades de domínio
│   │       ├── Game.cs
│   │       ├── Team.cs
│   │       ├── Player.cs
│   │       ├── PlayerGameStats.cs
│   │       ├── PlayerSeasonStats.cs
│   │       ├── PlayerCareerStats.cs
│   │       └── GamePlay.cs
│   ├── Resources/                     # Arquivos de recursos (keywords JSON)
│   └── Services/                      # Serviços de domínio
│       ├── GameService.cs
│       ├── TeamService.cs
│       ├── PlayerService.cs
│       ├── PlayerStatsService.cs
│       ├── GameStatsService.cs
│       ├── GameSyncService.cs
│       ├── PlayerStatsSyncService.cs
│       ├── BackgroundSyncService.cs
│       ├── EspnParser.cs              # Parser de dados da ESPN
│       └── GroqClient.cs              # Cliente da API Groq (IA)
│
├── 📁 HoopGameNight.Infrastructure/   # Camada de Infraestrutura
│   ├── Data/                          # Inicialização do banco de dados
│   ├── ExternalServices/              # Integrações externas
│   │   ├── EspnApiService.cs          # Consumo da ESPN API
│   │   ├── HttpClientService.cs       # HTTP client com retry
│   │   └── ESPN/                      # Configurações ESPN
│   ├── HealthChecks/                  # Health checks customizados
│   │   ├── CacheHealthCheck.cs
│   │   ├── EspnApiHealthCheck.cs
│   │   └── SyncHealthCheck.cs
│   ├── Jobs/
│   │   └── SyncJobs.cs                # Jobs agendados do Hangfire
│   ├── Monitoring/                    # Métricas e monitoramento
│   ├── Repositories/                  # Implementação dos repositórios
│   │   ├── BaseRepository.cs
│   │   ├── GameRepository.cs
│   │   ├── TeamRepository.cs
│   │   ├── PlayerRepository.cs
│   │   ├── PlayerStatsRepository.cs
│   │   └── GamePlayRepository.cs
│   ├── Scripts/                       # Scripts de migração
│   ├── Services/                      # Serviços de infraestrutura
│   ├── Sql/                           # Queries SQL organizadas
│   │   ├── Database/
│   │   ├── Games/
│   │   ├── Teams/
│   │   ├── Players/
│   │   ├── PlayerStats/
│   │   └── Statistics/
│   └── TypeHandlers/                  # Dapper type handlers
│
├── 📁 HoopGameNight.Tests/           # Testes Automatizados
│   ├── Unit/
│   │   ├── Api/                       # Testes de Controllers e Middleware
│   │   ├── Core/                      # Testes de Services e Models
│   │   └── Infrastructure/            # Testes de Repositories e Data
│   ├── Integration/
│   │   ├── Controllers/               # Testes de integração de endpoints
│   │   └── DatabaseTests.cs           # Testes com Testcontainers
│   ├── Helpers/                       # Test fixtures e builders
│   ├── Resources/                     # Dados de teste (JSON)
│   └── Scripts/                       # Scripts auxiliares para testes
│
├── 📁 HoopGameNight-front/           # Frontend Angular (PWA)
│   └── src/
│       ├── app/
│       │   ├── core/                  # Módulo core (services, interceptors)
│       │   ├── features/              # Feature modules
│       │   │   ├── dashboard/         # Dashboard principal
│       │   │   ├── games/             # Listagem e detalhes de jogos
│       │   │   ├── teams/             # Listagem e detalhes de times
│       │   │   ├── players/           # Listagem e detalhes de jogadores
│       │   │   ├── ask/               # Coach Assistant (IA)
│       │   │   └── api-status/        # Status da API
│       │   ├── layout/               # Componentes de layout
│       │   └── shared/               # Componentes compartilhados
│       ├── environments/             # Configurações por ambiente
│       ├── styles/                   # Estilos globais SCSS
│       └── assets/                   # Recursos estáticos
│
├── 📄 Dockerfile                     # Build multi-stage .NET 8
├── 📄 docker-compose.redis.yml       # Redis + Redis Commander
├── 📄 .env                           # Variáveis de ambiente
└── 📄 HoopGameNight.sln              # Solution file
```

### Diagrama de Camadas

```
┌──────────────────────────────────────────────────────────┐
│                    🎨 Frontend (Angular)                 │
│        Dashboard │ Games │ Teams │ Players │ Ask AI      │
├──────────────────────────────────────────────────────────┤
│                     ↕ HTTP / SignalR                      │
├──────────────────────────────────────────────────────────┤
│                    📡 HoopGameNight.Api                   │
│   Controllers │ Middleware │ Hubs │ Filters │ HealthCheck │
├──────────────────────────────────────────────────────────┤
│                   💎 HoopGameNight.Core                   │
│    Services │ Models │ Interfaces │ DTOs │ Validators     │
├──────────────────────────────────────────────────────────┤
│              🔧 HoopGameNight.Infrastructure              │
│  Repositories │ ESPN API │ Jobs │ Cache │ SQL │ Redis     │
├──────────────────────────────────────────────────────────┤
│             🗄️ MySQL 8.0  │  ⚡ Redis 7  │  🌐 ESPN API  │
└──────────────────────────────────────────────────────────┘
```

---

## 📌 Pré-requisitos

Antes de iniciar, certifique-se de ter as seguintes ferramentas instaladas:

| Ferramenta | Versão Mínima | Link |
|:---|:---:|:---|
| **.NET SDK** | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Node.js** | 18+ | [Download](https://nodejs.org/) |
| **Angular CLI** | 20+ | `npm install -g @angular/cli` |
| **MySQL** | 8.0+ | [Download](https://dev.mysql.com/downloads/) |
| **Docker** *(opcional)* | 20+ | [Download](https://www.docker.com/get-started) |
| **Redis** *(opcional)* | 7+ | Via Docker Compose ou [Download](https://redis.io/) |

---

## 🚀 Instalação

### 1. Clonar o Repositório

```bash
git clone https://github.com/LucasMontalvao1/HoopGameNight.git
cd HoopGameNight
```

### 2. Configurar o Banco de Dados

Certifique-se de que o MySQL está rodando na sua máquina.

```sql
-- Criar o banco de dados
CREATE DATABASE hoop_game_night;
```

> **💡 Nota:** O banco de dados é inicializado automaticamente pela aplicação ao iniciar. A classe `DatabaseInitializer` executa os scripts SQL localizados em `HoopGameNight.Infrastructure/Sql/` para criar tabelas, índices e triggers.

### 3. Configurar Variáveis de Ambiente

Edite o arquivo `.env` na raiz do projeto:

```env
# Database Configuration
DB_SERVER=127.0.0.1
DB_PORT=3306
DB_NAME=hoop_game_night
DB_USER=root
DB_PASSWORD=sua_senha_aqui

# Environment
ASPNETCORE_ENVIRONMENT=Development
```

> ⚠️ **Importante:** Nunca commite senhas reais no `.env`. Utilize `.env.example` como template.

### 4. Configurar e Executar o Backend

```bash
# Navegar para a pasta da API
cd HoopGameNight.Api

# Restaurar dependências
dotnet restore

# Compilar o projeto
dotnet build

# Executar a aplicação
dotnet run
```

A API estará disponível em:
- **Swagger UI:** http://localhost:5214
- **Health Check:** http://localhost:5214/health
- **Hangfire Dashboard:** http://localhost:5214/hangfire

### 5. Configurar e Executar o Frontend

```bash
# Navegar para a pasta do frontend
cd HoopGameNight-front

# Instalar dependências
npm install

# Executar em modo de desenvolvimento
ng serve
```

O frontend estará disponível em: **http://localhost:4200**

### 6. Redis (Opcional — Cache Distribuído)

Para habilitar o cache distribuído com Redis, use o Docker Compose incluso:

```bash
# Na raiz do projeto
docker-compose -f docker-compose.redis.yml up -d
```

Isso levantará:
- **Redis** na porta `6379`
- **Redis Commander** (interface web) na porta `8081` → http://localhost:8081

---

## 🐳 Docker (Deploy)

### Build e execução da API via Docker

```bash
# Build da imagem
docker build -t hoopgamenight-api .

# Executar o container
docker run -p 8080:8080 \
  -e DB_SERVER=host.docker.internal \
  -e DB_PORT=3306 \
  -e DB_NAME=hoop_game_night \
  -e DB_USER=root \
  -e DB_PASSWORD=sua_senha \
  hoopgamenight-api
```

---

## 📡 Endpoints Principais

### Sistema

| Método | Rota | Descrição |
|:---:|:---|:---|
| `GET` | `/api/info` | Informações da API (versão, uptime, features) |
| `GET` | `/api/status` | Status rápido da aplicação |
| `GET` | `/api/metrics` | Métricas de cache e sincronização |
| `GET` | `/health` | Health check completo |
| `GET` | `/health/ready` | Readiness probe |
| `GET` | `/health/live` | Liveness probe |

### Jogos (`/api/v1/games`)

| Método | Rota | Descrição |
|:---:|:---|:---|
| `GET` | `/api/v1/games/today` | Jogos do dia |
| `GET` | `/api/v1/games/live` | Jogos ao vivo |
| `GET` | `/api/v1/games/{id}` | Detalhes de um jogo |
| `GET` | `/api/v1/games/date/{date}` | Jogos por data |

### Times (`/api/v1/teams`)

| Método | Rota | Descrição |
|:---:|:---|:---|
| `GET` | `/api/v1/teams` | Listar todos os times |
| `GET` | `/api/v1/teams/{abbreviation}` | Detalhes de um time |

### Jogadores (`/api/v1/players`)

| Método | Rota | Descrição |
|:---:|:---|:---|
| `GET` | `/api/v1/players` | Listar jogadores |
| `GET` | `/api/v1/players/{id}` | Detalhes de um jogador |

### Estatísticas (`/api/v1/player-stats`)

| Método | Rota | Descrição |
|:---:|:---|:---|
| `GET` | `/api/v1/player-stats/{playerId}` | Stats de um jogador |
| `GET` | `/api/v1/leaders` | Líderes em estatísticas |

### Assistente IA (`/api/v1/ask`)

| Método | Rota | Descrição |
|:---:|:---|:---|
| `POST` | `/api/v1/ask` | Perguntar ao Coach Assistant |

### SignalR Hub

| Hub | Rota | Evento |
|:---:|:---|:---|
| `GameHub` | `/hubs/games` | `ReceiveGameUpdates` |

---

## ⚡ Comunicação em Tempo Real

O projeto utiliza **SignalR** para enviar atualizações de jogos ao vivo ao frontend:

```
Cliente (Angular) ←→ SignalR WebSocket ←→ GameHub (Backend)
                                              ↕
                                      Hangfire SyncJobs
                                              ↕
                                         ESPN API
```

Os clientes se conectam ao hub `/hubs/games` e recebem o evento `ReceiveGameUpdates` automaticamente quando novos dados são sincronizados.

---

## 🔄 Jobs em Background (Hangfire)

| Job | Frequência | Descrição |
|:---|:---:|:---|
| `sync-games` | A cada 6 horas | Sincroniza jogos da ESPN |
| `sync-live-games` | A cada 15 minutos | Atualiza placar de jogos ao vivo |
| `sync-player-stats` | A cada 12 horas | Sincroniza estatísticas de jogadores |
| `dawn-master-sync` | 03:00 AM (diário) | Sincronização completa de madrugada |

Acesse o **Hangfire Dashboard** em `/hangfire` para monitorar execuções, filas e falhas.

---

## 🔒 Segurança

A API implementa diversas camadas de segurança:

- **Security Headers (OWASP):** `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`
- **Rate Limiting:** 100 requisições por minuto por IP (configurável)
- **CORS:** Origens permitidas configuráveis por ambiente
- **Correlation ID:** Rastreamento de requisições end-to-end
- **Global Exception Handler:** Tratamento centralizado de erros sem vazamento de stack traces

---

## 📊 Logs e Monitoramento

### Serilog

O projeto utiliza **Serilog** com saída para Console e Arquivo:

```bash
# Logs ficam em:
HoopGameNight.Api/logs/app-YYYY-MM-DD.txt

# Visualizar logs
cat HoopGameNight.Api/logs/app-2026-04-23.txt
```

**Configurações de log:**
- Retenção: **7 dias** (rolling por dia)
- Nível padrão: `Information`
- Enriquecido com: `CorrelationId`, `MachineName`, `ThreadId`

### Health Checks

A aplicação expõe endpoints de saúde para monitoramento:

| Endpoint | Descrição |
|:---|:---|
| `/health` | Status completo (MySQL, Redis, ESPN, Cache, Sync) |
| `/health/ready` | Readiness — serviços prontos para receber tráfego |
| `/health/live` | Liveness — aplicação está viva |

**Health Checks implementados:**
- ✅ MySQL connectivity
- ✅ ESPN API availability
- ✅ Cache service status
- ✅ Sync service status

---

## 🧪 Testes

### Executar Testes

```bash
# Navegar para o diretório de testes
cd HoopGameNight.Tests

# Restaurar e compilar
dotnet restore
dotnet build

# Executar todos os testes
dotnet test

# Com relatório de cobertura
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
```

### Tipos de Testes

| Tipo | Diretório | Ferramentas |
|:---|:---|:---|
| **Unitários** | `Unit/Api/`, `Unit/Core/`, `Unit/Infrastructure/` | xUnit, Moq, AutoFixture |
| **Integração** | `Integration/` | Testcontainers (MySQL real), WebApplicationFactory |

### Gerar Relatório de Cobertura

```bash
# Gerar relatório HTML
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Visualizar com ReportGenerator
reportgenerator -reports:coverage.cobertura.xml -targetdir:coveragereport
```

---

## 🌐 Deploy em Produção

| Serviço | Plataforma | URL |
|:---|:---:|:---|
| **Backend API** | Render | `https://hoopgamenight.onrender.com` |
| **Frontend** | Vercel | `https://hoop-game-night.vercel.app` |

---

## 🔧 Troubleshooting

<details>
<summary><strong>❌ Erro de conexão com MySQL</strong></summary>

Verifique se:
1. O MySQL está rodando: `mysqladmin -u root -p status`
2. As variáveis no `.env` estão corretas
3. O banco `hoop_game_night` existe: `SHOW DATABASES;`
4. O charset está como `utf8mb4`
</details>

<details>
<summary><strong>❌ Redis não conecta</strong></summary>

1. Verifique se o Docker está rodando: `docker ps`
2. Suba o Redis: `docker-compose -f docker-compose.redis.yml up -d`
3. A aplicação funciona sem Redis (fallback para MemoryCache)
</details>

<details>
<summary><strong>❌ Frontend não conecta ao Backend</strong></summary>

1. Verifique a URL no `environment.ts`: deve apontar para `https://localhost:7039`
2. Certifique-se de que o backend está rodando
3. Verifique se o CORS está configurado para `http://localhost:4200`
</details>

<details>
<summary><strong>❌ Hangfire falha na inicialização</strong></summary>

Certifique-se de usar o pacote `Hangfire.Storage.MySql` (versão 2.1.0-beta), compatível com .NET 8 e MySQL 8. O pacote antigo `Hangfire.MySqlStorage` causa erros de coluna ambígua.
</details>

---

## 🚀 Possíveis Melhorias Futuras

- [ ] 🔐 **Autenticação JWT** — Login de usuários com tokens
- [ ] 📲 **Push Notifications** — Alertas de início de jogo via Service Worker
- [ ] 🏆 **Sistema de Apostas (simulado)** — Palpites entre amigos
- [ ] 📈 **Dashboards Comparativos** — Comparar jogadores lado a lado
- [ ] 🗓️ **Calendário de Temporada** — Visualização completa da season
- [ ] 🌍 **Internacionalização (i18n)** — Suporte a múltiplos idiomas
- [ ] 📊 **Grafana + Prometheus** — Observabilidade avançada
- [ ] 🧪 **Testes E2E** — Cypress ou Playwright para fluxos completos
- [ ] 🔄 **CI/CD Pipeline** — GitHub Actions para build, test e deploy automático

---

## 👨‍💻 Autor

<table>
  <tr>
    <td align="center">
      <a href="https://github.com/LucasMontalvao1">
        <img src="https://github.com/LucasMontalvao1.png" width="100px;" alt="Lucas Montalvão"/><br />
        <sub><b>Lucas Montalvão</b></sub>
      </a>
    </td>
  </tr>
</table>

📧 **Contato:** Lucas@hoopgamenight.com  
🔗 **GitHub:** [github.com/LucasMontalvao1](https://github.com/LucasMontalvao1)

---

<p align="center">
  Feito com ❤️ e ☕ por <strong>Lucas Montalvão</strong>
</p>
