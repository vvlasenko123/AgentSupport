FROM python:3.9-slim

# Устанавливаем рабочую директорию
WORKDIR /app

# Копируем зависимости
COPY requirements.txt .
# Установка torch для CPU, чтобы образ не весил 5ГБ
RUN pip install --no-cache-dir -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cpu

# Копируем всё содержимое папки ml в /app
COPY . .

# Проверяем, что папка с моделью на месте (для логов сборки)
RUN ls -l /app/rubert

EXPOSE 8000

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]