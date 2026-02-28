# ENIGMA Frontend

Фронтенд-приложение для работы с обращениями:

- список обращений: `/appeals`
- детальная карточка обращения: `/appeals/:id`

Стек:

- React 19 + TypeScript
- Vite
- React Router
- Axios
- SCSS

## Возможности

- Загрузка списка обращений с API
- Поиск по названию и ID
- Переход в карточку конкретного обращения
- Таблица детального просмотра обращения
- Состояния загрузки и ошибок
- Адаптивная верстка + плавные анимации

## Структура проекта

```text
frontend/
  src/
    api/
      axiosInstance.ts
      appealsApi.ts
    components/
      Header/
      Footer/
    pages/
      AppealsList/
      AppealPage/
      NotFound/
    types/
      appeal.ts
    App.tsx
    App.scss
    index.scss
    index.tsx
  Dockerfile
  nginx.conf
  package.json
```

## Быстрый старт (локально)

Требования:

- Node.js 20+
- npm 10+

Установка и запуск:

```bash
npm i
npm run dev
```

Приложение по умолчанию доступно на:

- `http://localhost:5173`

## Скрипты

```bash
npm run dev      # запуск dev-сервера
npm run build    # production-сборка (tsc + vite build)
npm run preview  # локальный просмотр production-сборки
npm run lint     # eslint проверка
```

## API и конфигурация

Базовый URL API находится в:

- `src/api/axiosInstance.ts`

## Docker

В проекте подготовлен production Dockerfile:

- многостадийная сборка (`node:20-alpine` -> `nginx:alpine`)
- SPA-роутинг через `nginx.conf` (`try_files ... /index.html`)

Сборка образа:

```bash
docker build -t enigma-frontend ./frontend
```

Запуск контейнера:

```bash
docker run --rm -p 80:80 enigma-frontend
```

После запуска приложение доступно на:

- `http://localhost/5173`
