import json
import os
import shutil
import torch
import numpy as np
from sklearn.model_selection import train_test_split
from transformers import (
    BertTokenizer,
    BertForSequenceClassification,
    Trainer,
    TrainingArguments,
    BertConfig
)

# --- 1. КОНФИГУРАЦИЯ ---
CATEGORY_MAP = {
    "MALFUNCTION": 0,
    "REPAIR_SERVICE": 1,
    "MESSAGE_NOTIFICATION": 2,
    "INFORMATION_REQUEST": 3,
    "CALIBRATION_SETTING": 4,
    "CONNECTION_INTEGRATION": 5,
    "FEEDBACK_COMPLAINT": 6,
    "WARRANTY_RETURN": 7,
    "SOFTWARE_UPDATE": 8
}

ID_TO_CATEGORY = {v: k for k, v in CATEGORY_MAP.items()}

MODEL_NAME = "cointegrated/rubert-tiny2"
SAVE_PATH = "./rubert"
DATA_FILE = "train_data.json"
EPOCHS = 15
BATCH_SIZE = 8  # Уменьши до 4, если мало оперативной памяти
MAX_LENGTH = 512


class ErisDataset(torch.utils.data.Dataset):
    def __init__(self, encodings, labels):
        self.encodings = encodings
        self.labels = labels

    def __getitem__(self, idx):
        item = {key: torch.tensor(val[idx]) for key, val in self.encodings.items()}
        item['labels'] = torch.tensor(self.labels[idx])
        return item

    def __len__(self):
        return len(self.labels)


def train():
    print(f"🚀 Запуск профессионального переобучения...")
    print(f"📊 Категорий: {len(CATEGORY_MAP)}, Эпох: {EPOCHS}")

    if not os.path.exists(DATA_FILE):
        print(f"❌ Ошибка: Файл {DATA_FILE} не найден!")
        return

    # 2. Загрузка данных
    with open(DATA_FILE, 'r', encoding='utf-8') as f:
        raw_data = json.load(f)

    texts = []
    labels = []
    for item in raw_data:
        cat_name = item.get("category")
        if cat_name in CATEGORY_MAP:
            texts.append(item["text"])
            labels.append(CATEGORY_MAP[cat_name])

    print(f"✅ Загружено примеров: {len(texts)}")

    # Разделение на обучение и валидацию
    train_texts, val_texts, train_labels, val_labels = train_test_split(
        texts, labels, test_size=0.15, random_state=42, stratify=labels
    )

    # 3. Подготовка модели и токенайзера
    tokenizer = BertTokenizer.from_pretrained(MODEL_NAME)

    # Чтобы избежать ошибок несовпадающих слоев, принудительно задаем конфиг
    config = BertConfig.from_pretrained(MODEL_NAME, num_labels=len(CATEGORY_MAP))
    model = BertForSequenceClassification.from_pretrained(MODEL_NAME, config=config)

    train_encodings = tokenizer(train_texts, truncation=True, padding=True, max_length=MAX_LENGTH)
    val_encodings = tokenizer(val_texts, truncation=True, padding=True, max_length=MAX_LENGTH)

    train_dataset = ErisDataset(train_encodings, train_labels)
    val_dataset = ErisDataset(val_encodings, val_labels)

    # 4. Настройка гиперпараметров (TrainingArguments)
    training_args = TrainingArguments(
        output_dir='./results',
        num_train_epochs=EPOCHS,
        per_device_train_batch_size=BATCH_SIZE,
        per_device_eval_batch_size=BATCH_SIZE,
        learning_rate=5e-5,  # Чуть выше для маленькой модели
        warmup_steps=50,  # Плавный вход в обучение
        weight_decay=0.01,  # Регуляризация
        logging_dir='./logs',
        logging_steps=10,
        evaluation_strategy="epoch",  # Проверка каждую эпоху
        save_strategy="no",  # Не плодим промежуточные чекпоинты (экономим место)
        load_best_model_at_end=False,  # Нам важен прогресс всех 15 эпох
        report_to="none"
    )

    # 5. Запуск процесса
    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=val_dataset
    )

    print("🛠 Начинаю циклы обучения...")
    trainer.train()

    # 6. Финальное сохранение
    if os.path.exists(SAVE_PATH):
        shutil.rmtree(SAVE_PATH)  # Очищаем старую папку перед сохранением

    model.save_pretrained(SAVE_PATH)
    tokenizer.save_pretrained(SAVE_PATH)

    with open(os.path.join(SAVE_PATH, "category_mapping.json"), "w") as f:
        json.dump(ID_TO_CATEGORY, f, ensure_ascii=False, indent=4)

    print(f"✨ Бинго! Модель обучена на 15 эпох и сохранена в {SAVE_PATH}")


if __name__ == "__main__":
    train()