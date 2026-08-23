//geral

tentar melhorar a atualização das paginas, pois visualmente não atualiza e tem que recarregar.

cadastro: cpf deve ser real e não qualquer numero, data de nascimento tem que ser maior de 18, email deve ser valido, caso selecione a opção sim para a pergunta "possui empresa?", deve ser um cnpj valido tbm,


//login 

colocar limite de tentativas para evitar invasão

// visão admin

trocar o nome Dashboard no menu de admin para Início
na gestão de usuarios deve ter a opção de ver/editar a senha do usuario
na aba grupo de investidores colocar paginas limitando 5,10,20 por pagina e um filtro básico para pesquisar por nome do grupo.

analisar como está funcionando o dias para cobrança e cobrança intensiva

trocar nome config de score para configurar formulario.

na parte de analisar emprestimo em detalhe da proposta, não deve mostrar as parcelas e a taxa de juros, pois não foi setado ainda.
a fichinha não está vindo com os dados do respectitivo cliente.

caso o admin não selecione o grupo de investidores na aprovaçao do emprestimo o sistema deve entender que o dinheiro vai ser usado pelo proprio admin , nesse caso é uma operação manual e não vai ser descontado de nenhum usuario.

após fechar o caixa e ir no historico de fehamento percebi que fechei no dia 13 e ele contabilizou que foi fechado no dia 14.

//cliente (tomador de emprestimo)

melhorar o design da pagina de solicitação de documentos, deixar intuitivo que após o envio dos documentos o usuario deve responder as perguntas do formulario(não precisa saber que é para calcular o score, apenas responder as perguntas)
liberar as opções do menu após aprovação do cadastro pelo admin, deixando tudo em modo somente leitura até a aprovação e na pagina inicial mostrar uma mensagem dizendo que o cadastro está em análise.
melhorar o método de upload, deixar mais prático.
bug ao invés de fazer upload de um arquivo pelo celular, tentar tirar a foto na hora, ele recarrega a pagina.
analisar e fazer o redirecionamento das paginas corretamente.
aparecer a aba e permitir o acesso do tomador a pagina de solicitação de aumento de limite após a finalização de um empréstimo pago.
aba meu perfil após a aprovação deve mostrar os dados cadastrados, caso ele queira atualizar

//cliente (investidor)

no cadastro investidor colocar quanto vai ser investido no cadastro
e retirar a pergunta se possui empresa, pois não é relevante.

pós cadastro 

pagina como se fosse de banco onde aparece opções de investimentos 
(cada um com rentabilidades diferentes - configuravel pelo admin os valores de rentabilidade e nomes, quando implementar isso tirar a opçã
de quando aceitar o cadastro no painel do admin escolher a rentabilidade)


regra de negocio, quando o dinheiro for o saldo compremetido vai ser a rentabilidade x, e se o dinheiro estiver parado no saldo disponivel é rentabilidade y (configurado pelo admin)

invés de criar um email com base no cpf apenas deixe o cpf mesmo(no painel de usuarios exibir o cpf e não o email) e nas mensagens colocar o cpf no lugar do email tbm.

ao solicitar saque ele já está retirando o valor da conta, o que não é correto, só deve tirar após o admin aprovar o saque.

retirar esse histórico de movimentações na aba do investidor (ele não deve saber da operação que está rolando, apenas que ele tem um dinheiro investido).



//FINAL

Colocar a gateway de pagamento asaas no sistema e sincronizar todas as funções que antes eram simuladas. (PLANO CONCLUÍDO E DOCUMENTADO EM plano_integracao_asaas.md)

transformar em app.