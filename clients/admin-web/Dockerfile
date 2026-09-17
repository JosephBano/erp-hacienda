# syntax=docker/dockerfile:1.7

# Build context: ./clients/admin-web

FROM node:20-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf

COPY --from=build /src/dist/admin-web/browser /usr/share/nginx/html

EXPOSE 80
