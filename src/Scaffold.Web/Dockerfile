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
COPY --from=build /app/dist /usr/share/nginx/html
COPY src/Scaffold.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
