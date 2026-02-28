# -*- coding: utf-8 -*-
"""
Инференс: парсинг письма -> поля ComplaintModel.
- Извлечение ФИО, телефона, серийных номеров (регулярки + эвристики).
- Тональность, категория, issue_summary — RuBERT + обученные головы.
- Тип устройства — матчинг по каталогу устройств ЭРИС.
"""

import json
import os
import re
import numpy as np
import torch
import joblib
from transformers import AutoTokenizer, AutoModel

from config import (
    MODELS_DIR,
    RUBERT_MODEL,
    EMOTIONAL_TONE_MAP,
    DEFAULT_STATUS,
    DEVICES_LIST_PATH,
    EMPTY_PLACEHOLDER,
)
from preprocess import load_devices_list


# Серийные номера: с/н 123, с/н 210201384, (с/н ...), № 411005, ID: b55dd3e8290d
RE_SERIAL = re.compile(
    r"(?:с/н|заводской\s+номер|№|id)\s*[:\s]*\(?([a-f0-9\-]+|\d{5,})\)?",
    re.I,
)
RE_SERIAL_NUM = re.compile(r"\b(\d{6,})\b")
# Телефон: +7 ..., 8 (999) 123-45-67, 8-999-123-45-67
RE_PHONE = re.compile(
    r"(?:\+7|8)\s*[\(\s\-]*\d{3}[\)\s\-]*\d{3}[\s\-]?\d{2}[\s\-]?\d{2}\b"
)


class ComplaintPredictor:
    def __init__(self, models_dir=None):
        self.models_dir = models_dir or MODELS_DIR
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.tokenizer = None
        self.bert = None
        self.clf_sentiment = None
        self.clf_category = None
        self.le_sentiment = None
        self.le_category = None
        self.train_emb = None
        self.train_issue_summaries = None
        self.devices = []

    def load(self):
        if self.bert is not None:
            return
        # Локально дообученные артефакты или базовая модель
        if os.path.isfile(os.path.join(self.models_dir, "config.json")):
            self.tokenizer = AutoTokenizer.from_pretrained(self.models_dir)
            self.bert = AutoModel.from_pretrained(self.models_dir).to(self.device)
        else:
            self.tokenizer = AutoTokenizer.from_pretrained(RUBERT_MODEL)
            self.bert = AutoModel.from_pretrained(RUBERT_MODEL).to(self.device)

        path_sent = os.path.join(self.models_dir, "sentiment_classifier.joblib")
        path_cat = os.path.join(self.models_dir, "category_classifier.joblib")
        if os.path.isfile(path_sent):
            self.clf_sentiment = joblib.load(path_sent)
            self.le_sentiment = np.load(
                os.path.join(self.models_dir, "label_encoder_sentiment.npy"),
                allow_pickle=True,
            )
        if os.path.isfile(path_cat):
            self.clf_category = joblib.load(path_cat)
            self.le_category = np.load(
                os.path.join(self.models_dir, "label_encoder_category.npy"),
                allow_pickle=True,
            )

        path_emb = os.path.join(self.models_dir, "train_embeddings.npy")
        path_sum = os.path.join(self.models_dir, "train_issue_summaries.json")
        if os.path.isfile(path_emb) and os.path.isfile(path_sum):
            self.train_emb = np.load(path_emb)
            with open(path_sum, "r", encoding="utf-8") as f:
                self.train_issue_summaries = json.load(f)

        self.devices = load_devices_list()

    def _embed(self, text):
        if not text or not self.tokenizer or self.bert is None:
            return None
        enc = self.tokenizer(
            text,
            max_length=256,
            padding=True,
            truncation=True,
            return_tensors="pt",
        ).to(self.device)
        with torch.no_grad():
            out = self.bert(**enc)
            emb = out.last_hidden_state[:, 0, :].cpu().numpy()
        return emb

    def _predict_sentiment(self, emb):
        if self.clf_sentiment is None or self.le_sentiment is None or emb is None:
            return "Нейтральный"
        pred = self.clf_sentiment.predict(emb)[0]
        label = int(self.le_sentiment[pred]) if pred < len(self.le_sentiment) else 0
        return EMOTIONAL_TONE_MAP.get(label, "Нейтральный")

    def _predict_category(self, emb):
        if self.clf_category is None or self.le_category is None or emb is None:
            return "INFORMATION_REQUEST"
        pred = self.clf_category.predict(emb)[0]
        return str(self.le_category[pred])

    def _nearest_issue_summary(self, emb):
        if self.train_emb is None or self.train_issue_summaries is None or emb is None:
            return ""
        sim = np.dot(self.train_emb, emb.T).ravel()
        idx = np.argmax(sim)
        return self.train_issue_summaries[idx]

    @staticmethod
    def _extract_serials(text):
        serials = []
        for m in RE_SERIAL.finditer(text):
            serials.append(m.group(1).strip())
        for m in RE_SERIAL_NUM.finditer(text):
            s = m.group(1)
            if s not in serials and len(s) >= 5:
                serials.append(s)
        return list(dict.fromkeys(serials))

    @staticmethod
    def _extract_phone(text):
        m = RE_PHONE.search(text)
        return m.group(0).strip() if m else None

    def _detect_device_type(self, text):
        if not text or not self.devices:
            return None
        text_lower = text.lower()
        for dev in self.devices:
            if dev and dev.lower() in text_lower:
                return dev
        return None

    @staticmethod
    def _extract_fio(subject, content, from_name):
        # Из письма часто "Кот Максим Игоревич" или в начале тела
        if from_name and from_name.strip():
            return from_name.strip()
        combined = f"{subject or ''} {content or ''}"
        # Паттерн: Фамилия Имя Отчество или Имя Отчество
        fio_match = re.search(
            r"(?:^|[,\.\s])([А-ЯЁа-яё]+\s+[А-ЯЁа-яё]+(?:\s+[А-ЯЁа-яё]+)?)(?=[,\.\s]|$)",
            combined[:500],
        )
        if fio_match:
            return fio_match.group(1).strip()
        return ""

    def predict(self, subject, content, from_email=None, from_name=None):
        """
        По теме и телу письма возвращает словарь полей для ComplaintModel.
        """
        self.load()
        full_text = f"{subject or ''}\n{content or ''}".strip() or " "
        emb = self._embed(full_text)

        fio = self._extract_fio(subject, content, from_name)
        phone = self._extract_phone(full_text)
        serials = self._extract_serials(full_text)
        device_type = self._detect_device_type(full_text)
        emotional_tone = self._predict_sentiment(emb)
        category = self._predict_category(emb)
        issue_summary = self._nearest_issue_summary(emb)
        if not issue_summary and full_text.strip():
            issue_summary = full_text[:300].strip()

        # Незаполненные поля: строки — прочерк, опциональные — null (оставляем None)
        return {
            "fio": fio.strip() or EMPTY_PLACEHOLDER,
            "phoneNumber": phone,  # None → null в JSON
            "serialNumbers": serials if serials else [],  # пустой список, не null
            "deviceType": device_type,  # None → null в JSON
            "emotionalTone": emotional_tone.strip() if emotional_tone else EMPTY_PLACEHOLDER,
            "issueSummary": issue_summary.strip() if issue_summary else EMPTY_PLACEHOLDER,
            "category": category.strip() if category else EMPTY_PLACEHOLDER,
            "status": DEFAULT_STATUS,
        }
