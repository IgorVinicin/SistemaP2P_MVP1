# 🚀 Guia de Preparação para Produção (Sistema P2P)

Aqui está o diagnóstico do que precisa ser alterado, limpo e configurado para subir sua aplicação para produção com segurança e performance.

## 1. Banco de Dados (Supabase / Postgres)
Para migrar para o Supabase, o banco de dados deve estar limpo e estruturado.
✅ **O que eu já fiz:**
Gerei um arquivo `.sql` completo com toda a estrutura e dados da sua base local. 
O arquivo foi salvo na raiz do projeto como: **`sistemap2p_dump.sql`**. Você pode fazer o upload deste arquivo diretamente no painel do Supabase (no menu *SQL Editor*) e rodar para recriar o banco perfeitamente.

## 2. Backend (.NET Core)
Existem algumas configurações atuais no `Program.cs` que são feitas para desenvolvimento e **devem** ser alteradas em produção.

**🧹 O que DEVE ser alterado:**
- **CORS (Segurança):** Atualmente, a política `AllowAngular` está configurada com `.SetIsOriginAllowed(origin => true)`. Isso permite que **qualquer site na internet** faça chamadas para sua API usando as credenciais de um usuário.
  - *Em Produção:* Troque por `.WithOrigins("https://seudominio.com.br")`.
- **Swagger (Documentação):** A documentação da API está desabilitada em produção pelo bloco `if (app.Environment.IsDevelopment())`, o que está **correto**. Mantenha assim para não expor a estrutura da sua API publicamente.
- **HTTPS:** A linha `// app.UseHttpsRedirection();` está comentada.
  - *Em Produção:* Ela deve ser descomentada para forçar o tráfego a ser encriptado via SSL/TLS.
- **Variáveis de Ambiente:** No ambiente do servidor (VPS, Azure, AWS), lembre-se de cadastrar as duas variáveis que colocamos no `.env` (`ConnectionStrings__DefaultConnection` e `Jwt__Key`), trocando a URL do Postgres pela URL fornecida pelo **Supabase**.

## 3. Frontend (Angular)
No Frontend, o foco é construir uma versão minificada e otimizada que aponta para o servidor real.

**🧹 O que DEVE ser alterado:**
- **URL da API:** No arquivo `frontend/src/environments/environment.production.ts`, a variável `apiUrl` está apontando para o seu IP local (`http://192.168.18.213:5141/api`). 
  - *Em Produção:* Ela deve apontar para o domínio onde o Backend ficará hospedado (Ex: `https://api.seudominio.com.br/api`).
- **Comando de Build:** Para enviar o projeto ao servidor de produção, **não** faça upload do código fonte inteiro. Você deve rodar o comando:
  ```bash
  ng build --configuration production
  ```
  Isso vai gerar uma pasta `dist/` com arquivos ultra-leves e minificados. Apenas essa pasta vai para a hospedagem do site (Vercel, Netlify, Apache, etc).

## 4. O que NÃO precisa ser alterado (Deixe como está)
- **Código Fonte (Controllers, Services):** Nós fizemos a limpeza de imports fantasmas e de alertas de segurança recentemente. O código base está limpo e seguro para rodar.
- **Rate Limiting:** A configuração de Rate Limit (que bloqueia múltiplas tentativas de requisições rápidas) que está no `Program.cs` está excelente e pronta para proteger o servidor contra ataques de negação de serviço.
- **Tratamento de Exceções Globais (`GlobalExceptionHandler`):** Está ativo e padronizado, vai garantir que erros do sistema retornem mensagens JSON amigáveis sem vazar stack trace no console do navegador em produção.
