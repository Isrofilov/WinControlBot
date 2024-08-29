from telegram import Update
from telegram.ext import Application, CommandHandler, MessageHandler, filters
import os
import uptime
from datetime import datetime, timezone

TOKEN = 'YOUR_TELEGRAM_BOT_TOKEN_HERE'
AUTHORIZED_USERS = [User1,User2,User3]
TIME_THRESHOLD = 5 * 60

translations = {
    'en': {
        'unauthorized': 'You do not have permission to execute this command.\nYour userId is {user_id}',
        'commands': '/uptime - Get system uptime\n/sleep - Put the system to sleep\n/hibernate - Hibernate the system\n/shutdown - Shut down the system',
        'sleep': 'The computer will be put to sleep...',
        'hibernate': 'The computer will be hibernated...',
        'shutdown': 'Shutting down the computer...',
        'uptime': 'System uptime: {days} days {hours} hours {minutes} minutes {seconds} seconds',
        'retry_request': 'Your request was sent too long ago. Please repeat the request.'
    },
    'ru': {
        'unauthorized': 'У вас нет прав для выполнения этой команды.\nВаш userId {user_id}',
        'commands': '/uptime - Получить время работы системы\n/sleep - Перевести в режим сна\n/hibernate - Перевести в режим гибернации\n/shutdown - Выключить компьютер',
        'sleep': 'Компьютер будет отправлен в режим сна...',
        'hibernate': 'Компьютер будет отправлен в режим гибернации...',
        'shutdown': 'Выключаю компьютер...',
        'uptime': 'Время работы системы: {days} дней {hours} часов {minutes} минут {seconds} секунд',
        'retry_request': 'Ваш запрос был отправлен слишком давно. Пожалуйста, повторите запрос.'
    }
}

def get_translation(language_code, key, **kwargs):
    return translations.get(language_code, translations['en'])[key].format(**kwargs)

async def is_user_authorized(user_id: int) -> bool:
    return user_id in AUTHORIZED_USERS

async def handle_command(update: Update, message_key: str, action=None, **kwargs) -> None:
    user_id = update.effective_user.id
    language_code = update.effective_user.language_code
    
    message_date = update.message.date.replace(tzinfo=timezone.utc)
    current_time = datetime.now(timezone.utc)
    time_difference = (current_time - message_date).total_seconds()
    
    if time_difference > TIME_THRESHOLD:
        await send_reply(update, 'retry_request', language_code)
        return
    
    if await is_user_authorized(user_id):
        await send_reply(update, message_key, language_code, **kwargs)
        if action:
            os.system(action)
    else:
        await send_reply(update, 'unauthorized', language_code, user_id=user_id)

async def send_reply(update: Update, message_key: str, language_code: str, **kwargs) -> None:
    message = get_translation(language_code, message_key, **kwargs)
    await update.message.reply_text(message)

async def start(update: Update, context) -> None:
    await handle_command(update, 'commands')

async def sleep(update: Update, context) -> None:
    await handle_command(update, 'sleep', action="rundll32.exe powrprof.dll,SetSuspendState 0,1,0")

async def hibernate(update: Update, context) -> None:
    await handle_command(update, 'hibernate', action="shutdown /h")

async def shutdown(update: Update, context) -> None:
    await handle_command(update, 'shutdown', action="shutdown /s /t 0")

async def get_uptime(update: Update, context) -> None:
    uptime_seconds = int(uptime.uptime())
    await handle_command(update, 'uptime',
                         days=uptime_seconds // (3600 * 24),
                         hours=(uptime_seconds % (3600 * 24)) // 3600,
                         minutes=(uptime_seconds % 3600) // 60,
                         seconds=uptime_seconds % 60)

def main() -> None:
    application = Application.builder().token(TOKEN).build()
    command_handlers = {
        'start': start,
        'uptime': get_uptime,
        'sleep': sleep,
        'hibernate': hibernate,
        'shutdown': shutdown
    }

    for command, handler in command_handlers.items():
        application.add_handler(CommandHandler(command, handler))

    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, start))

    application.run_polling()

if __name__ == "__main__":
    main()