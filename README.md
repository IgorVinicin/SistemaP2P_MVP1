# Sistema P2P — Plataforma de Empréstimos

Plataforma de empréstimos peer-to-peer (P2P) desenvolvida para conectar pessoas que desejam solicitar crédito a investidores interessados em financiar operações de empréstimo.

O projeto implementa um fluxo de intermediação financeira com gerenciamento de usuários, análise de perfil, avaliação de risco e controle administrativo das operações.

## Visão geral

A aplicação foi estruturada para atender três perfis principais:

- **Solicitante:** realiza cadastro, fornece informações pessoais e solicita empréstimos.
- **Investidor:** acompanha oportunidades e informações relacionadas às operações de investimento.
- **Administrador:** gerencia usuários, analisa solicitações e controla a aprovação das operações.

O sistema considera etapas de validação cadastral e análise antes da liberação de uma operação.

## Funcionalidades

### Gestão de usuários
- Cadastro e autenticação de usuários.
- Separação de permissões por perfil.
- Validação de informações cadastrais.
- Gerenciamento de documentos de identificação.
- Fluxo de envio e análise de documentação.

### Solicitação de empréstimos
- Cadastro de solicitações de crédito.
- Coleta de informações pessoais e financeiras.
- Identificação de pessoa física ou jurídica.
- Envio de documentos complementares.
- Questionário de avaliação de perfil.

### Análise de risco
- Coleta de informações para avaliação do solicitante.
- Estruturação de critérios de pontuação.
- Classificação de risco.
- Suporte à análise administrativa.

### Gestão administrativa
- Visualização de solicitações.
- Análise e aprovação de cadastros e documentos.
- Controle das operações.
- Aprovação administrativa final dos empréstimos.

### Investidores
- Acesso às informações das operações.
- Visualização de oportunidades de investimento.
- Acompanhamento de informações relacionadas ao risco e às operações financiadas.

## Tecnologias utilizadas

| Tecnologia | Aplicação |
|---|---|
| C# | Linguagem principal do backend |
| ASP.NET Core | Desenvolvimento da API |
| Entity Framework Core | ORM e persistência de dados |
| PostgreSQL | Banco de dados relacional |
| Npgsql | Integração com PostgreSQL |
| Angular | Desenvolvimento da interface |
| JWT | Autenticação e autorização |
| xUnit | Testes automatizados |

## Arquitetura

O projeto utiliza uma arquitetura cliente-servidor, dividida em duas aplicações principais:

**Backend**
- API REST desenvolvida em ASP.NET Core.
- Regras de negócio e validações.
- Persistência de dados utilizando Entity Framework Core.
- Autenticação e controle de acesso.

**Frontend**
- Aplicação Angular responsável pela interface.
- Comunicação com a API por requisições HTTP.
- Separação de funcionalidades conforme os perfis de usuário.

**Testes**
- Projeto dedicado a testes automatizados do backend.
- Validação de comportamentos e regras da aplicação.

## Estrutura do repositório

```text
SistemaP2P_MVP1/
├── backend/          # API e regras de negócio
├── backend.Tests/    # Testes automatizados
├── frontend/         # Aplicação Angular
├── md/               # Documentação complementar
├── sistemap2p_dump.sql
└── Documentação MVP
```

## Executando o projeto

### Pré-requisitos

- .NET SDK compatível com a solução.
- Node.js e npm.
- PostgreSQL.
- Angular CLI, caso necessário.

### Backend

Entre na pasta do backend:

```bash
cd backend
dotnet restore
```

Configure a conexão com o PostgreSQL e as demais variáveis de ambiente necessárias.

Execute as migrações, caso estejam configuradas na solução:

```bash
dotnet ef database update
```

Inicie a API:

```bash
dotnet run
```

### Frontend

Em outro terminal:

```bash
cd frontend
npm install
```

Execute a aplicação utilizando o script de desenvolvimento configurado no `package.json`.

### Testes

Para executar os testes automatizados:

```bash
dotnet test
```

## Segurança e validação

A aplicação contempla autenticação baseada em tokens, separação de permissões por perfil e validação de informações fornecidas pelos usuários.

Os fluxos de aprovação administrativa permitem que operações passem por análise antes de sua efetivação.

## Objetivo técnico

O projeto tem como objetivo aplicar conceitos de desenvolvimento de APIs REST, arquitetura cliente-servidor, modelagem de dados, autenticação, autorização, integração entre frontend e backend e implementação de regras de negócio em um domínio financeiro.

## Autor

**Igor Vinicius**

[GitHub](https://github.com/IgorVinicin)
