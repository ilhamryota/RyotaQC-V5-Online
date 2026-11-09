#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
L=/home/admin/.pm2/logs/esteraone-green-be-out.log
echo "=== ALL DB_URL lines with position (last 5) ==="
grep -n "DB_URL" "$L" | tail -5
echo "=== total lines in log ==="
wc -l "$L"
echo "=== last DB_URL occurrence ==="
grep "DB_URL" "$L" | tail -1
echo
echo "=== FULL final missing-row audit (78 tables) ==="
Q8() { psql -h $H -p 5438 -U postgres -d esteraone -Atq -c "$1"; }
Q9() { psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq -c "$1"; }
tm=0
for t in $(Q8 "select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' order by 1;"); do
  pk=$(Q8 "select a.attname from pg_index i join pg_class c on c.oid=i.indrelid join pg_namespace n on n.oid=c.relnamespace join pg_attribute a on a.attrelid=c.oid and a.attnum=i.indkey[0] where n.nspname='public' and c.relname='$t' and i.indisprimary and array_length(i.indkey,1)=1;")
  [ -z "$pk" ] && continue
  Q8 "select \"$pk\"::text from public.\"$t\";" | sort -u > /tmp/x8.ids
  Q9 "select \"$pk\"::text from public.\"$t\";" | sort -u > /tmp/x9.ids
  miss=$(comm -13 /tmp/x8.ids /tmp/x9.ids | wc -l)
  if [ "$miss" -ne 0 ]; then
    printf "STILL_MISSING %-24s %s rows\n" "$t" "$miss"
    tm=$((tm+miss))
  fi
done
echo "TOTAL_MISSING_IN_5438=$tm"
echo
echo "=== row counts side by side (non-empty tables) ==="
printf "%-30s %10s %10s\n" TABLE 5438 5439
for t in $(Q8 "select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' order by 1;"); do
  n8=$(Q8 "select count(*) from public.\"$t\";"); n9=$(Q9 "select count(*) from public.\"$t\";")
  [ "$n8" = "0" ] && [ "$n9" = "0" ] && continue
  printf "%-30s %10s %10s\n" "$t" "$n8" "$n9"
done
