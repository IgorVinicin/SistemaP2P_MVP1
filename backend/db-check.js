const { Client } = require('pg');
const client = new Client({ connectionString: 'postgres://postgres:postgres@localhost:5432/sistemap2p' });
async function run() {
  await client.connect();
  const res = await client.query('SELECT * FROM "CashFlowSnapshots" ORDER BY "SnapshotDate" DESC');
  console.log('SNAPSHOTS:', res.rows);
  await client.end();
}
run();
