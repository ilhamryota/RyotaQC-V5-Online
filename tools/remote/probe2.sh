#!/usr/bin/env bash
export PM2_HOME=/home/admin/.pm2
echo "=== PM2 LIST ==="
pm2 jlist 2>/dev/null | jq -r '.[] | [.name, .pm2_env.status, (.pm2_env.PORT//"-"), (.pm2_env.pm_cwd//"-"), (.pm2_env.pm_exec_path//"-")] | @tsv'
echo "=== ENV DB VARS PER APP ==="
pm2 jlist 2>/dev/null | jq -r '.[] | "APP=" + .name + "\n" + ((.pm2_env.env // {}) | to_entries | map(select(.key|test("PORT|DB|DATABASE|PG|POSTGRES|URL|HOST";"i"))) | map("  " + .key + "=" + (.value|tostring)) | join("\n"))'
echo "=== ENV FILES ==="
ls -la /home/esteraone/esteraone.green 2>&1 | head -30
ls -la /home/esteraone/esteraone.green/bestlaptop-erp-be 2>&1 | head -30
ls -la /home/esteraone/intergarasi.ryota/ryota-qc-hub 2>&1 | head -30
echo "=== postgres procs ==="
ps -eo user,pid,args | grep -i postgres | grep -v grep | head -20
