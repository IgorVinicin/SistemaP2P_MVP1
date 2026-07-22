const { Client } = require('pg');
const client = new Client({ connectionString: 'postgres://postgres:postgres@localhost:5432/sistemap2p' });
async function run() {
  await client.connect();
  const res = await client.query('SELECT "Id", "TransactionDate", "Valor" FROM "LedgerTransactions" ORDER BY "TransactionDate" DESC LIMIT 5');
  console.log('LEDGER TRANSACTIONS:', res.rows);
  await client.end();
}
run();
