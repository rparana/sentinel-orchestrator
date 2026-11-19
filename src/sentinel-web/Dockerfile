FROM node:26-alpine AS build
WORKDIR /app

RUN npm install -g pnpm@12

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN CI=true pnpm build

FROM nginx:alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/sentinel-web/browser /usr/share/nginx/html

EXPOSE 80
