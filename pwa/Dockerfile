# Build context: pwa/
# Vite bakes VITE_* vars into the JS bundle at build time, so they must be
# passed as build args (not runtime env vars) and must be reachable from
# the end user's browser, not just from inside the docker network.
#
# Build:  docker build -t rushorder-pwa --build-arg VITE_API_URL=http://SERVER_IP:5000/api .

# ─── Stage: build ─────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG VITE_API_URL
ARG VITE_RESTAURANT_ID
ARG VITE_STRIPE_PUBLISHABLE_KEY
ARG VITE_VAPID_PUBLIC_KEY
ENV VITE_API_URL=$VITE_API_URL \
    VITE_RESTAURANT_ID=$VITE_RESTAURANT_ID \
    VITE_STRIPE_PUBLISHABLE_KEY=$VITE_STRIPE_PUBLISHABLE_KEY \
    VITE_VAPID_PUBLIC_KEY=$VITE_VAPID_PUBLIC_KEY

RUN npm run build

# ─── Stage: runtime (nginx sirviendo el build estático) ───────────────────────
FROM nginx:1.27-alpine AS runtime

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost/ || exit 1

EXPOSE 80
