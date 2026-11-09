#!/usr/bin/env bash
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
P8="psql -h $H -p 5438 -U postgres -d esteraone -Atq -v ON_ERROR_STOP=1"
P9="psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq -v ON_ERROR_STOP=1"

# tables + single-column PK
$P8 -c "select c.relname || '|' || a.attname
from pg_index i
join pg_class c on c.oid = i.indrelid
join pg_namespace n on n.oid = c.relnamespace
join pg_attribute a on a.attrelid = c.oid and a.attnum = i.indkey[0]
where n.nspname='public' and i.indisprimary and array_length(i.indkey,1)=1
order by 1;" > /tmp/pks.txt

printf "%-40s %8s %8s %8s %8s\n" TABLE ROWS_5438 ROWS_5439 MISSING MODIFIED > /tmp/diff_report.txt
: > /tmp/diff_detail.txt

while IFS='|' read -r t pk; do
  n8=$($P8 -c "select count(*) from \"$t\";")
  n9=$($P9 -c "select count(*) from \"$t\";")
  $P8 -c "select \"$pk\"::text from \"$t\";" | sort -u > /tmp/a.ids
  $P9 -c "select \"$pk\"::text from \"$t\";" | sort -u > /tmp/b.ids
  miss=$(comm -13 /tmp/a.ids /tmp/b.ids | wc -l)
  extra=$(comm -23 /tmp/a.ids /tmp/b.ids | wc -l)
  printf "%-40s %8s %8s %8s %8s\n" "$t" "$n8" "$n9" "$miss" "$extra" >> /tmp/diff_report.txt
  if [ "$miss" -gt 0 ] || [ "$extra" -gt 0 ]; then
    echo "TABLE $t pk=$pk rows8=$n8 rows9=$n9 missing_in_5438=$miss only_in_5438=$extra" >> /tmp/diff_detail.txt
  fi
done < /tmp/pks.txt

echo "=== DIFF REPORT ==="
cat /tmp/diff_report.txt
echo "=== TABLES WITH DIFFERENCES ==="
cat /tmp/diff_detail.txt
echo "=== tables without single pk ==="
$P8 -c "select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' and not exists (select 1 from pg_index i where i.indrelid=c.oid and i.indisprimary) order by 1;"
