# -*- coding: utf-8 -*-
"""
API ML-сервиса: на вход — письмо (EmailMessageModel), на выход — ComplaintModel.
Порт 6767. Бекенд передаёт сюда письмо и получает заполненные поля жалобы.
"""

from datetime import datetime
from typing import Optional
from uuid import UUID

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from model import ComplaintPredictor

app = FastAPI(title="AgentSupport ML", description="Парсинг писем и классификация обращений ЭРИС")
predictor = ComplaintPredictor()


# ----- Вход: формат с бекенда (C# EmailMessageModel) -----
class EmailMessageModel(BaseModel):
    id: Optional[str] = None
    complaint_id: Optional[str] = Field(None, alias="complaintId")
    direction: Optional[str] = None
    external_message_id: Optional[str] = Field(None, alias="externalMessageId")
    from_email: Optional[str] = Field(None, alias="fromEmail")
    from_name: Optional[str] = Field(None, alias="fromName")
    to_email: Optional[str] = Field(None, alias="toEmail")
    subject: Optional[str] = None
    content: Optional[str] = None
    thread_id: Optional[str] = Field(None, alias="threadId")
    sent_at_utc: Optional[datetime] = Field(None, alias="sentAtUtc")
    created_at_utc: Optional[datetime] = Field(None, alias="createdAtUtc")

    class Config:
        populate_by_name = True


# ----- Выход: ComplaintModel (поля для бекенда) -----
class ComplaintModel(BaseModel):
    id: Optional[str] = None
    submission_date: Optional[datetime] = Field(None, alias="submissionDate")
    fio: Optional[str] = None  # null, если не удалось извлечь
    object_name: Optional[str] = Field(None, alias="objectName")  # название предприятия/объекта
    phone_number: Optional[str] = Field(None, alias="phoneNumber")
    email: Optional[str] = None
    serial_numbers: list[str] = Field(default_factory=list, alias="serialNumbers")
    device_type: Optional[str] = Field(None, alias="deviceType")
    emotional_tone: Optional[str] = Field(None, alias="emotionalTone")
    issue_summary: str = Field("", alias="issueSummary")
    status: str = "Waiting"
    category: Optional[str] = None
    suggested_answer: Optional[str] = Field(None, alias="suggestedAnswer")  # предполагаемый ответ на вопрос

    class Config:
        populate_by_name = True


@app.post("/process", response_model=ComplaintModel)
def process_email(email: EmailMessageModel) -> ComplaintModel:
    """
    Принимает письмо (EmailMessageModel), возвращает ComplaintModel.
    Email и SubmissionDate берутся из письма, остальное заполняет модель.
    """
    subject = email.subject or ""
    content = email.content or ""
    from_name = email.from_name or ""
    from_email = email.from_email

    try:
        out = predictor.predict(
            subject=subject,
            content=content,
            from_email=from_email,
            from_name=from_name,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

    submission_date = email.sent_at_utc or email.created_at_utc or datetime.utcnow()
    return ComplaintModel(
        id=email.id,
        submission_date=submission_date,
        fio=out["fio"],
        object_name=out.get("objectName"),
        phone_number=out["phoneNumber"],
        email=from_email,
        serial_numbers=out["serialNumbers"],
        device_type=out["deviceType"],
        emotional_tone=out["emotionalTone"],
        issue_summary=out["issueSummary"],
        status=out["status"],
        category=out.get("category"),
        suggested_answer=out.get("suggestedAnswer"),
    )


# Совместимость с RPC: бекенд может слать MessageId, FromName, FromEmail, Subject, SentAtUtc, Content
class SimpleEmailRequest(BaseModel):
    message_id: Optional[str] = Field(None, alias="messageId")
    from_name: Optional[str] = Field(None, alias="fromName")
    from_email: Optional[str] = Field(None, alias="fromEmail")
    subject: Optional[str] = ""
    sent_at_utc: Optional[datetime] = Field(None, alias="sentAtUtc")
    content: Optional[str] = ""

    class Config:
        populate_by_name = True


@app.post("/process/simple", response_model=ComplaintModel)
def process_simple_email(req: SimpleEmailRequest) -> ComplaintModel:
    """Упрощённый контракт (Kafka RPC): messageId, fromName, fromEmail, subject, sentAtUtc, content."""
    try:
        out = predictor.predict(
            subject=req.subject or "",
            content=req.content or "",
            from_email=req.from_email,
            from_name=req.from_name or "",
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

    submission = req.sent_at_utc or datetime.utcnow()
    return ComplaintModel(
        submission_date=submission,
        fio=out["fio"],
        object_name=out.get("objectName"),
        phone_number=out["phoneNumber"],
        email=req.from_email,
        serial_numbers=out["serialNumbers"],
        device_type=out["deviceType"],
        emotional_tone=out["emotionalTone"],
        issue_summary=out["issueSummary"],
        status=out["status"],
        category=out.get("category"),
        suggested_answer=out.get("suggestedAnswer"),
    )


@app.get("/health")
def health():
    return {"status": "ok"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=6767)
