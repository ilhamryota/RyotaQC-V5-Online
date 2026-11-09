#!/usr/bin/env bash
A=/home/admin/esteraone.admin/src/server.js
echo "=== head 140 ==="
sed -n '1,140p' $A
echo "=== grep deploy/pod/remote ==="
grep -n "deploy\|restart\|pm2\|8002\|8004\|greenBePort\|blueBePort" $A | head -60
