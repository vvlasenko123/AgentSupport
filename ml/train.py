# -*- coding: utf-8 -*-
"""
Дообучение RuBERT на train_data.json (и данных из data/) для:
- тональности (0/1/2 -> Нейтральный/Положительный/Негативный),
- категории обращения (APPEAL_CATEGORIES),
- эмбеддинги текста для поиска ближайшего issue_summary.
"""

import json
import os
import torch
from torch.utils.data import Dataset
from transformers import AutoTokenizer, AutoModel, AutoConfig
import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import LabelEncoder

from config import RUBERT_MODEL, MODELS_DIR, EMOTIONAL_TONE_MAP, APPEAL_CATEGORIES
from preprocess import load_train_data, prepare_records

os.makedirs(MODELS_DIR, exist_ok=True)


class TextDataset(Dataset):
    def __init__(self, texts, tokenizer, max_length=256):
        self.texts = texts
        self.tokenizer = tokenizer
        self.max_length = max_length

    def __len__(self):
        return len(self.texts)

    def __getitem__(self, i):
        enc = self.tokenizer(
            self.texts[i],
            max_length=self.max_length,
            padding="max_length",
            truncation=True,
            return_tensors="pt",
        )
        return {k: v.squeeze(0) for k, v in enc.items()}


def get_embeddings(model, tokenizer, texts, device, batch_size=8, max_length=256):
    """Получить [CLS] эмбеддинги для списка текстов."""
    model.eval()
    all_emb = []
    for i in range(0, len(texts), batch_size):
        batch = texts[i : i + batch_size]
        enc = tokenizer(
            batch,
            max_length=max_length,
            padding=True,
            truncation=True,
            return_tensors="pt",
        ).to(device)
        with torch.no_grad():
            out = model(**enc)
            cls = out.last_hidden_state[:, 0, :].cpu().numpy()
        all_emb.append(cls)
    return np.vstack(all_emb)


def main():
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"Device: {device}")

    records = load_train_data()
    records = prepare_records(records)
    if not records:
        raise SystemExit("Нет данных для обучения. Проверьте train_data.json и data/.")

    texts = [r["text"] for r in records]
    labels_sentiment = [r["label"] for r in records]
    categories = [r["category"] for r in records]
    issue_summaries = [r["issue_summary"] for r in records]
    answers = [r.get("answer", "") for r in records]

    print(f"Загружаем {RUBERT_MODEL}...")
    tokenizer = AutoTokenizer.from_pretrained(RUBERT_MODEL)
    config = AutoConfig.from_pretrained(RUBERT_MODEL)
    model = AutoModel.from_pretrained(RUBERT_MODEL, config=config).to(device)

    print("Считаем эмбеддинги...")
    emb = get_embeddings(model, tokenizer, texts, device)

    # Сохраняем эмбеддинги, issue_summary и ответы для nearest neighbor
    np.save(os.path.join(MODELS_DIR, "train_embeddings.npy"), emb)
    with open(os.path.join(MODELS_DIR, "train_issue_summaries.json"), "w", encoding="utf-8") as f:
        json.dump(issue_summaries, f, ensure_ascii=False)
    with open(os.path.join(MODELS_DIR, "train_answers.json"), "w", encoding="utf-8") as f:
        json.dump(answers, f, ensure_ascii=False)
    with open(os.path.join(MODELS_DIR, "train_texts.json"), "w", encoding="utf-8") as f:
        json.dump(texts, f, ensure_ascii=False)

    # Классификатор тональности
    le_sent = LabelEncoder()
    y_sent = le_sent.fit_transform(labels_sentiment)  # 0,1,2
    clf_sent = LogisticRegression(max_iter=500, random_state=42)
    clf_sent.fit(emb, y_sent)
    np.save(os.path.join(MODELS_DIR, "label_encoder_sentiment.npy"), le_sent.classes_)
    import joblib
    joblib.dump(clf_sent, os.path.join(MODELS_DIR, "sentiment_classifier.joblib"))

    # Классификатор категории
    le_cat = LabelEncoder()
    y_cat = le_cat.fit_transform(categories)
    clf_cat = LogisticRegression(max_iter=500, random_state=42)
    clf_cat.fit(emb, y_cat)
    np.save(os.path.join(MODELS_DIR, "label_encoder_category.npy"), le_cat.classes_)
    joblib.dump(clf_cat, os.path.join(MODELS_DIR, "category_classifier.joblib"))

    # Сохраняем токенайзер и модель для инференса
    tokenizer.save_pretrained(MODELS_DIR)
    model.save_pretrained(MODELS_DIR)

    print("Готово. Модели сохранены в", MODELS_DIR)


if __name__ == "__main__":
    main()
