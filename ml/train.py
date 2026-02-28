from transformers import BertTokenizer, BertForSequenceClassification, Trainer, TrainingArguments
import torch
import os
import json
from sklearn.model_selection import train_test_split

# Параметры
MODEL_NAME = "cointegrated/rubert-tiny2"
SAVE_PATH = "./rubert"
DATA_FILE = "train_data.json"
EPOCHS = 15

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
    print(f"--- Запуск обучения: 9 категорий, {EPOCHS} эпох ---")

    if not os.path.exists(DATA_FILE):
        print(f"Файл {DATA_FILE} не найден!")
        return

    # Загрузка данных
    with open(DATA_FILE, 'r', encoding='utf-8') as f:
        raw_data = json.load(f)

    texts = []
    labels = []

    for item in raw_data:
        cat_name = item.get("category")
        if cat_name in CATEGORY_MAP:
            texts.append(item["text"])
            labels.append(CATEGORY_MAP[cat_name])

    print(f"Загружено примеров: {len(texts)}")

    # Токенизация
    tokenizer = BertTokenizer.from_pretrained(MODEL_NAME)
    model = BertForSequenceClassification.from_pretrained(MODEL_NAME, num_labels=len(CATEGORY_MAP))

    # Размораживаем все слои
    for param in model.parameters():
        param.requires_grad = True

    train_texts, val_texts, train_labels, val_labels = train_test_split(
        texts, labels, test_size=0.15, random_state=42
    )

    train_encodings = tokenizer(train_texts, truncation=True, padding=True, max_length=512)
    val_encodings = tokenizer(val_texts, truncation=True, padding=True, max_length=512)

    train_dataset = ErisDataset(train_encodings, train_labels)
    val_dataset = ErisDataset(val_encodings, val_labels)

    # Настройка параметров обучения
    training_args = TrainingArguments(
        output_dir='./results',
        num_train_epochs=EPOCHS,
        per_device_train_batch_size=8,
        learning_rate=1e-5,  # Уменьшаем скорость обучения
        evaluation_strategy="epoch",
        save_strategy="epoch",
        load_best_model_at_end=False,
        metric_for_best_model="eval_loss",
        lr_scheduler_type="linear",  # Линейный scheduler
        report_to="none"
    )

    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=val_dataset,
    )

    # Запуск тренировки
    trainer.train()
    model.save_pretrained(SAVE_PATH)
    tokenizer.save_pretrained(SAVE_PATH)

    # Сохраняем маппинг категорий
    with open(os.path.join(SAVE_PATH, "category_mapping.json"), "w") as f:
        json.dump(ID_TO_CATEGORY, f)

    print(f"Обучение завершено. Модель сохранена в {SAVE_PATH}")

if __name__ == "__main__":
    train()