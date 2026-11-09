#!/usr/bin/env bash
export PM2_HOME=/home/admin/.pm2
echo "=== pm2 list full ==="
pm2 jlist 2>/dev/null | jq -r '.[] | {name, pid: .pid, status: .pm2_env.status, cwd: .pm2_env.pm_cwd, script: .pm2_env.pm_exec_path, args: (.pm2_env.args // []), node_args: .pm2_env.node_args, restart: .pm2_env.restart_time, uptime: .pm2_env.pm_uptime} | tostring'
echo "=== esteraone-green-be full env ==="
pm2 jlist 2>/dev/null | jq -r '.[] | select(.name=="esteraone-green-be") | .pm2_env.env'
echo "=== describe esteraone-green-be ==="
pm2 describe esteraone-green-be 2>/dev/null | sed -n '1,60p'
