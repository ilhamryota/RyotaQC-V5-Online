#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
SRVIP=109.123.239.163

echo "=== A. koneksi backend ke masing-masing DB (sebelum test) ==="
for p in 5438:esteraone 5439:esteraone_dev; do
  port=${p%%:*}; db=${p##*:}
  printf "port %s (%s): " "$port" "$db"
  psql -h $H -p $port -U postgres -d $db -Atc "select count(*) from pg_stat_activity where client_addr='$SRVIP';"
done

echo
echo "=== B. request via Host: dev.esteraone.com (murni lokal, tanpa nginx) ==="
for i in 1 2 3; do
  code=$(curl -s -m 10 -o /dev/null -w '%{http_code}' -X POST -H 'Host: dev.esteraone.com' -H 'Content-Type: application/json' \
    -d "{\"email\":\"probe$i@local.test\",\"password\":\"wrongpass\"}" http://127.0.0.1:8002/api/auth/login)
  echo "  dev-host request #$i -> HTTP $code"
done
sleep 1
echo "  -- query terakhir yang dijalankan backend, per DB --"
for p in 5438:esteraone 5439:esteraone_dev; do
  port=${p%%:*}; db=${p##*:}
  echo "  [port $port / $db]"
  psql -h $H -p $port -U postgres -d $db -Atc "select state||' | '||coalesce(regexp_replace(left(query,70),'\s+',' ','g'),'-') from pg_stat_activity where client_addr='$SRVIP' order by state_date desc limit 3;" 2>/dev/null | sed 's/^/    /'
done

echo
echo "=== C. request via Host: esteraone.com (PROD) ==="
for i in 1 2 3; do
  code=$(curl -s -m 10 -o /dev/null -w '%{http_code}' -X POST -H 'Host: esteraone.com' -H 'Content-Type: application/json' \
    -d "{\"email\":\"probe$i@local.test\",\"password\":\"wrongpass\"}" http://127.0.0.1:8002/api/auth/login)
  echo "  prod-host request #$i -> HTTP $code"
done
sleep 1
for p in 5438:esteraone 5439:esteraone_dev; do
  port=${p%%:*}; db=${p##*:}
  echo "  [port $port / $db]"
  psql -h $H -p $port -U postgres -d $db -Atc "select state||' | '||coalesce(regexp_replace(left(query,70),'\s+',' ','g'),'-') from pg_stat_activity where client_addr='$SRVIP' order by state_date desc limit 3;" 2>/dev/null | sed 's/^/    /'
done

echo
echo "=== D. nginx live (dev & prod lewat domain asli) ==="
curl -s -m 10 -o /dev/null -w "  https://esteraone.com/api/auth/login      -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{"email":"p@x.y","password":"x"}' https://esteraone.com/api/auth/login
curl -s -m 10 -o /dev/null -w "  https://dev.esteraone.com/api/auth/login  -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{"email":"p@x.y","password":"x"}' https://dev.esteraone.com/api/auth/login
