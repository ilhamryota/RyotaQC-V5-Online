#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
Q8() { psql -h $H -p 5438 -U postgres -d esteraone -Atq -v ON_ERROR_STOP=1 -c "$1"; }
Q9() { psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq -v ON_ERROR_STOP=1 -c "$1"; }

mkdir -p /tmp/merge/data
rm -f /tmp/merge/data/*.dat
SQL=/tmp/merge/pass1.sql
: > $SQL

# pre snapshot
: > /tmp/merge/pre_counts.tsv
# missing id detail
: > /tmp/merge/missing_ids.txt

echo "=== column parity check ==="
Q8 "select table_name||'.'||count(*) from information_schema.columns where table_schema='public' group by 1 order by 1;" > /tmp/m.cols8
Q9 "select table_name||'.'||count(*) from information_schema.columns where table_schema='public' group by 1 order by 1;" > /tmp/m.cols9
if diff -q /tmp/m.cols8 /tmp/m.cols9 >/dev/null; then echo "PARITY OK ($(wc -l < /tmp/m.cols8) tables)"; else echo "PARITY MISMATCH:"; diff /tmp/m.cols8 /tmp/m.cols9; fi

echo "=== build stage/insert script ==="
{
  echo "SET client_min_messages = warning;"
  echo "BEGIN;"
  echo "SET LOCAL session_replication_role = replica;"
} >> $SQL

while IFS= read -r t; do
  cols=$(Q8 "select string_agg(quote_ident(column_name), ',' order by ordinal_position) from information_schema.columns where table_schema='public' and table_name='$t' and is_generated='NEVER';")
  hasident=$(Q8 "select count(*) from information_schema.columns where table_schema='public' and table_name='$t' and is_identity='YES';")
  q="\"$t\""
  Q9 "\\copy (select $cols from public.$q) to '/tmp/merge/data/$t.dat'" >/dev/null
  n8=$(Q8 "select count(*) from public.$q;")
  n9=$(Q9 "select count(*) from public.$q;")
  printf "%s\t%s\t%s\n" "$t" "$n8" "$n9" >> /tmp/merge/pre_counts.tsv
  ovr=""
  if [ "$hasident" != "0" ]; then ovr="OVERRIDING SYSTEM VALUE"; fi
  echo "CREATE TEMP TABLE stg_$t (LIKE public.$q);" >> $SQL
  echo "\\copy stg_$t ($cols) FROM '/tmp/merge/data/$t.dat'" >> $SQL
  echo "INSERT INTO public.$q ($cols) $ovr SELECT $cols FROM stg_$t ON CONFLICT DO NOTHING;" >> $SQL
  echo "DROP TABLE stg_$t;" >> $SQL
done < <(Q8 "select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' order by 1;")

echo "COMMIT;" >> $SQL
echo "script lines: $(wc -l < $SQL)"
echo "data files: $(ls /tmp/merge/data | wc -l)"

echo "=== RUN MERGE (pass 1) ==="
psql -h $H -p 5438 -U postgres -d esteraone -q -v ON_ERROR_STOP=1 -f $SQL 2>&1 | head -30
echo "exit=$?"

echo "=== post counts diff ==="
while IFS=$'\t' read -r t n8 n9; do
  now=$(Q8 "select count(*) from public.\"$t\";")
  if [ "$now" != "$n8" ]; then printf "%-28s pre=%-6s post=%-6s inserted=%s\n" "$t" "$n8" "$now" "$((now-n8))"; fi
done < /tmp/merge/pre_counts.tsv
