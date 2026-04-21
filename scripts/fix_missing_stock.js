const { Client } = require('../frontend/node_modules/pg');

const client = new Client({
  host: 'aws-1-us-east-1.pooler.supabase.com',
  port: 5432,
  database: 'postgres',
  user: 'postgres.qlnssywitvntudrukdnd',
  password: 'Fcampo1903**',
  ssl: { rejectUnauthorized: false }
});

const sql = `
  INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
  SELECT p.company_id, b.id, p.id, 0
  FROM inventory.products p
  CROSS JOIN core.branches b
  WHERE b.company_id = p.company_id
    AND p.deleted_at IS NULL
    AND p.is_active = TRUE
    AND NOT EXISTS (
      SELECT 1 FROM inventory.stock s
      WHERE s.product_id = p.id AND s.branch_id = b.id
    )
  ON CONFLICT (branch_id, product_id) DO NOTHING
`;

client.connect()
  .then(() => client.query(sql))
  .then(r => { console.log('Filas de stock creadas:', r.rowCount); client.end(); })
  .catch(e => { console.error('ERROR:', e.message); client.end(); });
