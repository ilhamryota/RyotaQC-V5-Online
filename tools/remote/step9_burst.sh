#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
xc() { psql -h $H -p $1 -U postgres -d $2 -Atc "select xact_commit from pg_stat_database where datname='$2';"; }

burst() {
  local host="$1"; local n="$2"
  for i in $(seq 1 $n); do
    curl -s -m 10 -o /dev/null -X POST -H "Host: $host" -H 'Content-Type: application/json' \
      -d "{\"email\":\"qa$i@local.test\",\"password\":\"x\"}" http://127.0.0.1:8002/api/auth/login
  done
}

echo "=== BURST TEST: 20 request ke host dev.esteraone.com ==="
p8=$(xc 5438 esteraone); p9=$(xc 5439 esteraone_dev)
burst dev.esteraone.com 20
sleep 2
q8=$(xc 5438 esteraone); q9=$(xc 5439 esteraone_dev)
echo "  transaksi 5438 (prod): $((q8-p8))"
echo "  transaksi 5439 (dev) : $((q9-p9))"

echo
echo "=== BURST TEST: 20 request ke host esteraone.com (PROD) ==="
p8=$(xc 5438 esteraone); p9=$(xc 5439 esteraone_dev)
burst esteraone.com 20
sleep 2
q8=$(xc 5438 esteraone); q9=$(xc 5439 esteraone_dev)
echo "  transaksi 5438 (prod): $((q8-p8))"
echo "  transaksi 5439 (dev) : $((q9-p9))"

echo
echo "=== error log setelah restart ==="
awk 'END{print NR}' /home/admin/.pm2/logs/esteraone-green-be-error.log
echo "  (bandingkan dengan baseline 42453 baris)"
