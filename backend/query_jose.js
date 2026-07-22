const { Client } = require('pg');
const client = new Client({ connectionString: 'postgres://postgres:postgres@localhost:5432/sistemap2p' });

async function run() {
  await client.connect();
  
  // Find User Jose
  const userRes = await client.query('SELECT u."Id" as "UserId", * FROM "Users" u JOIN "UserProfiles" p ON u."Id" = p."Id" WHERE p."Nome" ILIKE \'%josé%\' OR p."Nome" ILIKE \'%jose%\'');
  console.log('USERS FOUND:', userRes.rows);
  
  if (userRes.rows.length === 0) {
    console.log('Jose not found.');
    await client.end();
    return;
  }
  
  const userId = userRes.rows[0].UserId || userRes.rows[0].Id;
  
  // Find Loans
  const loanRes = await client.query(`SELECT * FROM "LoanRequests" WHERE "UserId" = '${userId}'`);
  console.log('LOANS FOUND:', loanRes.rows);
  
  for (const loan of loanRes.rows) {
    const instRes = await client.query(`SELECT * FROM "LoanInstallments" WHERE "LoanRequestId" = '${loan.Id}' ORDER BY "NumeroParcela"`);
    console.log(`INSTALLMENTS FOR LOAN ${loan.Id} (${loan.ValorSolicitado} BRL, status: ${loan.Status}, juros: ${loan.TaxaJuros}%):`);
    for (const inst of instRes.rows) {
      console.log(`  Parcela ${inst.NumeroParcela}: Valor ${inst.ValorParcela}, Principal ${inst.ValorPrincipal}, Juros ${inst.ValorJuros}, Vencimento ${inst.DataVencimento}`);
    }
  }
  
  await client.end();
}

run().catch(console.error);
