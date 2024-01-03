from telegram import Update
from telegram.ext import Application, CommandHandler, MessageHandler, filters
import os
import uptime

TOKEN = 'YOUR_TELEGRAM_BOT_TOKEN_HERE'
AUTHORIZED_USERS = [User1,User2,User3]

translations = {
    'en': {
        'unauthorized': 'You do not have permission to execute this command.\nYour userId is {user_id}',
        'commands': (
            '/uptime - Get system uptime\n'
            '/sleep - Put the system to sleep\n'
            '/hibernate - Hibernate the system\n'
            '/shutdown - Shut down the system'
        ),
        'sleep': 'The computer will be put to sleep...',
        'hibernate': 'The computer will be hibernated...',
        'shutdown': 'Shutting down the computer...',
        'uptime': 'System uptime: {days} days {hours} hours {minutes} minutes {seconds} seconds'
    },
    'ru': {
        'unauthorized': 'У вас нет прав для выполнения этой команды.\nВаш userId {user_id}',
        'commands': (
            '/uptime - Получить время работы системы\n'
            '/sleep - Перевести в режим сна\n'
            '/hibernate - Перевести в режим гибернации\n'
            '/shutdown - Выключить компьютер'
        ),
        'sleep': 'Компьютер будет отправлен в режим сна...',
        'hibernate': 'Компьютер будет отправлен в режим гибернации...',
        'shutdown': 'Выключаю компьютер...',
        'uptime': 'Время работы системы: {days} дней {hours} часов {minutes} минут {seconds} секунд'
    }
}

def get_translation(language_code, key, **kwargs):
    if language_code not in translations:
        language_code = 'en'
    translations_dict = translations[language_code]
    return translations_dict[key].format(**kwargs)

    
async def is_user_authorized(user_id: int) -> bool:
    return user_id in AUTHORIZED_USERS

async def send_unauthorized_message(update: Update, language_code: str) -> None:
    user_id = update.effective_user.id
    message = get_translation(language_code, 'unauthorized', user_id=user_id)
    await update.message.reply_text(message)

async def send_reply(update: Update, message_key: str, language_code: str, **kwargs) -> None:
    message = get_translation(language_code, message_key, **kwargs)
    await update.message.reply_text(message)

async def start(update: Update, context) -> None:
    language_code = update.effective_user.language_code
    if await is_user_authorized(update.effective_user.id):
        await send_reply(update, 'commands', language_code)
    else:
        await send_unauthorized_message(update, language_code)
        
async def sleep(update: Update, context) -> None:
    language_code = update.effective_user.language_code
    if await is_user_authorized(update.effective_user.id):
        await send_reply(update, 'sleep', language_code)
        os.system("rundll32.exe powrprof.dll,SetSuspendState 0,1,0")
    else:
        await send_unauthorized_message(update, language_code)

async def hibernate(update: Update, context) -> None:
    language_code = update.effective_user.language_code
    if await is_user_authorized(update.effective_user.id):
        await send_reply(update, 'hibernate', language_code)
        os.system("shutdown /h")
    else:
        await send_unauthorized_message(update, language_code)

async def shutdown(update: Update, context) -> None:
    language_code = update.effective_user.language_code
    if await is_user_authorized(update.effective_user.id):
        await send_reply(update, 'shutdown', language_code)
        os.system("shutdown /s /t 0")
    else:
        await send_unauthorized_message(update, language_code)

async def get_uptime(update: Update, context) -> None:
    language_code = update.effective_user.language_code
    if await is_user_authorized(update.effective_user.id):
        uptime_seconds = int(uptime.uptime())
        uptime_days = int(uptime_seconds // (3600 * 24))
        uptime_hours = int((uptime_seconds % (3600 * 24)) // 3600)
        uptime_minutes = int((uptime_seconds % 3600) // 60)
        remaining_seconds = int(uptime_seconds % 60)
        await send_reply(update, 'uptime', language_code,
                         days=uptime_days,
                         hours=uptime_hours,
                         minutes=uptime_minutes,
                         seconds=remaining_seconds)
    else:
        await send_unauthorized_message(update, language_code)
        

def main() -> None:
    application = Application.builder().token(TOKEN).build()
    application.add_handler(CommandHandler('start', start))
    application.add_handler(CommandHandler('uptime', get_uptime))
    application.add_handler(CommandHandler('sleep', sleep))
    application.add_handler(CommandHandler('hibernate', hibernate))
    application.add_handler(CommandHandler('shutdown', shutdown))
    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, start))

    application.run_polling()

if __name__ == "__main__":
    main()
