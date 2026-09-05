# Build the Angular frontend image.
# Run from repo root: docker build -f HUB.VIEW/Dockerfile -t nexus-hub-view .

FROM node:22-alpine AS build
WORKDIR /src
COPY HUB.VIEW/package*.json ./
RUN npm ci
COPY HUB.VIEW/ ./
RUN npm run build -- --configuration docker

FROM nginx:alpine AS runtime
COPY --from=build /src/dist/nexus-hub/browser /usr/share/nginx/html
# Runtime config: the docker variant (relative /api URLs) becomes the active config.json.
# Prod deployments can still mount their own config.json over this without rebuilding.
COPY --from=build /src/dist/nexus-hub/browser/config.docker.json /usr/share/nginx/html/config.json
COPY HUB.VIEW/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
