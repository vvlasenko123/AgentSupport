# -*- coding: utf-8 -*-
"""Загрузка и подготовка данных для обучения."""

import json
import os
from pathlib import Path

from config import APPEAL_CATEGORIES, TRAIN_DATA_JSON, DATA_DIR, DEVICES_LIST_PATH


def load_train_data():
    """Загружает train_data.json и опционально файлы из data/."""
    records = []
    if os.path.isfile(TRAIN_DATA_JSON):
        with open(TRAIN_DATA_JSON, "r", encoding="utf-8") as f:
            records = json.load(f)
    if os.path.isdir(DATA_DIR):
        for p in Path(DATA_DIR).glob("*.json"):
            if p.name == "train_data.json":
                continue
            try:
                with open(p, "r", encoding="utf-8") as f:
                    data = json.load(f)
                if isinstance(data, list):
                    records.extend(data)
                else:
                    records.append(data)
            except Exception:
                pass
    return records


def load_devices_list():
    """Загружает список устройств из data/devices.txt."""
    devices = []
    if os.path.isfile(DEVICES_LIST_PATH):
        with open(DEVICES_LIST_PATH, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#"):
                    devices.append(line)
    # Дублируем без "ЭРИС " для матчинга
    return sorted(set(devices), key=len, reverse=True)  # длинные первыми для матча


def prepare_records(records):
    """Приводит записи к единому формату: text, label, category, issue_summary, answer."""
    out = []
    cat_set = set(APPEAL_CATEGORIES)
    for r in records:
        text = (r.get("text") or "").strip()
        if not text:
            continue
        label = r.get("label", 0)
        if label not in (0, 1, 2):
            label = 0
        category = (r.get("category") or "").strip()
        if category not in cat_set:
            continue
        issue_summary = (r.get("issue_summary") or "").strip() or text[:200]
        answer = (r.get("answer") or "").strip()
        out.append({
            "text": text,
            "label": label,
            "category": category,
            "issue_summary": issue_summary,
            "answer": answer,
        })
    return out
