# AgentSupport

Development
```bash
  docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d
```

Production
```bash
  docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

1 раз создать контейнеры tools, но НЕ запускать
```bash
  docker compose --profile tools up --no-start db_seed db_cleanup_all
```

Запуск сидера (каждый запуск добавит еще записей)
```bash
    docker compose start db_seed
```

Повторить еще раз:
```bash
    docker compose start db_seed
```
Очистка
```bash
docker compose start db_cleanup_all
```