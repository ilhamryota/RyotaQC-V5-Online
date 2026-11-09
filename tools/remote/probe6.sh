#!/usr/bin/env bash
echo "=== upstream conf ==="
cat /etc/nginx/conf.d/esteraone_upstream.conf
echo "=== dev fe env.local ==="
cat /home/esteraone/esteraone.green/bestlaptop-erp-fe/.env.local
echo "=== proc identity ==="
for p in 1670088 1670111 1670112 1674331 1083571 292370; do
  echo "--- PID $p ---"
  tr '\0' ' ' < /proc/$p/cmdline 2>/dev/null; echo
  readlink /proc/$p/cwd 2>&1
  ls -l /proc/$p/cwd 2>&1
done
echo "=== listening sockets all ==="
ss -lnt 2>/dev/null | awk '{print $4}' | sort -u
