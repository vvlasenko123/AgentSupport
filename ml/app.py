import os
import re
import uuid
from datetime import datetime
from typing import List, Optional
from enum import Enum

import torch
from fastapi import FastAPI
from pydantic import BaseModel
from transformers import BertTokenizer, BertForSequenceClassification
from docx import Document
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

app = FastAPI(title="ERIS ML Analysis Service")


# --- 1. ОПРЕДЕЛЕНИЕ КАТЕГОРИЙ (ENUM) ---
class AppealCategory(str, Enum):
    MALFUNCTION = "Поломка"
    REPAIR_SERVICE = "Ремонт"
    MESSAGE_NOTIFICATION = "Сообщение"
    INFORMATION_REQUEST = "Запрос информации"
    CALIBRATION_SETTING = "Калибровка"
    CONNECTION_INTEGRATION = "Подключение"
    FEEDBACK_COMPLAINT = "Жалоба"
    WARRANTY_RETURN = "Возврат"
    SOFTWARE_UPDATE = "Обновление ПО"


# --- НАСТРОЙКИ ПУТЕЙ ---
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.path.join(BASE_DIR, "rubert")
DATA_PATH = os.path.join(BASE_DIR, "data")

# --- ЗАГРУЗКА МОДЕЛИ ТОНАЛЬНОСТИ ---
tokenizer = BertTokenizer.from_pretrained(MODEL_PATH)
model = BertForSequenceClassification.from_pretrained(MODEL_PATH)
model.eval()

# --- СПРАВОЧНИК ОБОРУДОВАНИЯ ЭРИС ---
ERIS_MODELS_MAP = {
    "400": "Корректировочная станция Док ЭРИС-400",
    "414": "ПГ ЭРИС-414 (портативный многоканальный)",
    "411": "ПГ ЭРИС-411 (портативный одноканальный)",
    "230": "ДГС ЭРИС-230 (стационарный с OLED)",
    "210": "ДГС ЭРИС-210 (стационарный)",
    "211": "ДГС ЭРИС-210-RF (беспроводной)",
    "330": "Извещатель пламени ИП-330",
    "130": "Контроллер СГМ ЭРИС-130",
    "110": "Контроллер СГМ ЭРИС-110",
    "412": "ERIS Simple X",
    "215": "ДГС ЭРИС-ФИД",
    "700": "ЭРИС Оксициркон",
    "800": "LoraBOX точка доступа",
    "advant": "Стационарный газоанализатор Advant"
}


# --- ЛОГИКА БАЗЫ ЗНАНИЙ (RAG) ---
def load_knowledge_base():
    kb = []
    if os.path.exists(DATA_PATH):
        for file in os.listdir(DATA_PATH):
            if file.endswith(".docx"):
                try:
                    doc = Document(os.path.join(DATA_PATH, file))
                    for para in doc.paragraphs:
                        text = para.text.strip()
                        if len(text) > 20:
                            kb.append({"source": file, "content": text})
                except Exception as e:
                    print(f"Ошибка чтения {file}: {e}")
    return kb


KNOWLEDGE_BASE = load_knowledge_base()


# --- МОДЕЛИ ДАННЫХ ---
class MailRequest(BaseModel):
    text: str


class AnalysisResponse(BaseModel):
    id: str
    submission_date: str
    fio: str
    object_name: str
    phone_number: str
    email: str
    serial_numbers: List[str]
    device_type: str
    emotional_tone: str
    category: str  # <--- ДОБАВЛЕНО ПОЛЕ КАТЕГОРИИ
    issue_summary: str
    suggested_answer: str


# --- 2. ЛОГИКА ОПРЕДЕЛЕНИЯ КАТЕГОРИИ ---
def detect_category(text: str) -> str:
    """Определяет категорию на основе ключевых слов и паттернов."""
    t = text.lower()

    # Правила маппинга
    if any(w in t for w in ["не работает", "ошибка", "сломался", "fault", "неисправен", "горит красным"]):
        return AppealCategory.MALFUNCTION.value
    if any(w in t for w in ["ремонт", "сервис", "починить", "замена сенсора", "отправить вам"]):
        return AppealCategory.REPAIR_SERVICE.value
    if any(w in t for w in ["калибровка", "поверка", "ноль", "чувствительность", "пгс", "баллон"]):
        return AppealCategory.CALIBRATION_SETTING.value
    if any(w in t for w in ["связь", "подключение", "modbus", "rs-485", "lora", "интеграция", "шина"]):
        return AppealCategory.CONNECTION_INTEGRATION.value
    if any(w in t for w in ["обновление", "прошивка", "firmware", "драйвер", "версия по"]):
        return AppealCategory.SOFTWARE_UPDATE.value
    if any(w in t for w in ["возврат", "гарантия", "брак", "некомплект"]):
        return AppealCategory.WARRANTY_RETURN.value
    if any(w in t for w in ["подскажите", "сколько", "информация", "паспорт", "рэ", "описание"]):
        return AppealCategory.INFORMATION_REQUEST.value
    if any(w in t for w in ["жалоба", "плохо", "недоволен", "претензия"]):
        return AppealCategory.FEEDBACK_COMPLAINT.value
    if any(w in t for w in ["спасибо", "благодарим", "принято", "в работе"]):
        return AppealCategory.MESSAGE_NOTIFICATION.value

    return AppealCategory.INFORMATION_REQUEST.value  # По умолчанию


# --- ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ (extract_entities, create_smart_summary, get_suggested_answer - остаются без изменений) ---
def extract_entities(text: str):
    phone = re.search(r'(\+7|8)\s?\(?\d{3}\)?\s?\d{3}-?\d{2}-?\d{2}', text)
    email = re.search(r'[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}', text)
    serials = re.findall(r'\b\d{9}\b', text)
    fio_match = re.search(r'([А-Я][а-я]+)\s([А-Я][а-я]+)\s?([А-Я][а-я]+)?', text)

    device_type = "Оборудование ЭРИС"
    if serials:
        prefix = serials[0][:3]
        device_type = ERIS_MODELS_MAP.get(prefix, device_type)
    else:
        for key, name in ERIS_MODELS_MAP.items():
            if key.lower() in text.lower():
                device_type = name
                break

    return (
        phone.group(0) if phone else "-",
        email.group(0) if email else "-",
        serials,
        fio_match.group(0) if fio_match else "-",
        device_type
    )


def create_smart_summary(text: str):
    clean = re.sub(r'(Добрый день|Здравствуйте|Приветствую|С уважением)[^.!]*[.!]', '', text, flags=re.I)
    keywords = ["не выходит", "не работает", "ошибка", "сломался", "калибровка", "поверка", "связь"]
    sentences = [s.strip() for s in clean.split('.') if len(s.strip()) > 10]
    for s in sentences:
        if any(k in s.lower() for k in keywords):
            return s[:250]
    return sentences[0][:250] if sentences else "Краткая суть не определена"


def get_suggested_answer(user_text: str, tone: str):
    """Ищет подходящий ответ. Если тон положительный - благодарит."""
    if tone == "положительный":
        return "Благодарим за ваш отзыв! Мы ценим доверие к оборудованию ЭРИС и всегда готовы помочь в решении технических вопросов."

    if not KNOWLEDGE_BASE:
        return "Инструкции не найдены в базе (проверьте папку data)."

    corpus = [user_text] + [item['content'] for item in KNOWLEDGE_BASE]
    vectorizer = TfidfVectorizer().fit_transform(corpus)
    vectors = vectorizer.toarray()
    cosine_sim = cosine_similarity([vectors[0]], vectors[1:])[0]
    best_idx = cosine_sim.argmax()

    if cosine_sim[best_idx] > 0.15:
        return KNOWLEDGE_BASE[best_idx]['content']

    return "Информации по данному случаю в базе знаний нет. Обращение передано техническому специалисту."


# --- ЭНДПОИНТЫ ---

@app.post("/analyze_mail", response_model=AnalysisResponse)
async def analyze_mail(request: MailRequest):
    text = request.text

    # 1. Анализ тональности (BERT)
    inputs = tokenizer(text, return_tensors="pt", truncation=True, max_length=512)
    with torch.no_grad():
        outputs = model(**inputs)
    tone_id = outputs.logits.argmax(-1).item()
    tones = {0: "нейтральный", 1: "положительный", 2: "негативный"}
    current_tone = tones.get(tone_id, "нейтральный")

    # 2. Определение категории (Enum логика)
    current_category = detect_category(text)

    # 3. Извлечение сущностей
    phone, email, serials, fio, device = extract_entities(text)

    # 4. Генерация саммари и поиск ответа
    summary = create_smart_summary(text)
    answer = get_suggested_answer(text, current_tone)

    return AnalysisResponse(
        id=str(uuid.uuid4()),
        submission_date=datetime.now().isoformat(),
        fio=fio,
        object_name="Обращение по газоанализаторам",
        phone_number=phone,
        email=email,
        serial_numbers=serials,
        device_type=device,
        emotional_tone=current_tone,
        category=current_category,  # <--- ОТПРАВЛЯЕМ КАТЕГОРИЮ
        issue_summary=summary,
        suggested_answer=answer
    )


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=6767)