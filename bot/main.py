import asyncio
from aiogram import Bot, Dispatcher, types
from aiogram.filters import Command
from aiogram.types import Message
from aiogram.enums import ParseMode
from aiokafka import AIOKafkaConsumer
import uuid
from datetime import datetime
from dataclasses import dataclass
from typing import List, Optional, Set
import json
import logging
import asyncpg
import os

# Настройка логирования
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(message)s',
    datefmt='%H:%M:%S'
)
logger = logging.getLogger(__name__)

# Отключаем логи от библиотек
logging.getLogger('aiokafka').setLevel(logging.WARNING)
logging.getLogger('aiogram').setLevel(logging.WARNING)

# Конфигурация
TELEGRAM_TOKEN = "8657445324:AAHYZLDx7jz-rQAHVjxuZalnikQz2qrm6t0"
KAFKA_BOOTSTRAP_SERVERS = os.getenv('KAFKA_BOOTSTRAP_SERVERS', 'localhost:9092')
KAFKA_TOPIC = 'EmailReceivedRpcRequest'  # Топик Kafka, к которому подключаемся

# Конфигурация PostgreSQL
DB_CONFIG = {
    'user': os.getenv('DB_USER', 'postgres'),
    'password': os.getenv('DB_PASSWORD', 'postgres'),
    'database': os.getenv('DB_NAME', 'agent-api'),
    'host': os.getenv('DB_HOST', 'localhost'),
    'port': int(os.getenv('DB_PORT', '5432'))
}

@dataclass
class Notification:
    id: uuid.UUID
    submission_date: datetime
    fio: str
    object_name: str
    phone_number: Optional[str]
    email: Optional[str]
    serial_numbers: List[str]
    device_type: Optional[str]
    emotional_tone: Optional[str]
    issue_summary: str
    status: str

class Database:
    def __init__(self):
        self.pool = None

    async def connect(self):
        """Подключение к БД"""
        try:
            self.pool = await asyncpg.create_pool(**DB_CONFIG)
            logger.info("Подключено к PostgreSQL")

            # Проверяем существование таблицы
            async with self.pool.acquire() as conn:
                await conn.execute('''
                                   CREATE TABLE IF NOT EXISTS subscribers (
                                                                              chat_id BIGINT PRIMARY KEY,
                                                                              username TEXT,
                                                                              first_name TEXT,
                                                                              last_seen TIMESTAMP
                                   )
                                   ''')
                logger.info("Таблица subscribers проверена")
        except Exception as e:
            logger.error(f"Ошибка подключения к БД: {e}")
            raise

    async def close(self):
        """Закрытие подключения к БД"""
        if self.pool:
            await self.pool.close()
            logger.info("Соединение с БД закрыто")

    async def add_subscriber(self, chat_id: int, username: str = None, first_name: str = None):
        """Добавление или обновление подписчика"""
        try:
            async with self.pool.acquire() as conn:
                await conn.execute('''
                                   INSERT INTO subscribers (chat_id, username, first_name, last_seen)
                                   VALUES ($1, $2, $3, CURRENT_TIMESTAMP)
                                       ON CONFLICT (chat_id)
                    DO UPDATE SET
                                       username = EXCLUDED.username,
                                                                  first_name = EXCLUDED.first_name,
                                                                  last_seen = CURRENT_TIMESTAMP
                                   ''', chat_id, username, first_name)
            logger.info(f"Подписчик {chat_id} сохранен в БД")
            return True
        except Exception as e:
            logger.error(f"Ошибка сохранения подписчика {chat_id}: {e}")
            return False

    async def remove_subscriber(self, chat_id: int):
        """Удаление подписчика"""
        try:
            async with self.pool.acquire() as conn:
                result = await conn.execute('DELETE FROM subscribers WHERE chat_id = $1', chat_id)
                if result.endswith('1'):
                    logger.info(f"Подписчик {chat_id} удален из БД")
                    return True
                else:
                    logger.info(f"Подписчик {chat_id} не найден в БД")
                    return False
        except Exception as e:
            logger.error(f"Ошибка удаления подписчика {chat_id}: {e}")
            return False

    async def get_all_subscribers(self) -> Set[int]:
        """Получение всех подписчиков"""
        try:
            async with self.pool.acquire() as conn:
                rows = await conn.fetch('SELECT chat_id FROM subscribers')
                subscribers = {row['chat_id'] for row in rows}
            logger.info(f"Загружено {len(subscribers)} подписчиков из БД")
            return subscribers
        except Exception as e:
            logger.error(f"Ошибка загрузки подписчиков: {e}")
            return set()

    async def get_subscriber_count(self) -> int:
        """Получение количества подписчиков"""
        try:
            async with self.pool.acquire() as conn:
                count = await conn.fetchval('SELECT COUNT(*) FROM subscribers')
            return count
        except Exception as e:
            logger.error(f"Ошибка получения количества подписчиков: {e}")
            return 0

class SimpleBot:
    def __init__(self):
        self.bot = Bot(token=TELEGRAM_TOKEN)
        self.dp = Dispatcher()
        self.db = Database()
        self.consumer = AIOKafkaConsumer(
            KAFKA_TOPIC,  # Подключаемся к топику
            bootstrap_servers=KAFKA_BOOTSTRAP_SERVERS,
            value_deserializer=lambda m: json.loads(m.decode('utf-8')),
            auto_offset_reset='earliest',
            group_id="telegram_notification_bot"
        )
        # Регистрируем обработчики команд
        self.dp.message.register(self.cmd_start, Command("start"))
        self.dp.message.register(self.cmd_reg, Command("reg"))
        self.dp.message.register(self.cmd_rem, Command("rem"))
        self.dp.message.register(self.cmd_count, Command("count"))
        self.dp.message.register(self.cmd_help, Command("help"))

    async def cmd_start(self, message: Message):
        """Обработчик команды /start"""
        await message.answer(
            "👋 <b>Привет! Я бот для уведомлений из Kafka</b>\n\n"
            "<b>Доступные команды:</b>\n"
            "🔹 /reg - подписаться на уведомления\n"
            "🔹 /rem - отписаться от уведомлений\n"
            "🔹 /count - количество подписчиков\n"
            "🔹 /help - показать это сообщение",
            parse_mode=ParseMode.HTML
        )

    async def cmd_help(self, message: Message):
        """Обработчик команды /help"""
        await self.cmd_start(message)

    async def cmd_reg(self, message: Message):
        """Обработчик команды /reg - подписка"""
        chat_id = message.chat.id
        username = message.chat.username
        first_name = message.chat.first_name

        result = await self.db.add_subscriber(chat_id, username, first_name)

        if result:
            await message.answer(
                "✅ <b>Вы успешно подписались на уведомления!</b>\n",
                parse_mode=ParseMode.HTML
            )
        else:
            await message.answer(
                "❌ <b>Ошибка при подписке</b>\n"
                "Попробуйте позже или обратитесь к администратору.",
                parse_mode=ParseMode.HTML
            )

        # Показываем количество подписчиков
        count = await self.db.get_subscriber_count()
        await message.answer(
            f"📊 <b>Всего подписчиков:</b> {count}",
            parse_mode=ParseMode.HTML
        )

    async def cmd_rem(self, message: Message):
        """Обработчик команды /rem - отписка"""
        chat_id = message.chat.id

        result = await self.db.remove_subscriber(chat_id)

        if result:
            await message.answer(
                "✅ <b>Вы успешно отписались от уведомлений!</b>\n"
                "Вы больше не будете получать оповещения.",
                parse_mode=ParseMode.HTML
            )
        else:
            await message.answer(
                "❌ <b>Вы не были подписаны на уведомления</b>\n"
                "Используйте /reg для подписки.",
                parse_mode=ParseMode.HTML
            )

        # Показываем количество подписчиков
        count = await self.db.get_subscriber_count()
        await message.answer(
            f"📊 <b>Всего подписчиков:</b> {count}",
            parse_mode=ParseMode.HTML
        )

    async def cmd_count(self, message: Message):
        """Обработчик команды /count - количество подписчиков"""
        count = await self.db.get_subscriber_count()
        await message.answer(
            f"📊 <b>Всего подписчиков:</b> {count}",
            parse_mode=ParseMode.HTML
        )

    async def notify_all_subscribers(self, message: str):
        """Отправляет уведомление всем подписчикам из БД"""
        subscribers = await self.db.get_all_subscribers()

        if not subscribers:
            logger.warning("Нет подписчиков в БД для отправки")
            return

        logger.info(f"Отправка уведомления {len(subscribers)} подписчикам из БД...")

        success = 0
        failed = []

        for chat_id in subscribers:
            try:
                await self.bot.send_message(
                    chat_id=chat_id,
                    text=message,
                    parse_mode=ParseMode.HTML
                )
                logger.info(f"Отправлено подписчику: {chat_id}")
                success += 1
                await asyncio.sleep(0.05)
            except Exception as e:
                logger.error(f"Ошибка отправки подписчику {chat_id}: {e}")
                failed.append((chat_id, str(e)))

        # Удаляем подписчиков, которым не удалось отправить (бот заблокирован или чат удален)
        for chat_id, error in failed:
            if "chat not found" in error.lower() or "bot was blocked" in error.lower():
                await self.db.remove_subscriber(chat_id)
                logger.info(f"Подписчик {chat_id} удален (недоступен)")

        logger.info(f"Результат отправки: успешно {success}/{len(subscribers)}")

    async def start(self):
        # Подключаемся к БД
        await self.db.connect()

        # Загружаем начальных подписчиков из истории
        logger.info("Загрузка начальных подписчиков из Telegram...")
        await self.load_initial_subscribers()

        # Запускаем поллинг для обработки входящих сообщений
        asyncio.create_task(self.dp.start_polling(self.bot))
        logger.info("Бот готов принимать сообщения")

        # Запускаем Kafka
        await self.consumer.start()
        logger.info("Ожидание сообщений из Kafka...")

        try:
            async for msg in self.consumer:
                logger.info(f"Получено сообщение из Kafka (offset: {msg.offset})")

                # Просто отправляем уведомление, что новый запрос в поддержку
                message = "📩 <b>Пришел новый запрос в поддержку, проверьте систему!</b>"

                # Отправляем всем подписчикам из БД
                await self.notify_all_subscribers(message)

        except Exception as e:
            logger.error(f"Ошибка: {e}")
        finally:
            await self.consumer.stop()
            await self.db.close()

async def main():
    bot = SimpleBot()
    await bot.start()

if __name__ == "__main__":
    logger.info("Запуск бота с PostgreSQL...")
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logger.info("Бот остановлен")