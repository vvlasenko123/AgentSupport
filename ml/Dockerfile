FROM python:3.11-slim

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY config.py preprocess.py model.py app.py train.py ./
COPY train_data.json ./
COPY data/ ./data/

ENV RUBERT_MODEL=cointegrated/rubert-tiny2

# Дообучение на train_data.json (и данных из data/) при сборке
RUN python train.py

EXPOSE 6767
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "6767"]
