# ---------- Сборка Angular-приложения ----------
FROM node:22-alpine AS build
WORKDIR /src

# Отдельным слоем — чтобы npm ci не перезапускался при правках исходников.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# ---------- Runtime: nginx отдаёт статику и проксирует /api на бэкенд ----------
FROM nginx:1.27-alpine

COPY --from=build /src/dist/motoparts-frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Шаблон и скрипт для рантайм-конфигурации (см. main.ts и docker/20-envsubst-config.sh).
# Кладём вне html-корня, чтобы наружу был виден только готовый config.json.
COPY docker/config.template.json /etc/nginx/config.template.json
COPY docker/20-envsubst-config.sh /docker-entrypoint.d/20-envsubst-config.sh
RUN chmod +x /docker-entrypoint.d/20-envsubst-config.sh

# ENTRYPOINT/CMD не переопределяем — используются из базового образа nginx:
# он сам прогоняет скрипты из /docker-entrypoint.d/ перед запуском nginx.
