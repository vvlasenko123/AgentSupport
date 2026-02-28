# Используем Python 3.9 как в твоем логе
FROM python:3.9-slim

# Устанавливаем рабочую директорию
WORKDIR /app

# Копируем файл зависимостей
COPY requirements.txt .

# Устанавливаем зависимости (игнорируем кэш, чтобы точно поставилось)
RUN pip install --no-cache-dir -r requirements.txt

# Копируем остальной код
COPY . .

# Команда для запуска
CMD ["python", "train.py"]