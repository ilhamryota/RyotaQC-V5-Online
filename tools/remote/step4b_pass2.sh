#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
Q8() { psql -h $H -p 5438 -U postgres -d esteraone -Atq -c "$1"; }
Q9() { psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq -c "$1"; }

mkdir -p /tmp/merge2/data
rm -f /tmp/merge2/data/*.dat
SQL=/tmp/merge2/pass2.sql
: > $SQL
{
  echo "SET client_min_messages = warning;"
  echo "BEGIN;"
  echo "SET LOCAL session_replication_role = replica;"
} >> $SQL

for t in $(Q8 "select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' order by 1;"); do
  cols=$(Q8 "select string_agg(quote_ident(column_name), ',' order by ordinal_position) from information_schema.columns where table_schema='public' and table_name='$t' and is_generated='NEVER';")
  hasident=$(Q8 "select count(*) from information_schema.columns where table_schema='public' and table_name='$t' and is_identity='YES';")
  Q9 "\\copy (select $cols from public.\"$t\") to '/tmp/merge2/data/$t.dat'" >/dev/null
  ovr=""
  [ "$hasident" != "0" ] && ovr="OVERRIDING SYSTEM VALUE"
  echo "CREATE TEMP TABLE stg2_$t (LIKE public.\"$t\");" >> $SQL
  echo "\\copy stg2_$t ($cols) FROM '/tmp/merge2/data/$t.dat'" >> $SQL
  echo "INSERT INTO public.\"$t\" ($cols) $ovr SELECT $cols FROM stg2_$t ON CONFLICT DO NOTHING;" >> $SQL
  echo "DROP TABLE stg2_$t;" >> $SQL
done
echo "COMMIT;" >> $SQL
echo "pass2 lines: $(wc -l < $SQL)"

echo "=== pre-pass2 counts ==="
for t in Attendance EmployeeShift ServiceTicket ServiceTicketComment app_activity_logs Notification CRMContact FinanceTask JournalDetail JournalHeader POSServiceInvoice POSServiceInvoiceItem ProfileUpdateRequest; do
  printf "%-24s %s\n" "$t" "$(Q8 "select count(*) from public.\"$t\";")"
done > /tmp/pre2.txt
cat /tmp/pre2.txt

echo "=== RUN PASS 2 ==="
psql -h $H -p 5438 -U postgres -d esteraone -q -v ON_ERROR_STOP=1 -f $SQL 2>&1 | head -10

echo "=== post-pass2 delta ==="
while read -r t n; do
  now=$(Q8 "select count(*) from public.\"$t\";")
  [ "$now" != "$n" ] && printf "%-24s %s -> %s (+%s)\n" "$t" "$n" "$now" "$((now-n))"
done < /tmp/pre2.txt
echo "PASS2_DONE"
