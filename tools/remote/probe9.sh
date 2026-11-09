#!/usr/bin/env bash
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
echo "=== 5438 prod connectivity ==="
psql -h $H -p 5438 -U postgres -d esteraone -Atc "select current_database(), pg_size_pretty(pg_database_size(current_database()));" 2>&1
echo "=== 5439 dev connectivity ==="
psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select current_database(), pg_size_pretty(pg_database_size(current_database()));" 2>&1
echo "=== table counts 5438 ==="
psql -h $H -p 5438 -U postgres -d esteraone -Atc "select count(*) from information_schema.tables where table_schema='public';" 2>&1
echo "=== table counts 5439 ==="
psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select count(*) from information_schema.tables where table_schema='public';" 2>&1
echo "=== tables like kanban/service/absensi (5438) ==="
psql -h $H -p 5438 -U postgres -d esteraone -Atc "select table_name from information_schema.tables where table_schema='public' and (table_name ilike '%kanban%' or table_name ilike '%absensi%' or table_name ilike '%service%' or table_name ilike '%activity%' or table_name ilike '%attendance%') order by 1;" 2>&1
echo "=== max updated/created 5438 recent ==="
psql -h $H -p 5438 -U postgres -d esteraone -Atc "select relname, n_live_tup from pg_stat_user_tables order by n_live_tup desc limit 15;" 2>&1
