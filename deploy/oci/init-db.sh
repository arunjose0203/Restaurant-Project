#!/usr/bin/env bash
set -eu
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=ON_ERROR_STOP=1 --set=app_password="$APP_DB_PASSWORD" <<'SQL'
CREATE ROLE restaurant_app LOGIN PASSWORD :'app_password';
ALTER DATABASE restaurant OWNER TO restaurant_app;
GRANT ALL ON SCHEMA public TO restaurant_app;
SQL
