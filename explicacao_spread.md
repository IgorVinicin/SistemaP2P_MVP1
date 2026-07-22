# Análise Prática: Como o Spread da Plataforma é Calculado

Esta documentação detalha a lógica matemática utilizada pela plataforma para calcular a distribuição de rendimentos e o lucro operacional (spread), utilizando dados reais de um empréstimo extraído do banco de dados do sistema.

## 1. Dados do Empréstimo e Investimento

- **Tomador:** `toma20`
- **Investidor:** `inv60`
- **Valor Solicitado (toma20):** R$ 20.000,00
- **Taxa de Juros do Empréstimo:** 7,00% ao mês
- **Valor Investido (inv60):** R$ 2.000,00 (o que dá exatamente **10%** do empréstimo)
- **Taxa Combinada (inv60):** 5,00% ao mês

> [!NOTE]
> A diferença entre a taxa cobrada do tomador (7%) e a taxa paga ao investidor (5%) é o que compõe a base do Spread da plataforma (2%).

## 2. O Pagamento da Primeira Parcela

O tomador (`toma20`) pagou a primeira parcela no valor total de **R$ 1.887,86**.

O sistema desmembrou esse pagamento pela Tabela Price da seguinte forma:
- **Amortização do Principal:** R$ 487,86
- **Juros da Parcela:** R$ 1.400,00 *(7% exatos de R$ 20.000)*

## 3. A Matemática do Investidor (inv60)

Como o investidor (`inv60`) tem **10%** de participação no empréstimo, o sistema calcula o que ele deve receber:

### a) Devolução do Principal:
- 10% de R$ 487,86 = **R$ 48,79** 
*(Isso volta pro bolso do investidor, descontando do saldo devedor pendente do capital que ele investiu).*

### b) Rendimento (Os Juros):
Aqui entra o cálculo diferencial. O `inv60` **não recebe 10% dos R$ 1.400,00 integrais** (o que daria R$ 140,00). Ele recebe a proporção baseada na taxa que ele aceitou (5% ao invés de 7%).

- O cálculo exato que a plataforma faz: `R$ 140,00 * (5 / 7) = R$ 100,00`.
- Outra forma de entender: O `inv60` investiu R$ 2.000,00 a 5% ao mês. O primeiro mês sobre esse capital rende exatamente **R$ 100,00**.

> [!IMPORTANT]
> O valor registrado na carteira do investidor no banco de dados para os "Rendimentos" dessa parcela é rigorosamente de **R$ 100,00**.

## 4. O Lucro da Plataforma (Spread)

Pela fatia que pertencia a esse investidor específico, o tomador pagou R$ 140,00 de juros na parcela, mas o investidor recebeu exatamente os R$ 100,00 combinados.

- Os **R$ 40,00** de diferença dessa fração específica viram imediatamente **receita (spread) da plataforma**.
- Extrapolando o cenário: se todo o empréstimo (R$ 20.000) fosse financiado por investidores cobrando 5%, a plataforma recolheria exatos **R$ 400,00 de spread livre** só nessa primeira parcela (a diferença de 2% sobre os R$ 20.000 totais).

## Conclusão

O algoritmo de distribuição do `WalletDistributionService` aplica os pagamentos de forma impecável:
1. Ele limita o rendimento ao teto contratado pelo investidor (evitando a criação de "dinheiro do nada").
2. Ele direciona todo o excedente cobrado do tomador diretamente para a conta da plataforma.
3. Não ocorrem perdas de arredondamento porque o spread é calculado sempre pela **subtração do valor que resta** depois de repassar o valor devido de principal e juros.
