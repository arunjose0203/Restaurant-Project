# Oracle A1 deployment

Use an Ubuntu 24.04 ARM64 VM with 2 OCPUs, 12 GB RAM and 50 GB boot storage, subject to your account's Always Free allowance. A domain/subdomain you control is required for this HTTPS configuration. Domain registration is separate from Oracle's compute free tier.

Allow inbound TCP 22 only from your administrator IP; allow TCP 80 and 443 from clients. Keep PostgreSQL 5432 and API 8080 unpublished. OCI security lists/NSGs are additive: remove any existing broad SSH rule if restricting SSH. Use a public subnet with an internet gateway, a 0.0.0.0/0 route to that gateway, and a public IPv4 address.

1. Install Docker Engine and the Compose plugin using Docker's official Ubuntu repository instructions: https://docs.docker.com/engine/install/ubuntu/
2. Upload and extract the project into `/home/ubuntu/restaurant`.
3. Point a DNS A record for your hostname at the VM public IPv4. Do not add an AAAA record unless IPv6 is configured end to end.
4. Run:

```bash
cd ~/restaurant/deploy/oci
bash setup-env.sh
nano .env
sudo docker compose build
sudo docker compose up -d db
sudo docker compose run --rm api --migrate
sudo docker compose up -d
sudo docker compose ps
```

Stop if migration fails; inspect `sudo docker compose logs db` and the migration output. The first migration creates schema, menu/tables, and your initial admin. Subsequent migrations preserve the database. Caddy obtains HTTPS certificates automatically when DNS points to this VM and TCP 80/443 are reachable. WebSocket proxying supports SignalR without an additional load balancer.

Open `https://YOUR_DOMAIN`, sign in using ADMIN_EMAIL/ADMIN_PASSWORD from `.env`, create named staff accounts and test a complete order-to-payment flow across separate devices. Verify that the web app shows a real login rather than the demo banner. `curl -fsS https://YOUR_DOMAIN/health` checks API process liveness; a successful login also checks the database.

The API uses a non-superuser PostgreSQL role that owns the restaurant database for migrations. Database connections stay inside the host's private Docker network; only this local container link disables PostgreSQL TLS. For a remote database use its certificate-verified TLS connection instead. Browser/mobile traffic uses HTTPS. Persistent named volumes retain data across restarts. Do not use `docker compose down -v` on a working restaurant.

Set `EXPO_PUBLIC_API_URL=https://YOUR_DOMAIN` in the mobile `.env`, then rebuild/restart mobile. Both web and mobile use the same database through the API. A1 is ARM64, so build containers on the VM or explicitly build linux/arm64 images.

Back up the database, and copy the backup off this VM:

```bash
mkdir -p backups
chmod 700 backups
umask 077
sudo docker compose exec -T db pg_dump -U postgres -d restaurant -Fc > "backups/restaurant-$(date +%F-%H%M).dump"
```

Schedule backups in your production operations and test restoring into a separate database before relying on them. Docker volumes are persistence, not backups. Oracle may reclaim idle Always Free VMs and free capacity may be unavailable.

Updating `.env` does not automatically change an existing PostgreSQL user's password: database initialization scripts run only on an empty data volume. Rotate credentials deliberately in PostgreSQL and then update/recreate the API. The generated admin password is used only when seeding the first user.

Updates: upload updated source without replacing `.env`, run `docker compose build`, then `docker compose run --rm api --migrate`, and finally `docker compose up -d`. Keep a backup before schema updates. Do not run the destructive online smoke test against restaurant production data.

These files have been statically inspected; no Oracle VM has been provisioned or remotely deployed from this workspace. VM-level firewall rules may also need adjustment. Preserve OCI's existing link-local/iSCSI rules; do not flush iptables or blindly enable UFW.
