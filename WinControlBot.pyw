from telegram import Update, InlineKeyboardButton, InlineKeyboardMarkup, InputFile
from telegram.ext import Application, CommandHandler, MessageHandler, filters, CallbackQueryHandler
import os
import uptime
import psutil
import re
import asyncio
from datetime import datetime, timezone
import pyautogui
import io

TOKEN = 'YOUR_TELEGRAM_BOT_TOKEN_HERE'
AUTHORIZED_USERS = [User1, User2, User3]
TIME_THRESHOLD = 5 * 60
PENDING_ACTIONS = {}  # Storage of waiting actions for canceling

translations = {
    'en': {
        'unauthorized': 'You do not have permission to execute this command.\nYour userId is {user_id}',
        'commands': '/uptime - Get system uptime\n/sleep - Put the system to sleep\n/hibernate - Hibernate the system\n/shutdown - Shut down the system\n/restart - Restart the system\n/sysinfo - Get system information\n/shutdown_in [time] - Shutdown after specified time\n/screenshot - Get screenshot',
        'sleep': 'The computer will be put to sleep...',
        'hibernate': 'The computer will be hibernated...',
        'shutdown': 'Shutting down the computer...',
        'restart': 'Restarting the computer...',
        'shutdown_in': 'The computer will shut down in {time}...',
        'uptime': 'System uptime: {days} days {hours} hours {minutes} minutes {seconds} seconds',
        'retry_request': 'Your request was sent too long ago. Please repeat the request.',
        'sysinfo': '💻 System Information:\n\nCPU: {cpu_percent}% used\nMemory: {memory_used}/{memory_total} GB ({memory_percent}%)\nDisk: {disk_used}/{disk_total} GB ({disk_percent}%)',
        'action_pending': 'Action scheduled. Press "Cancel" to abort.',
        'action_canceled': 'Action canceled.',
        'action_executed': 'Action executed.',
        'invalid_time_format': 'Invalid time format. Use format like "1h30m" or "45m" or "10s".',
        'screenshot': 'Here is your screenshot.'
    },
    'ru': {
        'unauthorized': 'У вас нет прав для выполнения этой команды.\nВаш userId {user_id}',
        'commands': '/uptime - Получить время работы системы\n/sleep - Перевести в режим сна\n/hibernate - Перевести в режим гибернации\n/shutdown - Выключить компьютер\n/restart - Перезагрузить компьютер\n/sysinfo - Получить информацию о системе\n/shutdown_in [время] - Выключение через указанное время\n/screenshot - Получить скриншот',
        'sleep': 'Компьютер будет отправлен в режим сна...',
        'hibernate': 'Компьютер будет отправлен в режим гибернации...',
        'shutdown': 'Выключаю компьютер...',
        'restart': 'Перезагружаю компьютер...',
        'shutdown_in': 'Компьютер будет выключен через {time}...',
        'uptime': 'Время работы системы: {days} дней {hours} часов {minutes} минут {seconds} секунд',
        'retry_request': 'Ваш запрос был отправлен слишком давно. Пожалуйста, повторите запрос.',
        'sysinfo': '💻 Информация о системе:\n\nПроцессор: {cpu_percent}% использовано\nПамять: {memory_used}/{memory_total} ГБ ({memory_percent}%)\nДиск: {disk_used}/{disk_total} ГБ ({disk_percent}%)',
        'action_pending': 'Действие запланировано. Нажмите "Отмена" для отмены.',
        'action_canceled': 'Действие отменено.',
        'action_executed': 'Действие выполнено.',
        'invalid_time_format': 'Неверный формат времени. Используйте формат вида "1h30m" или "45m" или "10s".',
        'screenshot': 'Вот ваш скриншот экрана.'
    }
}

def get_translation(language_code, key, **kwargs):
    return translations.get(language_code, translations['en'])[key].format(**kwargs)

async def is_user_authorized(user_id: int) -> bool:
    return user_id in AUTHORIZED_USERS

def get_system_info():
    cpu_percent = psutil.cpu_percent(interval=1)
    memory = psutil.virtual_memory()
    memory_total = round(memory.total / (1024**3), 2)  # GB
    memory_used = round(memory.used / (1024**3), 2)  # GB
    memory_percent = memory.percent
    
    disk = psutil.disk_usage('/')
    disk_total = round(disk.total / (1024**3), 2)  # GB
    disk_used = round(disk.used / (1024**3), 2)  # GB
    disk_percent = disk.percent
    
    return {
        'cpu_percent': cpu_percent,
        'memory_total': memory_total,
        'memory_used': memory_used,
        'memory_percent': memory_percent,
        'disk_total': disk_total,
        'disk_used': disk_used,
        'disk_percent': disk_percent
    }

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

async def send_reply_with_cancel(update: Update, message_key: str, language_code: str, action_id: str, **kwargs) -> None:
    message = get_translation(language_code, message_key, **kwargs)
    keyboard = [[InlineKeyboardButton("Cancel", callback_data=f"cancel_{action_id}")]]
    if language_code == "ru":
        keyboard = [[InlineKeyboardButton("Отмена", callback_data=f"cancel_{action_id}")]]
    
    reply_markup = InlineKeyboardMarkup(keyboard)
    await update.message.reply_text(message, reply_markup=reply_markup)

async def start(update: Update, context) -> None:
    await handle_command(update, 'commands')

async def sleep(update: Update, context) -> None:
    await handle_command(update, 'sleep', action="rundll32.exe powrprof.dll,SetSuspendState 0,1,0")

async def hibernate(update: Update, context) -> None:
    await handle_command(update, 'hibernate', action="shutdown /h")

async def shutdown(update: Update, context) -> None:
    user_id = update.effective_user.id
    language_code = update.effective_user.language_code
    
    if not await is_user_authorized(user_id):
        await send_reply(update, 'unauthorized', language_code, user_id=user_id)
        return
    
    action_id = f"shutdown_{user_id}_{datetime.now().timestamp()}"
    PENDING_ACTIONS[action_id] = {
        'action': "shutdown /s /t 0",
        'user_id': user_id,
        'language_code': language_code,
        'message_id': update.message.message_id
    }
    
    await send_reply_with_cancel(update, 'action_pending', language_code, action_id)
    
    # Plan the performance of the action in 30 seconds
    asyncio.create_task(execute_delayed_action(action_id, 30))

async def restart(update: Update, context) -> None:
    user_id = update.effective_user.id
    language_code = update.effective_user.language_code
    
    if not await is_user_authorized(user_id):
        await send_reply(update, 'unauthorized', language_code, user_id=user_id)
        return
    
    action_id = f"restart_{user_id}_{datetime.now().timestamp()}"
    PENDING_ACTIONS[action_id] = {
        'action': "shutdown /r /t 0",
        'user_id': user_id,
        'language_code': language_code,
        'message_id': update.message.message_id
    }
    
    await send_reply_with_cancel(update, 'action_pending', language_code, action_id)
    
    # Plan the performance of the action in 30 seconds
    asyncio.create_task(execute_delayed_action(action_id, 30))

async def get_uptime(update: Update, context) -> None:
    uptime_seconds = int(uptime.uptime())
    await handle_command(update, 'uptime',
                         days=uptime_seconds // (3600 * 24),
                         hours=(uptime_seconds % (3600 * 24)) // 3600,
                         minutes=(uptime_seconds % 3600) // 60,
                         seconds=uptime_seconds % 60)

async def get_sysinfo(update: Update, context) -> None:
    user_id = update.effective_user.id
    language_code = update.effective_user.language_code
    
    if not await is_user_authorized(user_id):
        await send_reply(update, 'unauthorized', language_code, user_id=user_id)
        return
    
    info = get_system_info()
    await send_reply(update, 'sysinfo', language_code, **info)

async def shutdown_in(update: Update, context) -> None:
    user_id = update.effective_user.id
    language_code = update.effective_user.language_code
    
    if not await is_user_authorized(user_id):
        await send_reply(update, 'unauthorized', language_code, user_id=user_id)
        return
    
    if not context.args:
        await send_reply(update, 'invalid_time_format', language_code)
        return
    
    time_str = context.args[0]
    seconds = parse_time_string(time_str)
    
    if seconds is None:
        await send_reply(update, 'invalid_time_format', language_code)
        return
    
    # We format the time to display
    time_display = format_seconds_to_display(seconds, language_code)
    
    action_id = f"shutdown_in_{user_id}_{datetime.now().timestamp()}"
    PENDING_ACTIONS[action_id] = {
        'action': f"shutdown /s /t {seconds}",
        'user_id': user_id,
        'language_code': language_code,
        'message_id': update.message.message_id
    }
    
    await send_reply_with_cancel(update, 'shutdown_in', language_code, action_id, time=time_display)
    
    # Plan the performance of the action after the specified time
    asyncio.create_task(execute_delayed_action(action_id, seconds))

def parse_time_string(time_str):
    # Parish the line of time "1H30M" or "45M" or "10s" in seconds.
    pattern = r'(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?'
    match = re.match(pattern, time_str)
    
    if not match or not any(match.groups()):
        return None
    
    hours = int(match.group(1) or 0)
    minutes = int(match.group(2) or 0)
    seconds = int(match.group(3) or 0)
    
    return hours * 3600 + minutes * 60 + seconds

def format_seconds_to_display(seconds, language_code):
    # Format seconds in a format convenient for the user.
    hours, remainder = divmod(seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    
    parts = []
    if hours > 0:
        parts.append(f"{hours}{'ч' if language_code == 'ru' else 'h'}")
    if minutes > 0:
        parts.append(f"{minutes}{'м' if language_code == 'ru' else 'm'}")
    if seconds > 0 or not parts:
        parts.append(f"{seconds}{'с' if language_code == 'ru' else 's'}")
    
    return " ".join(parts)

async def execute_delayed_action(action_id, delay_seconds):
    # Perform an action with a delay if it is not canceled.
    await asyncio.sleep(delay_seconds)
    if action_id in PENDING_ACTIONS:
        action_data = PENDING_ACTIONS.pop(action_id)
        os.system(action_data['action'])

async def handle_callback(update: Update, context):
    # Tanking from the Inline cover.
    query = update.callback_query
    await query.answer()
    
    if query.data.startswith("cancel_"):
        action_id = query.data[7:]  # We delete "Cancel_" from data
        if action_id in PENDING_ACTIONS:
            action_data = PENDING_ACTIONS.pop(action_id)
            language_code = action_data['language_code']
            await query.edit_message_text(get_translation(language_code, 'action_canceled'))
        else:
            # If the action is already performed or canceled
            await query.edit_message_text(get_translation('en', 'action_executed'))

async def get_screenshot(update: Update, context) -> None:
    user_id = update.effective_user.id
    language_code = update.effective_user.language_code
    
    if not await is_user_authorized(user_id):
        await send_reply(update, 'unauthorized', language_code, user_id=user_id)
        return
    
    # We make a screenshot
    screenshot = pyautogui.screenshot()
    
    # We will convert to bytes for sending
    img_byte_arr = io.BytesIO()
    screenshot.save(img_byte_arr, format='PNG')
    img_byte_arr.seek(0)
    
    # We send a photo
    await update.message.reply_photo(
        photo=InputFile(img_byte_arr, filename='screenshot.png'),
        caption=get_translation(language_code, 'screenshot')
    )

def main() -> None:
    application = Application.builder().token(TOKEN).build()
    command_handlers = {
        'start': start,
        'uptime': get_uptime,
        'sleep': sleep,
        'hibernate': hibernate,
        'shutdown': shutdown,
        'restart': restart,
        'sysinfo': get_sysinfo,
        'shutdown_in': shutdown_in,
        'screenshot': get_screenshot
    }

    for command, handler in command_handlers.items():
        application.add_handler(CommandHandler(command, handler))

    application.add_handler(CallbackQueryHandler(handle_callback))
    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, start))

    application.run_polling()

if __name__ == "__main__":
    main()