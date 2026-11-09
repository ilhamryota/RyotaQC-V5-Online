#!/usr/bin/env bash
echo "=== devnull ==="
ls -l /dev/null
echo "=== ports ==="
for p in 8000 8001 8002 8004 8005 5438 5439 5432; do
  code=$(curl -s -m 3 -o /dev/null -w '%{http_code}' "http://127.0.0.1:$p/" 2>/dev/null)
  echo "PORT $p -> ${code:-no-http}"
done
echo "=== node procs ==="
ps -eo user,pid,ppid,etime,args | grep -E 'node|pm2' | grep -v grep
echo "=== pm2 home ==="
ls -la /home/admin/.pm2 2>&1 | head -20
