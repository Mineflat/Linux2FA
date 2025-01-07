using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Linux2FA
{
    internal class Program
    {
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Рассмотрите возможность добавления модификатора "required" или объявления значения, допускающего значение NULL.
        private static TelegramBotClient botClient;
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Рассмотрите возможность добавления модификатора "required" или объявления значения, допускающего значение NULL.
        
        private const long ChatId = 0; // Укажите ID пользователя Telegram
        private const string botToken = ""; // Замените на токен вашего бота
        private static CancellationTokenSource confirmationTimeoutCts = new CancellationTokenSource(); // Для таймера
        private static bool UpdateSent = false;
        [Obsolete]
        private static void Main(string[] args)
        {
            Init();
            Console.ReadLine();
        }

        [Obsolete]
        public static async void Init()
        {
            botClient = new TelegramBotClient(botToken);
            using var cts = new CancellationTokenSource();
            // Создаем объект ReceiverOptions
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(), // Получаем все типы обновлений
                DropPendingUpdates = true // Работаем только с новыми сообщениями, старые сообщения будут проигнорированы
            };
            // Запускаем получение обновлений
            botClient.StartReceiving(
                HandleUpdateAsync,
                HandlePollingErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token);
            // Отправка кнопок в Telegram
            await SendAuthButtonsAsync(ChatId);
            // Запускаем таймер на 1 минуту
            StartConfirmationTimeout();
            Console.WriteLine("Ожидание подтверждения провайдера 2FA...");
            cts.Token.WaitHandle.WaitOne(); // Ожидаем завершения работы
            Console.WriteLine("Программа завершена.");
            Environment.Exit(1);
        }
        [Obsolete]
        private static async Task SendAuthButtonsAsync(long chatId)
        {
            if (UpdateSent) return;

            var buttons = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Да, это я", "auth_yes"),
                    InlineKeyboardButton.WithCallbackData("Нет, это кто-то другой", "auth_no")
                }
            });

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Кто-то пытаетется войти на home-lb-ssh. Это Вы?",
                replyMarkup: buttons);
            UpdateSent = true;
        }

        [Obsolete]
        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                var callbackQuery = update.CallbackQuery!;
                if (callbackQuery.Data == "auth_yes")
                {
                    Console.WriteLine("Пользователь подтвердил вход. Запускаю оболочку Bash...");
                    await botClient.SendTextMessageAsync(
                        chatId: callbackQuery.Message!.Chat.Id,
                        text: "Доступ подтвержден. Запускаю оболочку Bash...",
                        cancellationToken: cancellationToken);

                    // Убираем часики с кнопок
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

                    // Отменяем таймер
                    confirmationTimeoutCts.Cancel();

                    Environment.Exit(0);
                }
                else if (callbackQuery.Data == "auth_no")
                {
                    Console.WriteLine("Пользователь отклонил вход. Завершаю выполнение...");
                    await botClient.SendTextMessageAsync(
                        chatId: callbackQuery.Message!.Chat.Id,
                        text: "Доступ отклонен. Завершаю работу...",
                        cancellationToken: cancellationToken);

                    // Убираем часики с кнопок
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

                    // Отменяем таймер
                    confirmationTimeoutCts.Cancel();

                    // Завершаем программу с кодом 1
                    Environment.Exit(1);
                }
            }
        }


        [Obsolete]
        private static void StartConfirmationTimeout()
        {
            Task.Run(async () =>
            {
                try
                {
                    // Ждем 1 минуту (60000 миллисекунд)
                    await Task.Delay(60000, confirmationTimeoutCts.Token);

                    Console.WriteLine("Время подтверждения истекло. Завершаю выполнение...");
                    await botClient.SendTextMessageAsync(
                        chatId: ChatId,
                        text: "Время для подтверждения истекло. Доступ отклонен.");

                    // Завершаем программу с кодом 1
                    Environment.Exit(1);
                }
                catch (TaskCanceledException)
                {
                    // Таймер был отменен (пользователь подтвердил или отклонил)
                    Console.WriteLine("Таймер подтверждения отменен.");
                    Environment.Exit(1);
                }
            });
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            Console.WriteLine(exception.StackTrace);
            return Task.CompletedTask;
        }
    }
}
