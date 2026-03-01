# ML-сервис AgentSupport

Парсинг писем и заполнение полей жалобы (ComplaintModel): ФИО, контакты, серийные номера, тип устройства, тональность, категория, суть вопроса.

## Как запустить и проверить

### Вариант 1: Всё в Docker (рекомендуется)

Из **корня проекта** (AgentSupport):

```bash
# Собрать образ (внутри запустится обучение на train_data.json — первый раз может занять 5–10 мин)
docker compose build ml

# Запустить сервис
docker compose up -d ml
```

Сервис будет на **http://localhost:6767**. Проверка:

```bash
# Проверка, что сервис живой
curl http://localhost:6767/health

# Тест разбора письма (упрощённый контракт)
curl -X POST http://localhost:6767/process/simple \
  -H "Content-Type: application/json" \
  -d '{
    "fromName": "Иванов Иван Иванович",
    "fromEmail": "ivanov@company.ru",
    "subject": "Датчик ЭРИС-210 с/н 210201384 не выходит на связь при калибровке",
    "content": "Добрый день. При калибровке магнитной 100 ppm ноль откалибровали, дальше тишина. Телефон +7 912 345-67-89."
  }'
```

В ответе увидишь поля жалобы: `fio`, `objectName`, `phoneNumber`, `serialNumbers`, `deviceType`, `emotionalTone`, `issueSummary`, `category` и т.д.

### Вариант 2: Локально (обучение и API на своей машине)

```bash
cd ml

# Создать виртуальное окружение (по желанию)
python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate

# Установить зависимости
pip install -r requirements.txt

# 1) Обучение (скачает RuBERT, дообучит на train_data.json, сохранит в ml/models/)
python train.py

# 2) Запуск API на порту 6767
python -m uvicorn app:app --host 0.0.0.0 --port 6767
```

В другом терминале те же проверки:

```bash
curl http://localhost:6767/health
curl -X POST http://localhost:6767/process/simple \
  -H "Content-Type: application/json" \
  -d '{"fromEmail":"test@mail.ru","subject":"Сломался ЭРИС-230","content":"Не работает дисплей."}'
```

### Полный контракт (как с C# бекенда)

Эндпоинт **POST /process** принимает тело в формате письма (camelCase):

```bash
curl -X POST http://localhost:6767/process \
  -H "Content-Type: application/json" \
  -d '{
    "fromEmail": "support@eris.ru",
    "subject": "Калибровка ЭРИС-210",
    "content": "Текст письма с серийным номером с/н 210201384.",
    "sentAtUtc": "2025-03-01T12:00:00Z"
  }'
```

---

## Модель

- **RuBERT** (`cointegrated/rubert-tiny2`) — дообучение на `train_data.json` и данных из папки `data/`.
- **Тональность**: Нейтральный / Положительный / Негативный (в датасете: label 0/1/2).
- **Категория**: AppealCategory (Поломка, Ремонт, Калибровка, Запрос информации и т.д.).
- **Тип устройства**: по каталогу ЭРИС в `data/devices.txt`.
- **`issueSummary` и `suggestedAnswer`**: теперь не копируются из ближайшего примера целиком, а генерируются на основе признаков запроса, предсказанной категории и нескольких релевантных кейсов из обучающей выборки.

## Дообучение отдельно

Скрипт `train.py` читает `train_data.json` и при наличии — JSON-файлы из папки `data/`. Сохраняет артефакты в `ml/models/`. Чтобы Docker использовал уже дообученную модель (без обучения при сборке), смонтируйте в `docker-compose` для сервиса `ml` том `./ml/models:/app/models` и уберите/закомментируйте в Dockerfile строку `RUN python train.py`.

## API

| Метод | Путь | Описание |
|-------|------|----------|
| GET | /health | Проверка работы |
| POST | /process | Вход: EmailMessageModel (camelCase), выход: ComplaintModel |
| POST | /process/simple | Упрощённо: fromName, fromEmail, subject, content, sentAtUtc |

Поля `email` и `submissionDate` в ответе берутся из письма; остальные заполняет модель.
