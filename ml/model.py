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
from transformers import AutoTokenizer, AutoModel, pipeline

from config import (
    MODELS_DIR,
    RUBERT_MODEL,
    NER_MODEL,
    EMOTIONAL_TONE_MAP,
    DEFAULT_STATUS,
    EMPTY_PLACEHOLDER,
)
from preprocess import load_devices_list


# Серийные номера: с/н 123, с/н 210201384, (с/н ...), № 411005, ID: b55dd3e8290d
RE_SERIAL = re.compile(
    r"(?:с/н|заводской\s+номер|№|id)\s*[:\s]*\(?([a-f0-9\-]+|\d{5,})\)?",
    re.I,
)
RE_SERIAL_NUM = re.compile(r"\b(\d{9})\b")
# Телефон: +7 ..., 8 (999) 123-45-67, 8-999-123-45-67
RE_PHONE = re.compile(
    r"(?:\+7|8)\s*[\(\s\-]*\d{3}[\)\s\-]*\d{3}[\s\-]?\d{2}[\s\-]?\d{2}\b"
)
RE_WS = re.compile(r"\s+")
RE_NAME_TOKEN = re.compile(r"^[А-ЯЁ][а-яё-]{1,30}$")
RE_NAME_PREFIX = re.compile(r"^(?:добрый\s+день|здравствуйте|привет|коллеги|уважаемые[\w\s,]*|меня\s+зовут|это|я)\s*[,:!\-\s]*", re.I)


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
        self.device_prefix_map = {}
        self.ner = None

    ISSUE_PATTERNS = [
        ("высокая температура", ["жар", "жаре", "высок", "перегрев", "нагрев", "температур"]),
        ("низкая температура", ["мороз", "холод", "низк", "замерзан", "минус"]),
        ("высокая влажность", ["влажн", "конденсат", "сырост"]),
        ("ложные срабатывания", ["ложн", "тревог", "срабатыван"]),
        ("неверные показания", ["неправильн", "некорректн", "ошибочн", "дрейф", "завыш", "заниж", "показыва"]),
        ("калибровка", ["калибров", "поверк", "нул", "чувствительност"]),
        ("нет связи", ["не выходит на связь", "связ", "modbus", "rs-485", "crc"]),
        ("ошибка канала", ["ошибк", "канал", "low flow", "timeout"]),
        ("не реагирует на газ", ["не реагирует", "нет реакции", "показания 0"]),
        ("питание или аккумулятор", ["не держит заряд", "аккумулятор", "питани"]),
        ("дисплей", ["диспле", "экран"]),
    ]
    PROBLEM_KEYS = (
        "влажн", "конденсат", "ложн", "тревог", "срабатыв",
        "температур", "мороз", "жар", "перегрев", "охлажд",
        "дрейф", "нестабил", "прыга", "ошиб", "неправ",
        "калиб", "поверк", "сенсор", "датчик", "фильтр", "помп",
        "забор", "проб", "заед", "подклинив", "защелк", "фикс", "рычаг",
        "модул", "пружин", "износ", "смазк", "ремонт", "замен",
    )
    ORG_STOPWORDS = {
        "нас", "нами", "вам", "вами", "объекте", "объекте", "установке",
        "службы", "система", "система", "участке", "заказчика", "компании",
    }
    FIO_NOISE = {
        "Добрый", "Здравствуйте", "Привет", "Коллеги", "Уважаемые",
        "Газоанализатор", "Система", "Док", "ЭРИС", "Advant",
    }

    ANSWER_HINTS = {
        "высокая температура": (
            "Уточните фактическую температуру в зоне установки и сравните ее с допустимым диапазоном прибора. "
            "При перегреве проверьте место монтажа, наличие прямого нагрева корпуса и выполните контрольную проверку показаний после стабилизации температуры."
        ),
        "низкая температура": (
            "Проверьте, укладываются ли условия эксплуатации в допустимый температурный диапазон. "
            "При работе на морозе оцените прогрев прибора, состояние кабелей и отсутствие обмерзания газового тракта."
        ),
        "высокая влажность": (
            "Проверьте наличие конденсата, состояние фильтров и защиту сенсора от влаги. "
            "Если прибор установлен во влажной зоне, нужна просушка тракта и повторная проверка после стабилизации среды."
        ),
        "ложные срабатывания": (
            "Нужно проверить внешние факторы, которые могут давать паразитный сигнал: блики, конденсат, аэрозоли, помехи по питанию или порогам."
        ),
        "неверные показания": (
            "Рекомендуется сверить показания с контрольной смесью или эталонным каналом, затем проверить ноль, чувствительность и текущее состояние сенсора."
        ),
        "калибровка": (
            "Перед повторной настройкой проверьте давление и расход ПГС, герметичность соединений и корректность последовательности калибровки."
        ),
        "нет связи": (
            "Проверьте линию связи, полярность интерфейса, настройки порта и отсутствие конфликта адресов устройства."
        ),
        "ошибка канала": (
            "Сначала проверьте конкретный измерительный канал, состояние сенсора и наличие сервисных ошибок в журнале прибора."
        ),
        "не реагирует на газ": (
            "Проверьте поступление газа к сенсору, правильность сборки после обслуживания и отсутствие блокировки газового канала."
        ),
        "питание или аккумулятор": (
            "Нужно проверить батарею, цепь питания и фактическое время автономной работы относительно нормы."
        ),
        "дисплей": (
            "Проверьте шлейф, целостность экрана и наличие механических повреждений корпуса после удара или перегрева."
        ),
    }

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
        self.device_prefix_map = self._build_device_prefix_map(self.devices)
        if NER_MODEL:
            try:
                self.ner = pipeline(
                    task="ner",
                    model=NER_MODEL,
                    aggregation_strategy="simple",
                    device=0 if self.device == "cuda" else -1,
                )
            except Exception:
                self.ner = None

    def _embed(self, text):
        if not text or not self.tokenizer or self.bert is None:
            return None
        enc = self.tokenizer(
            text,
            max_length=512,
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

    def _top_examples(self, emb, device_type=None, top_k=3):
        """Возвращает top-k релевантных примеров; device_type повышает релевантность, но не диктует текст ответа."""
        if self.train_emb is None or self.train_issue_summaries is None or emb is None:
            return []
        sim = np.dot(self.train_emb, emb.T).ravel()
        weighted = sim.copy()
        if device_type and self.train_texts and len(self.train_texts) == len(sim):
            device_lower = device_type.lower()
            mask = np.array([device_lower in (t or "").lower() for t in self.train_texts])
            weighted = weighted + mask.astype(float) * 0.15
        top_idx = np.argsort(weighted)[::-1][:top_k]
        examples = []
        for idx in top_idx:
            example = {
                "summary": self.train_issue_summaries[idx] if idx < len(self.train_issue_summaries) else "",
                "answer": self.train_answers[idx] if self.train_answers is not None and idx < len(self.train_answers) else "",
                "text": self.train_texts[idx] if self.train_texts is not None and idx < len(self.train_texts) else "",
                "score": float(sim[idx]),
            }
            examples.append(example)
        return examples

    @staticmethod
    def _clean_text(text):
        text = re.sub(r"\s+", " ", (text or "").strip())
        return text.strip(" .,\n\t")

    @staticmethod
    def _device_priority(name):
        lowered = (name or "").lower()
        score = 0
        if lowered.startswith("дгс "):
            score += 5
        if lowered.startswith("пг "):
            score += 5
        if lowered.startswith("док "):
            score += 5
        if lowered.startswith("сгм "):
            score += 5
        if lowered.startswith("ип-"):
            score += 4
        if "-rf" in lowered:
            score -= 3
        if lowered.startswith("эрис-"):
            score += 2
        return score + min(len(name or ""), 20) / 100.0

    @classmethod
    def _build_device_prefix_map(cls, devices):
        prefix_map = {}
        for device in devices or []:
            for match in re.findall(r"\b(\d{3})\b", device):
                current = prefix_map.get(match)
                if current is None or cls._device_priority(device) > cls._device_priority(current):
                    prefix_map[match] = device
        return prefix_map

    @staticmethod
    def _safe_cut(text, max_chars):
        text = text or ""
        return text if len(text) <= max_chars else text[:max_chars].rstrip()

    @staticmethod
    def _split_sentences(text):
        return [s.strip() for s in re.split(r"(?<=[\.\!\?])\s+", text or "") if s.strip()]

    @staticmethod
    def _normalize_spaces(text):
        return RE_WS.sub(" ", (text or "").strip())

    @staticmethod
    def _normalize_person_candidate(text):
        text = ComplaintPredictor._normalize_spaces(text)
        text = RE_NAME_PREFIX.sub("", text).strip(" ,.!:-")
        text = re.sub(r"\b(?:из|от|компания|организация|предприятие|служба|объект)\b.*$", "", text, flags=re.I).strip(" ,.!:-")
        return text

    @classmethod
    def _is_valid_person(cls, text):
        text = cls._normalize_person_candidate(text)
        parts = [p for p in text.split() if p]
        if not parts or len(parts) > 3:
            return False
        if any(p in cls.FIO_NOISE for p in parts):
            return False
        return all(RE_NAME_TOKEN.match(p) for p in parts)

    @staticmethod
    def _normalize_org_name(text):
        text = ComplaintPredictor._normalize_spaces(text)
        text = text.strip(" ,.!:-\"'«»")
        text = re.sub(r"\s{2,}", " ", text)
        return text

    @classmethod
    def _is_valid_org(cls, text):
        text = cls._normalize_org_name(text)
        if not text or len(text) < 3 or len(text) > 150:
            return False
        lowered = text.lower()
        if lowered in cls.ORG_STOPWORDS:
            return False
        if re.fullmatch(r"[a-z0-9\.\-]+\.[a-z]{2,}", lowered):
            return False
        return True

    def _extract_issue_signals(self, text):
        text_lower = (text or "").lower()
        signals = []
        for label, patterns in self.ISSUE_PATTERNS:
            if any(p in text_lower for p in patterns):
                signals.append(label)
        return signals

    def _extract_problem_focus(self, text, device_type=None):
        sentences = self._split_sentences(text)
        candidates = []
        for sentence in sentences:
            sentence_lower = sentence.lower()
            score = 0
            for word in self.PROBLEM_KEYS:
                if word in sentence_lower:
                    score += 2
            for _, words in self.ISSUE_PATTERNS:
                if any(word in sentence_lower for word in words):
                    score += 1
            if "?" in sentence:
                score += 1
            if any(greet in sentence_lower for greet in ["добрый день", "здравствуйте", "привет"]):
                score -= 2
            if score > 0:
                candidates.append((score, self._clean_text(sentence)))
        candidates.sort(key=lambda item: (item[0], len(item[1])), reverse=True)
        if candidates:
            focus = candidates[0][1]
        elif sentences:
            focus = self._clean_text(sentences[0])
        else:
            focus = device_type or "обращение"
        focus = re.sub(r"^(?:добрый\s+день|здравствуйте|коллеги|привет)[!,.:\s-]*", "", focus, flags=re.I).strip()
        focus = re.sub(r"^(?:я|мы)\s+[А-ЯЁа-яё\s-]{2,40}[,.\s-]+", "", focus, flags=re.I).strip()
        focus = re.sub(r"^(?:меня\s+зовут|это)\s+[А-ЯЁа-яё\s-]{2,50}[,.\s-]+", "", focus, flags=re.I).strip()
        if device_type and device_type.lower() not in focus.lower():
            focus = f"{device_type}: {focus}"
        return focus[:220]

    def _build_issue_summary(self, full_text, device_type=None, category=None):
        focus = self._extract_problem_focus(full_text, device_type=device_type)
        signals = self._extract_issue_signals(full_text)
        focus = self._clean_text(focus)
        if signals and not any(signal in focus.lower() for signal in signals):
            focus = f"{focus} ({', '.join(signals[:2])})"
        if focus and not focus.endswith("."):
            focus += "."
        return self._safe_cut(focus, 220)

    def _compose_suggested_answer(self, full_text, device_type=None, category=None, examples=None):
        signals = self._extract_issue_signals(full_text)
        examples = examples or []
        parts = []

        if device_type:
            parts.append(f"По обращению по прибору {device_type} требуется первичная техническая проверка условий эксплуатации и текущего состояния прибора.")
        else:
            parts.append("По обращению требуется первичная техническая проверка условий эксплуатации и текущего состояния прибора.")

        if signals:
            for signal in signals[:2]:
                hint = self.ANSWER_HINTS.get(signal)
                if hint and hint not in parts:
                    parts.append(hint)
        elif category == "INFORMATION_REQUEST":
            parts.append("Нужно уточнить точную модификацию прибора и подготовить ответ по документации и регламенту эксплуатации.")
        elif category == "WARRANTY_RETURN":
            parts.append("Нужно запросить серийный номер, дату поставки, фото или описание дефекта и проверить применимость гарантийного сценария.")

        borrowed = []
        for example in examples:
            for sentence in self._split_sentences(example.get("answer", "")):
                cleaned = self._clean_text(sentence)
                if len(cleaned) < 30:
                    continue
                if any(token in cleaned.lower() for token in ["проверьте", "убедитесь", "рекомендуется", "уточните", "направьте"]):
                    if cleaned not in borrowed:
                        borrowed.append(cleaned)
                if len(borrowed) >= 2:
                    break
            if len(borrowed) >= 2:
                break

        for sentence in borrowed:
            paraphrased = sentence
            paraphrased = re.sub(r"^Для\s+[^:]+:\s*", "", paraphrased, flags=re.I)
            paraphrased = paraphrased[:1].upper() + paraphrased[1:]
            if paraphrased not in parts:
                parts.append(paraphrased)

        if category in {"MALFUNCTION", "CALIBRATION_SETTING", "REPAIR_SERVICE"}:
            parts.append("Если после проверки условий среды и контрольного теста отклонение сохраняется, прибор нужно направить в сервис с указанием серийного номера и наблюдаемых симптомов.")

        text = " ".join(self._clean_text(part).rstrip(".") + "." for part in parts if part)
        return text[:900] if text else None

    @staticmethod
    def _extract_serials(text):
        serials = []
        for m in RE_SERIAL.finditer(text):
            value = m.group(1).strip()
            if re.fullmatch(r"\d{9}", value):
                serials.append(value)
        for m in RE_SERIAL_NUM.finditer(text):
            s = m.group(1)
            if s not in serials:
                serials.append(s)
        return list(dict.fromkeys(serials))

    @staticmethod
    def _extract_phone(text):
        m = RE_PHONE.search(text)
        return m.group(0).strip() if m else None

    def _detect_device_type_from_text(self, text):
        if not text or not self.devices:
            return None
        text_lower = text.lower()
        for dev in self.devices:
            if dev and dev.lower() in text_lower:
                return dev
        return None

    def _detect_device_type_from_serials(self, serials):
        for serial in serials or []:
            if re.fullmatch(r"\d{9}", serial):
                prefix = serial[:3]
                device = self.device_prefix_map.get(prefix)
                if device:
                    return device
        return None

    def _detect_device_type(self, text, serials=None):
        device = self._detect_device_type_from_text(text)
        if device:
            return device
        return self._detect_device_type_from_serials(serials)

    def _extract_fio_ner(self, text):
        if self.ner is None:
            return None
        try:
            entities = self.ner(self._safe_cut(text, 1000))
        except Exception:
            return None
        best_text = None
        best_score = 0.0
        for entity in entities:
            group = str(entity.get("entity_group", "")).upper()
            if group != "PER":
                continue
            word = self._normalize_person_candidate(str(entity.get("word", "")))
            if not self._is_valid_person(word):
                continue
            score = float(entity.get("score", 0.0))
            if score > best_score:
                best_score = score
                best_text = word
        return best_text

    def _extract_fio(self, subject, content, from_name):
        # Игнорируем placeholder из Swagger/API (например "string")
        if from_name and from_name.strip():
            name = self._normalize_person_candidate(from_name)
            if name.lower() not in ("string", "null", "") and self._is_valid_person(name):
                return name
        combined = f"{subject or ''} {content or ''}"
        # "я Даниил Олегович Мезев", "меня зовут Иван Петров", "это Алексей Сергеевич"
        for pattern in [
            r"(?:я|меня\s+зовут|это)\s+([А-ЯЁа-яё]+\s+[А-ЯЁа-яё]+(?:\s+[А-ЯЁа-яё]+)?)(?=[\s,\.]|$)",
            r"(?:^|[,\.\s])([А-ЯЁа-яё]+\s+[А-ЯЁа-яё]+(?:\s+[А-ЯЁа-яё]+)?)(?=[,\.\s]|$)",
        ]:
            m = re.search(pattern, combined[:800], re.I)
            if m:
                fio = self._normalize_person_candidate(m.group(1))
                if self._is_valid_person(fio):
                    return fio
        # Если полного ФИО нет, берём имя.
        for pattern in [
            r"(?:меня\s+зовут|я|это)\s+([А-ЯЁа-яё]{2,30})(?=[\s,\.!]|$)",
            r"(?:с\s+уважением|спасибо[,!\s]+)\s*([А-ЯЁа-яё]{2,30})(?=[\s,\.!]|$)",
        ]:
            m = re.search(pattern, combined[:800], re.I)
            if m:
                name = self._normalize_person_candidate(m.group(1))
                if self._is_valid_person(name):
                    return name
        ner_name = self._extract_fio_ner(combined)
        if ner_name:
            return ner_name
        return None

    @staticmethod
    def _extract_object_name(subject, content, from_email):
        """Название предприятия или объекта, откуда поступило обращение."""
        combined = f"{subject or ''} {content or ''}"
        # ПАО/ООО/АО/ОАО «Название», компания X, от лица X, на объекте X, ГНС «X», нефтебаза, и т.д.
        org_patterns = [
            r"((?:ПАО|ООО|АО|ОАО)\s+[«\"']?[А-ЯЁа-яё0-9\s\-]+[»\"']?)",
            r"((?:компания|организация|предприятие)\s+[«\"']?[А-ЯЁа-яё0-9\s\-]+[»\"']?)",
            r"от\s+лица\s+([А-ЯЁа-яё0-9\s\-«»\"']+?)(?:\s*\.|,|\n|$)",
            r"на\s+объекте\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+?)(?:[»\"']?(?:\s*\.|,|\n|$))",
            r"на\s+участке\s+([А-ЯЁа-яё0-9\s\-]+?)(?:\s*\.|,|\n|$)",
            r"ГНС\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+?)(?:[»\"']?(?:\s*\.|,|\n|$))",
            r"нефтебаза\s+[«\"']?([А-ЯЁа-яё0-9\s\-]+?)(?:[»\"']?(?:\s*\.|,|\n|$))",
        ]
        for pat in org_patterns:
            m = re.search(pat, combined[:1500], re.I)
            if m:
                name = ComplaintPredictor._normalize_org_name(m.group(1))
                if ComplaintPredictor._is_valid_org(name):
                    return name
        # Email-домен не считаем названием объекта: это давало слишком много мусора.
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
        device_type = self._detect_device_type(full_text, serials=serials)
        emotional_tone = self._predict_sentiment(emb)
        category = self._predict_category(emb)
        examples = self._top_examples(emb, device_type=device_type, top_k=3)
        issue_summary = self._build_issue_summary(full_text, device_type=device_type, category=category)
        suggested_answer = self._compose_suggested_answer(
            full_text,
            device_type=device_type,
            category=category,
            examples=examples,
        )
        if not issue_summary and full_text.strip():
            issue_summary = self._clean_text(full_text[:300])
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
