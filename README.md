# Sistema P2P Lending - MVP V1

Plataforma de empréstimos Peer-to-Peer (P2P) Lending de ponta a ponta, permitindo que tomadores de empréstimos solicitem recursos e investidores aportem valores em solicitações disponíveis, assegurados por regras de score de crédito e um fundo de proteção contra inadimplência.

---

## 🛠️ Stack Tecnológica & Versões

### 🖥️ Backend (API)
* **Runtime**: `.NET 10.0` (C# 14)
* **Framework**: `ASP.NET Core Web API`
* **OR/M & Banco de Dados**: `Entity Framework Core 10.0.8` com provedor `PostgreSQL` (`Npgsql 10.0.2`)
* **Segurança**: Autenticação baseada em Tokens JWT (`Microsoft.AspNetCore.Authentication.JwtBearer 10.0.8`) e criptografia com `Microsoft.AspNetCore.Identity.PasswordHasher`
* **Documentação**: `Microsoft.AspNetCore.OpenApi 10.0.5`

### 🎨 Frontend (SPA)
* **Framework**: `Angular 21.2.0` (com o novo esbuild application builder)
* **Componentes & UI**: `Angular Material / CDK 21.2.13`
* **Estilização**: CSS Nativo moderno e responsivo
* **Linguagem**: `TypeScript 5.9.2`
* **Gerenciamento de Estado**: Angular Signals & RxJS (`~7.8.0`)
* **Testes**: `Vitest 4.0.8`

---

## ⚙️ Configuração & Execução

### Pré-requisitos
* [.NET 10 SDK](https://dotnet.microsoft.com/download) instalado
* [Node.js](https://nodejs.org/) (versão recomendada v20 ou v22+)
* Banco de Dados PostgreSQL ativo

### 1. Inicializando o Backend
1. Navegue até a pasta do backend:
   ```bash
   cd backend
   ```
2. Configure a string de conexão do PostgreSQL no arquivo `appsettings.json`.
3. Execute a aplicação (a migração e carga de dados de seed ocorrerão automaticamente na inicialização):
   ```bash
   dotnet run
   ```
   * *A API estará rodando por padrão em:* `http://localhost:5141`

### 2. Inicializando o Frontend
1. Navegue até a pasta do frontend:
   ```bash
   cd frontend
   ```
2. Instale as dependências de pacotes:
   ```bash
   npm install
   ```
3. Execute o servidor de desenvolvimento:
   ```bash
   npm run start
   ```
   * *O painel do frontend estará acessível em:* `http://localhost:4200`
