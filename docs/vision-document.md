# 1. Documento de Visão: Global Logistics Tracker

## 1.1 Problema Oportunidade
Empresas de logística global enfrentam dificuldades para monitorar frotas em tempo real devido ao alto volume de dados (telemetria) e à necessidade de baixa latência na ponta de consulta. O sistema atual visa resolver o gargalo entre a ingestão massiva de dados e a disponibilidade imediata para o usuário final.

## 1.2 Descrição dos Envolvidos (Stakeholders)
- **Gestores de Frota:** Precisam de dados em tempo real para tomada de decisão.
- **Desenvolvedores (Sérgio):** Engenheiro responsável pela escalabilidade e manutenção da arquitetura distribuída.
- **Clientes Finais:** Usuários que consultam o status de seus pacotes.

## 1.3 Recursos e Funcionalidades do Produto
- **FE01:** Ingestão de telemetria via Minimal APIs (`.NET 9`).
- **FE02:** Processamento assíncrono de eventos para garantir que nenhuma coordenada seja perdida (`RabbitMQ`).
- **FE03:** Consulta de última localização com cache de alta performance (`Redis`).
- **FE04:** Histórico completo de movimentação com persistência relacional (`SQL Server`).

## 1.4 Restrições e Requisitos Não Funcionais
- **Arquitetura:** Baseada em Microserviços e CQRS.
- **Ambiente:** Totalmente containerizado com `Docker` para rodar em Linux e Cloud.
- **Qualidade:** Aplicação estrita de princípios `SOLID`, `Clean Code` e Testes Automatizados.