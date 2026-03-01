# -*- coding: utf-8 -*-
"""Конфигурация модели и API."""

import os

# RuBERT: лёгкая модель для русского языка
RUBERT_MODEL = os.environ.get("RUBERT_MODEL", "cointegrated/rubert-tiny2")
NER_MODEL = os.environ.get("NER_MODEL", "")

# Пути к данным
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
TRAIN_DATA_JSON = os.path.join(BASE_DIR, "train_data.json")
# Папка с дополнительными JSON для обучения (рядом с ml или внутри ml)
DATA_DIR = os.path.join(BASE_DIR, "data")
MODELS_DIR = os.path.join(BASE_DIR, "models")
DEVICES_LIST_PATH = os.path.join(BASE_DIR, "data", "devices.txt")

# Маппинг тональности: в датасете label 0=нейтральный, 1=положительный, 2=негативный
EMOTIONAL_TONE_MAP = {0: "Нейтральный", 1: "Положительный", 2: "Негативный"}

# Категории обращений (соответствуют AppealCategory на бекенде)
APPEAL_CATEGORIES = [
    "MALFUNCTION",           # Поломка
    "REPAIR_SERVICE",        # Ремонт
    "MESSAGE_NOTIFICATION",  # Сообщение
    "INFORMATION_REQUEST",   # Запрос информации
    "CALIBRATION_SETTING",   # Калибровка
    "CONNECTION_INTEGRATION",# Подключение
    "FEEDBACK_COMPLAINT",    # Жалоба
    "WARRANTY_RETURN",       # Возврат
    "SOFTWARE_UPDATE",       # Обновление ПО",
]

# Статус по умолчанию для новой жалобы (backend ожидает строку)
DEFAULT_STATUS = "Waiting"

# Если модель не смогла заполнить поле — подставляем прочерк (для строк) или null остаётся null
EMPTY_PLACEHOLDER = "—"
