#!/usr/bin/env bash
set -eu
cd -- "$(dirname -- "$0")"
if [ -e .env ]; then echo '.env already exists; edit it rather than replacing database passwords.' >&2; exit 1; fi
read -r -p 'Restaurant domain (e.g. restaurant.example.com): ' domain
read -r -p 'Administrator email: ' email
if [[ ! "$domain" =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]] || [[ ! "$email" =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; then echo 'Enter a valid hostname and email.' >&2; exit 1; fi
umask 077
{
    printf 'APP_DOMAIN=%s\nADMIN_EMAIL=%s\n' "$domain" "$email"
    printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 32)"
    printf 'APP_DB_PASSWORD=%s\n' "$(openssl rand -hex 32)"
    printf 'JWT_KEY=%s\n' "$(openssl rand -hex 48)"
    printf 'ADMIN_PASSWORD=%s\n' "$(openssl rand -hex 16)"
} > .env
echo 'Created private .env. Open it with nano to read the initial administrator password.'
