#!/usr/bin/env bash
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
P8="psql -h $H -p 5438 -U postgres -d esteraone -Atq"
P9="psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq"

echo "=== newest activity in 5438 ==="
$P8 -c "select 'app_activity_logs', max(\"createdAt\") from \"app_activity_logs\";" 2>&1
$P8 -c "select 'Attendance', max(\"createdAt\") from \"Attendance\";" 2>&1
$P8 -c "select 'ServiceTicket', max(\"createdAt\") from \"ServiceTicket\";" 2>&1
echo "=== newest activity in 5439 ==="
$P9 -c "select 'app_activity_logs', max(\"createdAt\") from \"app_activity_logs\";" 2>&1
$P9 -c "select 'Attendance', max(\"createdAt\") from \"Attendance\";" 2>&1
$P9 -c "select 'ServiceTicket', max(\"createdAt\") from \"ServiceTicket\";" 2>&1
echo "=== kanban-ish tables ==="
$P8 -c "select table_name from information_schema.tables where table_schema='public' and (table_name ilike '%task%' or table_name ilike '%board%' or table_name ilike '%card%' or table_name ilike '%project%') order by 1;" 2>&1
echo "=== timezone ==="
date
$P8 -c "show timezone; select now();"
