import asyncio
from aiokafka import AIOKafkaProducer
import json
import uuid
from datetime import datetime
import logging

# Настройка логирования в консоль
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
logger = logging.getLogger(__name__)

async def send_test_message():
    producer = AIOKafkaProducer(
        bootstrap_servers='localhost:9092',
        value_serializer=lambda v: json.dumps(v).encode('utf-8')
    )

    logger.info("🔄 Запуск Kafka продюсера...")
    await producer.start()
    logger.info("✅ Kafka продюсер запущен")

    try:
        # Тестовое сообщение
        test_message = {
            "id": str(uuid.uuid4()),
            "submission_date": datetime.now().isoformat(),
            "fio": "Иванов Иван Иванович",
            "object_name": "Тестовый объект №1",
            "phone_number": "+79991234567",
            "email": "ivanov@test.com",
            "serial_numbers": ["SN123456", "SN789012"],
            "device_type": "Смартфон iPhone 13",
            "emotional_tone": "Нейтральный",
            "issue_summary": "Не работает кнопка включения",
            "status": "new"
        }

        logger.info("📦 Подготовлено сообщение для отправки:")
        logger.info(f"  ID: {test_message['id']}")
        logger.info(f"  ФИО: {test_message['fio']}")
        logger.info(f"  Объект: {test_message['object_name']}")
        logger.info(f"  Телефон: {test_message['phone_number']}")
        logger.info(f"  Email: {test_message['email']}")
        logger.info(f"  Серийные номера: {', '.join(test_message['serial_numbers'])}")
        logger.info(f"  Тип устройства: {test_message['device_type']}")
        logger.info(f"  Тон: {test_message['emotional_tone']}")
        logger.info(f"  Проблема: {test_message['issue_summary']}")
        logger.info(f"  Статус: {test_message['status']}")

        # Отправляем в топик
        logger.info(f"📤 Отправка сообщения в топик 'notifications'...")
        await producer.send_and_wait("notifications", test_message)

        logger.info("✅ Тестовое сообщение успешно отправлено в Kafka!")
        logger.info(f"📍 Топик: notifications")
        logger.info(f"🆔 ID сообщения: {test_message['id']}")

        # Можно также вывести полное сообщение в красивом формате
        logger.info("📋 Полное содержимое отправленного сообщения:")
        logger.info(json.dumps(test_message, indent=2, ensure_ascii=False))

    except Exception as e:
        logger.error(f"❌ Ошибка при отправке сообщения: {e}")
        raise
    finally:
        logger.info("🛑 Остановка Kafka продюсера...")
        await producer.stop()
        logger.info("✅ Kafka продюсер остановлен")

if __name__ == "__main__":
    logger.info("🚀 Запуск скрипта отправки тестового сообщения в Kafka")
    asyncio.run(send_test_message())
    logger.info("✨ Скрипт завершен")