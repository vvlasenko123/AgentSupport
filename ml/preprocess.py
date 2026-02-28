# preprocess.py
import re

def extract_device_type_and_serial(text):
    # Извлекаем тип устройства и заводской номер (9 цифр)
    device_type_match = re.search(r'\b(\d{3})\d{6}\b', text)
    if device_type_match:
        device_type = device_type_match.group(1)
        serial_number = device_type_match.group(0)
    else:
        device_type = "Неизвестно"
        serial_number = None
    return device_type, serial_number

def detect_emotional_tone(text):
    # Простой анализ эмоционального окраса
    if "переживаю" in text or "волнуются" in text:
        return "негативный"
    elif "счастлив" in text or "доволен" in text:
        return "позитивный"
    return "нейтральный"

def preprocess_data(text):
    # Преобразование текста в токены для RuBERT
    inputs = tokenizer(text, return_tensors="pt", padding=True, truncation=True)
    return inputs