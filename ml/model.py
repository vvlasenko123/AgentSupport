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
        self.train_answers = None
        self.train_texts = None
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
        path_ans = os.path.join(self.models_dir, "train_answers.json")
        path_txt = os.path.join(self.models_dir, "train_texts.json")
        if os.path.isfile(path_emb) and os.path.isfile(path_sum):
            self.train_emb = np.load(path_emb)
            with open(path_sum, "r", encoding="utf-8") as f:
                self.train_issue_summaries = json.load(f)
            if os.path.isfile(path_ans):
                with open(path_ans, "r", encoding="utf-8") as f:
                    self.train_answers = json.load(f)
            if os.path.isfile(path_txt):
                with open(path_txt, "r", encoding="utf-8") as f:
                    self.train_texts = json.load(f)

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

    def _nearest_issue_summary_and_answer(self, emb, device_type=None):
        """Ближайший по эмбеддингу пример; при указании device_type приоритет у примеров с этим устройством."""
        if self.train_emb is None or self.train_issue_summaries is None or emb is None:
            return "", None
        sim = np.dot(self.train_emb, emb.T).ravel()
        idx = int(np.argmax(sim))
        if device_type and self.train_texts and len(self.train_texts) == len(sim):
            device_lower = device_type.lower()
            mask = np.array([device_lower in (t or "").lower() for t in self.train_texts])
            if np.any(mask):
                sim_masked = np.where(mask, sim, -np.inf)
                idx_masked = int(np.argmax(sim_masked))
                if sim_masked[idx_masked] > -np.inf:
                    idx = idx_masked
        summary = self.train_issue_summaries[idx]
        answer = None
        if self.train_answers is not None and idx < len(self.train_answers):
            a = (self.train_answers[idx] or "").strip()
            answer = a if a else None
        return summary, answer

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
        # Игнорируем placeholder из Swagger/API (например "string")
        if from_name and from_name.strip():
            name = from_name.strip()
            if name.lower() not in ("string", "null", ""):
                return name
        combined = f"{subject or ''} {content or ''}"
        # "я Даниил Олегович Мезев", "меня зовут Иван Петров", "это Алексей Сергеевич"
        for pattern in [
            r"(?:я|меня\s+зовут|это)\s+([А-ЯЁа-яё]+\s+[А-ЯЁа-яё]+(?:\s+[А-ЯЁа-яё]+)?)(?=[\s,\.]|$)",
            r"(?:^|[,\.\s])([А-ЯЁа-яё]+\s+[А-ЯЁа-яё]+(?:\s+[А-ЯЁа-яё]+)?)(?=[,\.\s]|$)",
        ]:
            m = re.search(pattern, combined[:800], re.I)
            if m:
                fio = m.group(1).strip()
                if len(fio) > 2 and len(fio) < 120:
                    return fio
        return None

    @staticmethod
    def _extract_object_name(subject, content, from_email):
        """Название предприятия или объекта, откуда поступило обращение."""
        combined = f"{subject or ''} {content or ''}"
        # ПАО/ООО/АО/ОАО «Название», компания X, от лица X, на объекте X, ГНС «X», нефтебаза, и т.д.
        org_patterns = [
            r"(?:ПАО|ООО|АО|ОАО)\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+)[»\"']?",
            r"(?:компания|организация|предприятие|служба)\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+)[»\"']?",
            r"от\s+лица\s+([А-ЯЁа-яё0-9\s\-]+?)(?:\s*\.|,|\n|$)",
            r"на\s+объекте\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+)[»\"']?",
            r"ГНС\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+)[»\"']?",
            r"нефтебаза\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+)[»\"']?",
            r"(?:установке|на\s+установке)\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+)[»\"']?",
        ]
        for pat in org_patterns:
            m = re.search(pat, combined[:1500], re.I)
            if m:
                name = m.group(1).strip()
                if len(name) > 2 and len(name) < 150:
                    return name
        # Fallback: домен из email как название объекта
        if from_email and "@" in from_email:
            return from_email.split("@", 1)[-1].strip()
        return None

    def predict(self, subject, content, from_email=None, from_name=None):
        """
        По теме и телу письма возвращает словарь полей для ComplaintModel.
        """
        self.load()
        full_text = f"{subject or ''}\n{content or ''}".strip() or " "
        emb = self._embed(full_text)

        fio = self._extract_fio(subject, content, from_name)
        fio = fio.strip() if fio else None  # null, если не найден
        phone = self._extract_phone(full_text)
        serials = self._extract_serials(full_text)
        device_type = self._detect_device_type(full_text)
        emotional_tone = self._predict_sentiment(emb)
        category = self._predict_category(emb)
        issue_summary, suggested_answer = self._nearest_issue_summary_and_answer(emb, device_type=device_type)
        if not issue_summary and full_text.strip():
            issue_summary = full_text[:300].strip()
        issue_summary = issue_summary.strip() if issue_summary else EMPTY_PLACEHOLDER
        object_name = self._extract_object_name(subject, content, from_email)

        return {
            "fio": fio,  # None → null в JSON, если не найден
            "objectName": object_name,  # название предприятия/объекта
            "phoneNumber": phone,
            "serialNumbers": serials if serials else [],
            "deviceType": device_type,
            "emotionalTone": emotional_tone.strip() if emotional_tone else EMPTY_PLACEHOLDER,
            "issueSummary": issue_summary,
            "category": category.strip() if category else EMPTY_PLACEHOLDER,
            "status": DEFAULT_STATUS,
            "suggestedAnswer": suggested_answer,  # предполагаемый ответ на вопрос (из обучающей выборки)
        }
