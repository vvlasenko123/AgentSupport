from transformers import RobertaTokenizer, RobertaForSequenceClassification
import os


def load_model():
    # Указываем локальный путь к модели
    model_path = "./app/ml/rubert"  # Путь к папке с моделью

    current_dir = os.getcwd()
    print(current_dir)
    # Проверим, существует ли папка с моделью
    if not os.path.exists(model_path):
        raise FileNotFoundError(f"Модель не найдена по пути: {model_path}")

    # Загружаем токенизатор и модель из локального пути, добавляем local_files_only=True
    print(f"Загружаем модель и токенизатор из локального пути: {model_path}")
    tokenizer = RobertaTokenizer.from_pretrained(model_path, local_files_only=True)
    model = RobertaForSequenceClassification.from_pretrained(model_path, local_files_only=True)

    return model, tokenizer