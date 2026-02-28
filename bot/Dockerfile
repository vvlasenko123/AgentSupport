FROM python:3.11-slim

WORKDIR /app

# Копируем requirements.txt (он в той же папке, что и Dockerfile)
COPY requirements.txt .

# Устанавливаем зависимости
RUN pip install --no-cache-dir -r requirements.txt

# Копируем код бота (main.py в той же папке)
COPY main.py .

# Запускаем бота
CMD ["python", "main.py"]