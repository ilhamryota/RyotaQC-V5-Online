#!/usr/bin/env bash
A=/home/admin/esteraone.admin/src/server.js
echo "=== lines 1640-1760 ==="
sed -n '1640,1760p' $A
echo "=== is admin copy symlink? ==="
ls -ld /home/admin/esteraone.green /home/esteraone/esteraone.green
readlink -f /home/admin/esteraone.green/bestlaptop-erp-be
echo "=== green-be pm2 app cwd realpath ==="
readlink -f /home/admin/esteraone.green/bestlaptop-erp-be/.env
md5sum /home/admin/esteraone.green/bestlaptop-erp-be/.env /home/esteraone/esteraone.green/bestlaptop-erp-be/.env
