#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
Q8() { psql -h $H -p 5438 -U postgres -d esteraone -Atq -c "$1"; }
Q9() { psql -h $H -p 5439 -U postgres -d esteraone_dev -Atq -c "$1"; }

echo "=== column parity per table ==="
Q8 "select table_name||'|'||count(*)||'|'||string_agg(column_name||':'||data_type, ',' order by ordinal_position) from information_schema.columns where table_schema='public' group by table_name order by table_name;" > /tmp/p8.txt
Q9 "select table_name||'|'||count(*)||'|'||string_agg(column_name||':'||data_type, ',' order by ordinal_position) from information_schema.columns where table_schema='public' group by table_name order by table_name;" > /tmp/p9.txt
if diff -q /tmp/p8.txt /tmp/p9.txt >/dev/null; then
  echo "SCHEMA PARITY: IDENTICAL ($(wc -l < /tmp/p8.txt) tables)"
else
  echo "SCHEMA PARITY: DIFFERENCES FOUND"
  diff /tmp/p8.txt /tmp/p9.txt | head -20
fi
echo "=== merge script stats ==="
wc -l /tmp/merge/pass1.sql 2>/dev/null
grep -c "INSERT INTO public" /tmp/merge/pass1.sql 2>/dev/null
ls /tmp/merge/data 2>/dev/null | wc -l
echo "=== current counts (key) ==="
for t in Attendance EmployeeShift ServiceTicket ServiceTicketComment app_activity_logs Notification CRMContact ProfileUpdateRequest FinanceTask; do
  printf "%-24s 5438=%s 5439=%s\n" "$t" "$(Q8 "select count(*) from \"$t\";")" "$(Q9 "select count(*) from \"$t\";")"
done
