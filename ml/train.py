import os
import json
import sys
import subprocess

# --- ХАК ДЛЯ УСТАНОВКИ ЗАВИСИМОСТЕЙ НА ЛЕТУ ---
def install_deps():
    try:
        import accelerate
    except ImportError:
        print("Библиотека 'accelerate' не найдена. Устанавливаю...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", "accelerate", "transformers[torch]", "-U"])
        print("Установка завершена. Перезапустите скрипт или продолжаю выполнение...")

install_deps()

# Теперь импортируем всё остальное
import torch
from sklearn.model_selection import train_test_split
from transformers import (
    BertTokenizer,
    BertForSequenceClassification,
    Trainer,
    TrainingArguments,
    DataCollatorWithPadding
)

# Параметры
MODEL_NAME = "cointegrated/rubert-tiny2"
SAVE_PATH = "./rubert"
DATA_FILE = "train_data.json"
EPOCHS = 15  # Теперь точно 15

CATEGORY_MAP = {
    "MALFUNCTION": 0, "REPAIR_SERVICE": 1, "MESSAGE_NOTIFICATION": 2,
    "INFORMATION_REQUEST": 3, "CALIBRATION_SETTING": 4, "CONNECTION_INTEGRATION": 5,
    "FEEDBACK_COMPLAINT": 6, "WARRANTY_RETURN": 7, "SOFTWARE_UPDATE": 8
}
ID_TO_CATEGORY = {v: k for k, v in CATEGORY_MAP.items()}

class ErisDataset(torch.utils.data.Dataset):
    def __init__(self, encodings, labels):
        self.encodings = encodings
        self.labels = labels

    def __getitem__(self, idx):
        item = {key: torch.tensor(val[idx]) for key, val in self.encodings.items()}
        item['labels'] = torch.tensor(self.labels[idx], dtype=torch.long)
        return item

    def __len__(self):
        return len(self.labels)

def train():
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"--- Запуск обучения на {device.upper()} (Эпох: {EPOCHS}) ---")

    if not os.path.exists(DATA_FILE):
        print(f"Ошибка: Файл {DATA_FILE} не найден!")
        return

    # 1. Загрузка данных
    with open(DATA_FILE, 'r', encoding='utf-8') as f:
        raw_data = json.load(f)

    texts, labels = [], []
    for item in raw_data:
        cat_name = item.get("category")
        if cat_name in CATEGORY_MAP:
            texts.append(item["text"])
            labels.append(CATEGORY_MAP[cat_name])

    print(f"Загружено примеров: {len(texts)}")

    # 2. Модель и токенизатор
    tokenizer = BertTokenizer.from_pretrained(MODEL_NAME)
    model = BertForSequenceClassification.from_pretrained(
        MODEL_NAME,
        num_labels=len(CATEGORY_MAP)
    ).to(device)

    # 3. Подготовка данных
    # Stratify гарантирует, что в тесте будут все категории пропорционально
    train_texts, val_texts, train_labels, val_labels = train_test_split(
        texts, labels, test_size=0.15, random_state=42, stratify=labels
    )

    train_encodings = tokenizer(train_texts, truncation=True, padding=True, max_length=512)
    val_encodings = tokenizer(val_texts, truncation=True, padding=True, max_length=512)

    train_dataset = ErisDataset(train_encodings, train_labels)
    val_dataset = ErisDataset(val_encodings, val_labels)

    # 4. Аргументы (решаем проблему 3-х эпох)
    training_args = TrainingArguments(
        output_dir='./results',
        overwrite_output_dir=True,          # Очищает старые чекпоинты (ВАЖНО!)
        num_train_epochs=EPOCHS,            # Строго 15
        per_device_train_batch_size=16,
        per_device_eval_batch_size=16,
        learning_rate=3e-5,                 # Оптимально для tiny2
        weight_decay=0.01,
        evaluation_strategy="epoch",
        save_strategy="epoch",
        logging_steps=10,
        load_best_model_at_end=True,        # Вернет лучшую модель, а не последнюю
        metric_for_best_model="eval_loss",
        report_to="none",                   # Отключает wandb и прочее
        fp16=torch.cuda.is_available()      # Ускорение на GPU
    )

    # 5. Trainer
    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=val_dataset,
        data_collator=DataCollatorWithPadding(tokenizer=tokenizer)
    )

    # 6. Обучение
    trainer.train()

    # Сохранение
    os.makedirs(SAVE_PATH, exist_ok=True)
    model.save_pretrained(SAVE_PATH)
    tokenizer.save_pretrained(SAVE_PATH)

    with open(os.path.join(SAVE_PATH, "category_mapping.json"), "w") as f:
        json.dump(ID_TO_CATEGORY, f, ensure_ascii=False, indent=4)

    print(f"\n[ГОТОВО] Модель сохранена в {SAVE_PATH}")

if __name__ == "__main__":
    train()