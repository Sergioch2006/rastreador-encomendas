# Global Logistics Tracker - Distributed Systems Case Study

Este projeto demonstra a aplicação prática dos conceitos de Engenharia de Software propostos por Roger Pressman, utilizando tecnologias de ponta do ecossistema .NET.

## 🛠 Metodologia de Desenvolvimento (Framework de Pressman)

Para garantir o rigor técnico e a qualidade do software, o projeto foi estruturado seguindo as 5 atividades fundamentais:

### 1. Comunicação (Communication)
- **Objetivo:** Alinhamento de requisitos de alta performance e resiliência.
- **Resultado:** Criação do Documento de Visão e definição de Requisitos Não Funcionais focados em escalabilidade horizontal.

### 2. Planejamento (Planning)
- **Gestão:** Utilização de metodologias ágeis (Scrum/Kanban) para organização das tarefas.
- **Backlog:** Divisão em épicos: Core Infrastructure, Ingestion Service, Event Bus, e Query Service.

### 3. Modelagem (Modeling)
- **Arquitetura:** Implementação do padrão CQRS (Command Query Responsibility Segregation) para otimizar fluxos de leitura e escrita.
- **Design:** Utilização de Clean Architecture para desacoplar as regras de negócio da infraestrutura externa.
- **Diagramação:** Desenho da topologia de microserviços e fluxo de mensagens via RabbitMQ.

### 4. Construção (Construction)
- **Stack:** .NET 9 (C#), Entity Framework Core, MediatR e MassTransit.
- **Qualidade:** Implementação de testes unitários (xUnit) e análise estática para conformidade com princípios SOLID.
- **Infraestrutura:** Configuração de Dockerfiles otimizados para ambiente Linux/Container.

### 5. Emprego (Deployment)
- **Orquestração:** Uso de Docker Compose para automação do ambiente de desenvolvimento no Zorin OS.
- **CI/CD:** Preparado para pipelines automatizados (GitHub Actions/Azure DevOps).
- **Monitoramento:** Implementação de Health Checks e logs estruturados.

## 🚀 Como Executar

1. Certifique-se de ter o Docker instalado no seu sistema Linux.
2. Clone o repositório.
3. Execute o comando abaixo:
   ```bash
   docker compose up -d
4. Acesse o painel do RabbitMQ em localhost:15672.
