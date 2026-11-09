#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
export PM2_HOME=/home/admin/.pm2
H=202.74.74.229
Q8() { psql -h $H -p 5438 -U postgres -d esteraone -Atq -c "$1"; }
Q9() { psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq -c "$1"; }

echo "=== A. RESIDUAL MERGE (pass 2, insert-only) ==="
psql -h $H -p 5438 -U postgres -d esteraone -q -v ON_ERROR_STOP=1 -f /tmp/merge/pass1.sql 2>&1 | head -10

echo "=== B. FINAL MISSING-ROW AUDIT (all tables) ==="
total_missing=0
for t in $(Q8 "select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' order by 1;"); do
  pk=$(Q8 "select a.attname from pg_index i join pg_class c on c.oid=i.indrelid join pg_namespace n on n.oid=c.relnamespace join pg_attribute a on a.attrelid=c.oid and a.attnum=i.indkey[0] where n.nspname='public' and c.relname='$t' and i.indisprimary and array_length(i.indkey,1)=1;")
  [ -z "$pk" ] && continue
  Q8 "select \"$pk\"::text from public.\"$t\";" | sort -u > /tmp/v8.ids
  Q9 "select \"$pk\"::text from public.\"$t\";" | sort -u > /tmp/v9.ids
  miss=$(comm -13 /tmp/v8.ids /tmp/v9.ids | wc -l)
  n8=$(Q8 "select count(*) from public.\"$t\";")
  n9=$(Q9 "select count(*) from public.\"$t\";")
  if [ "$miss" -ne 0 ] || [ "$n8" -ne "$n9" ]; then
    printf "DIFF %-26s 5438=%-6s 5439=%-6s missing_in_5438=%s\n" "$t" "$n8" "$n9" "$miss"
    total_missing=$((total_missing+miss))
  fi
done
echo "TOTAL_MISSING_ROWS_IN_5438=$total_missing"

echo "=== C. newest activity comparison ==="
echo -n "5438 app_activity_logs: "; Q8 "select max(\"createdAt\") from app_activity_logs;"
echo -n "5439 app_activity_logs: "; Q9 "select max(\"createdAt\") from app_activity_logs;"
echo -n "5438 Attendance:        "; Q8 "select max(\"createdAt\") from \"Attendance\";"
echo -n "5439 Attendance:        "; Q9 "select max(\"createdAt\") from \"Attendance\";"

echo "=== D. backend -> DB wiring ==="
grep -n "^DATABASE_URL=" /home/esteraone/esteraone.green/bestlaptop-erp-be/.env
tail -60 /home/admin/.pm2/logs/esteraone-green-be-out.log | grep -E "DB_URL" | tail -3

echo "=== E. ports: extra port check ==="
ss -lnt 2>/dev/null | awk '$4 ~ /:(8003|8004|8006)$/ {print "EXTRA PORT:",$4}'
echo "(empty = clean)"
ss -lnt 2>/dev/null | awk '$4 ~ /:(8000|8002|8005|3001)$/ {print "active:",$4}' | sort -u

echo "=== F. live traffic checks ==="
curl -s -m 10 -o /dev/null -w "prod api   -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' https://esteraone.com/api/auth/login
curl -s -m 10 -o /dev/null -w "dev  api   -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' https://dev.esteraone.com/api/auth/login
curl -s -m 10 -o /dev/null -w "prod web   -> %{http_code}\n" https://esteraone.com/
curl -s -m 10 -o /dev/null -w "hub 8005   -> %{http_code}\n" http://127.0.0.1:8005/api/v1/qc/metrics

echo "=== G. pm2 final ==="
pm2 jlist 2>/dev/null | jq -r '.[] | [.name,.pm2_env.status] | @tsv'
