#!/bin/sh
# Официальный образ nginx запускает по очереди все исполняемые скрипты из
# /docker-entrypoint.d/ перед стартом nginx — это единственное, что делает
# данный скрипт, отдельный ENTRYPOINT не нужен.
#
# Подставляет значения переменных окружения (заданы в docker-compose.yml из
# .env) в config.template.json, который читает Angular-приложение при загрузке
# страницы (см. frontend/src/main.ts, вызывается до bootstrapApplication).
# Благодаря этому апи-адрес и OAuth client id меняются без пересборки образа —
# достаточно поменять .env и перезапустить контейнер (docker compose up -d).
set -eu

envsubst '${API_URL} ${GOOGLE_CLIENT_ID} ${VK_CLIENT_ID}' \
  < /etc/nginx/config.template.json \
  > /usr/share/nginx/html/config.json
