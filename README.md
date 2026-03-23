# Global Logistics Tracker

Sistema distribuído de rastreamento logístico construído com .NET 10, CQRS, Clean Architecture, Docker Compose e observabilidade com Grafana.

## Visão Geral

O projeto está organizado em microserviços e componentes de suporte:

- `GlobalLogistics.IngestionAPI`: escrita de pacotes e eventos de rastreamento
- `GlobalLogistics.QueryAPI`: leitura de rastreamento
- `GlobalLogistics.AuthAPI`: autenticação JWT
- `GlobalLogistics.Gateway`: API Gateway com YARP
- `GlobalLogistics.Worker`: consumer RabbitMQ
- `SQL Server`: persistência
- `Redis`: cache
- `RabbitMQ`: mensageria
- `Loki`: agregação de logs
- `Grafana`: visualização de logs

## Funcionalidades Implementadas

- Arquitetura CQRS com separação entre escrita e leitura
- Clean Architecture com camadas `Domain`, `Application` e `Infrastructure`
- Autenticação JWT
- API Gateway com YARP exposto em `localhost:5000`
- Rate limiting no Gateway: `100 req/min` por IP
- Health checks por serviço e health check agregado no Gateway
- Logs estruturados com Serilog
- Exportação de logs para Loki
- Provisioning automático de datasource e dashboard no Grafana

## Endpoints via Gateway

Todos os testes de API devem usar a porta `5000`:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/packages`
- `POST /api/tracking`
- `GET /api/tracking/{trackingCode}`
- `GET /health`
- `GET /health/auth`
- `GET /health/ingestion`
- `GET /health/query`

## Stack Técnica

- .NET 10
- ASP.NET Core Minimal APIs e Controllers
- Entity Framework Core
- MediatR
- MassTransit
- SQL Server 2022
- Redis 7
- RabbitMQ 3
- YARP
- Serilog
- Loki
- Grafana
- Docker Compose

## Como Executar

Pré-requisitos:

- Docker
- Docker Compose
- .NET SDK 10.0
- `curl`

Suba toda a stack:

```bash
docker compose up -d --build
```

Verifique os containers:

```bash
docker compose ps
```

Valide a solução localmente:

```bash
dotnet build GlobalLogistics.slnx
```

## Como Testar com curl

### 1. Registrar usuário

```bash
curl -i -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "teste@example.com",
    "password": "Senha@123",
    "confirmPassword": "Senha@123",
    "fullName": "Usuário Teste"
  }'
```

### 2. Fazer login

```bash
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "teste@example.com",
    "password": "Senha@123"
  }'
```

Copie o valor do campo `token` e exporte:

```bash
export TOKEN="COLE_AQUI_O_JWT"
```

### 3. Criar pacote

```bash
curl -i -X POST http://localhost:5000/api/packages \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "trackingCode": "BR123456789",
    "senderName": "Empresa Logística SA",
    "recipientName": "João Silva",
    "originAddress": "São Paulo, SP",
    "destinationAddress": "Rio de Janeiro, RJ",
    "weightKg": 3.5
  }'
```

### 4. Publicar evento de rastreamento

```bash
curl -i -X POST http://localhost:5000/api/tracking \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "trackingCode": "BR123456789",
    "status": 2,
    "location": "Centro de Distribuição",
    "latitude": -23.5505,
    "longitude": -46.6333,
    "description": "Pacote em triagem"
  }'
```

### 5. Consultar rastreamento

```bash
curl -i http://localhost:5000/api/tracking/BR123456789 \
  -H "Authorization: Bearer $TOKEN"
```

### 6. Refresh de token

```bash
curl -i -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "token": "TOKEN_ATUAL_OU_EXPIRADO",
    "refreshToken": "REFRESH_TOKEN"
  }'
```

### 7. Health checks

```bash
curl http://localhost:5000/health
curl http://localhost:5000/health/auth
curl http://localhost:5000/health/ingestion
curl http://localhost:5000/health/query
```

### 8. Testar rate limit do Gateway

```bash
for i in {1..105}; do
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/health/query
done
```

## Observabilidade

Serviços expostos:

- Gateway: `http://localhost:5000`
- Loki: `http://localhost:3100`
- Grafana: `http://localhost:3000`
- RabbitMQ UI: `http://localhost:15672`

Credenciais padrão:

- Grafana:
  - usuário: `admin`
  - senha: `admin`
- RabbitMQ:
  - usuário: `logistics_user`
  - senha: `Rabbit@2026!`

O Grafana já sobe com:

- datasource `Loki` provisionado automaticamente
- dashboard `Global Logistics Logs` provisionado automaticamente

### Consultas úteis no Grafana Explore

```logql
{service="auth-api"}
{service="ingestion-api", level="Error"}
{service="query-api"} |= "HTTP "
{environment="Production"}
```

## Estrutura do Repositório

```text
GlobalLogistics.Domain/
GlobalLogistics.Application/
GlobalLogistics.Infrastructure/
GlobalLogistics.IngestionAPI/
GlobalLogistics.QueryAPI/
GlobalLogistics.AuthAPI/
GlobalLogistics.Gateway/
GlobalLogistics.Worker/
GlobalLogistics.Tests.Integration/
observability/
docker-compose.yml
```

## Notas

- As APIs de negócio e autenticação ficam na rede interna do Docker; o acesso externo é feito pelo Gateway.
- O Worker continua consumindo eventos do RabbitMQ internamente.
- Os logs enviados ao Loki possuem labels `service`, `level` e `environment`.
