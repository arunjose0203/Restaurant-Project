FROM node:24-bookworm-slim AS build
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
COPY shared/ /src/shared/
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM caddy:2
COPY --from=build /src/web/dist /srv
COPY deploy/oci/Caddyfile /etc/caddy/Caddyfile
