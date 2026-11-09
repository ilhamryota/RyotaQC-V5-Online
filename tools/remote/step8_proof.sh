#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229

snap() {
  psql -h $H -p 5438 -U postgres -d esteraone -Atc "select coalesce(max(query_start)::text,'-') from pg_stat_activity where datname='esteraone';"
}
snap9() {
  psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select coalesce(max(query_start)::text,'-') from pg_stat_activity where datname='esteraone_dev';"
}

echo "=== ROUTING PROOF: query_start per DB ==="
b8=$(snap); b9=$(snap9)
echo "before  prod5438=$b8   dev5439=$b9"
sleep 1
echo "--- kirim 1 request Host: dev.esteraone.com ---"
curl -s -m 10 -o /dev/null -w "   http=%{http_code}\n" -X POST -H 'Host: dev.esteraone.com' -H 'Content-Type: application/json' -d '{"email":"zz@local.test","password":"x"}' http://127.0.0.1:8002/api/auth/login
sleep 1
a8=$(snap); a9=$(snap9)
echo "after   prod5438=$a8   dev5439=$a9"
[ "$a9" != "$b9" ] && echo "   => DEV request menyentuh 5439 (BENAR)"
[ "$a8" != "$b8" ] && echo "   => DEV request menyentuh 5438 (SALAH!)"

b8=$(snap); b9=$(snap9)
echo "--- kirim 1 request Host: esteraone.com ---"
curl -s -m 10 -o /dev/null -w "   http=%{http_code}\n" -X POST -H 'Host: esteraone.com' -H 'Content-Type: application/json' -d '{"email":"zz@local.test","password":"x"}' http://127.0.0.1:8002/api/auth/login
sleep 1
a8=$(snap); a9=$(snap9)
echo "after   prod5438=$a8   dev5439=$a9"
[ "$a8" != "$b8" ] && echo "   => PROD request menyentuh 5438 (BENAR)"
[ "$a9" != "$b9" ] && echo "   => PROD request menyentuh 5439 (SALAH!)"

echo
echo "=== detail query terakhir tiap DB ==="
psql -h $H -p 5438 -U postgres -d esteraone -Atc "select '5438 | '||coalesce(regexp_replace(left(query,60),'\s+',' ','g'),'-')||' | '||coalesce(query_start::text,'-') from pg_stat_activity where datname='esteraone' and query_start is not null order by query_start desc limit 3;" | sed 's/^/  /'
psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select '5439 | '||coalesce(regexp_replace(left(query,60),'\s+',' ','g'),'-')||' | '||coalesce(query_start::text,'-') from pg_stat_activity where datname='esteraone_dev' and query_start is not null order by query_start desc limit 3;" | sed 's/^/  /'

echo
echo "=== error log: apakah error lama atau baru? (restart terakhir) ==="
ls -l --time-style=+%H:%M:%S /home/admin/.pm2/logs/esteraone-green-be-error.log
tail -3 /home/admin/.pm2/logs/esteraone-green-be-error.log | head -3
echo "--- 5 error terakhir dengan timestamp ---"
grep -aE "^[A-Z][a-z]{2} [A-Z][a-z]{2} [0-9]" /home/admin/.pm2/logs/esteraone-green-be-error.log | tail -3
grep -ac "" /home/admin/.pm2/logs/esteraone-green-be-error.log
