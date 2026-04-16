FROM node:20-alpine AS build
WORKDIR /app

ARG VITE_DISABLE_AUTH=true
ENV VITE_DISABLE_AUTH=$VITE_DISABLE_AUTH

# Copy package files for layer caching
COPY src/Scaffold.Web/package.json src/Scaffold.Web/package-lock.json ./
RUN npm ci

# Copy source and build
COPY src/Scaffold.Web/ .
RUN npm run build

FROM nginx:alpine AS final

# Create non-root user and configure writable directories for nginx
RUN adduser --disabled-password --gecos "" --uid 1000 appuser \
    && chown -R appuser:appuser /var/cache/nginx \
    && chown -R appuser:appuser /var/log/nginx \
    && chown -R appuser:appuser /etc/nginx/conf.d \
    && touch /var/run/nginx.pid \
    && chown appuser:appuser /var/run/nginx.pid

COPY --from=build --chown=appuser:appuser /app/dist /usr/share/nginx/html
COPY --chown=appuser:appuser src/Scaffold.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080

USER appuser
