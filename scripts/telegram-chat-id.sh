#!/usr/bin/env bash
#
# Get the chat ID for a Telegram bot.
#
# Usage:
#   1. Create a bot via @BotFather on Telegram
#   2. Send a message to your bot (or add it to a group)
#   3. Run: ./scripts/telegram-chat-id.sh <BOT_TOKEN>
#
# This fetches recent messages sent to the bot and shows the chat IDs.

set -euo pipefail

BOT_TOKEN="${1:?Usage: $0 <BOT_TOKEN>}"

echo "Fetching updates for bot..."
echo ""

RESPONSE=$(curl -s "https://api.telegram.org/bot${BOT_TOKEN}/getUpdates")

# Check for errors
OK=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('ok', False))" 2>/dev/null || echo "False")

if [ "$OK" != "True" ]; then
  echo "Error: Could not fetch updates. Is the bot token correct?"
  echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
  exit 1
fi

# Extract chat info
CHATS=$(echo "$RESPONSE" | python3 -c "
import sys, json
data = json.load(sys.stdin)
seen = set()
for update in data.get('result', []):
    msg = update.get('message') or update.get('my_chat_member', {}).get('chat')
    if not msg:
        continue
    chat = msg.get('chat', msg) if 'chat' in (msg if isinstance(msg, dict) else {}) else msg
    chat_id = chat.get('id')
    if chat_id and chat_id not in seen:
        seen.add(chat_id)
        chat_type = chat.get('type', '?')
        title = chat.get('title') or chat.get('first_name', '') + ' ' + chat.get('last_name', '')
        title = title.strip()
        print(f'  Chat ID: {chat_id}')
        print(f'  Type:    {chat_type}')
        print(f'  Name:    {title}')
        print()
if not seen:
    print('No chats found. Make sure you have:')
    print('  1. Sent a message to the bot, OR')
    print('  2. Added the bot to a group and sent a message there')
    print()
    print('Then run this script again.')
" 2>/dev/null)

if [ -z "$CHATS" ]; then
  echo "No chats found. Make sure you have:"
  echo "  1. Sent a message to the bot, OR"
  echo "  2. Added the bot to a group and sent a message there"
  echo ""
  echo "Then run this script again."
else
  echo "Found chat(s):"
  echo ""
  echo "$CHATS"
  echo "Use the Chat ID value when creating a Telegram channel in Aptabase."
fi
