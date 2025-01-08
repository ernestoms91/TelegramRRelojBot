using Telegram.Bot;
using System.IO;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net;

class Program
{
    static async Task Main(string[] args)
    {
        var token = "MyTOken"; // Reemplaza con tu token

        // Cliente HTTP sin proxy
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // Aumenta el tiempo de espera a 5 minutos
        };

        // Cliente HTTP con proxy y autenticación
        var proxy = new WebProxy
        {
            Address = new Uri("http://10.3.2.200:3128"), // Dirección y puerto del proxy
            Credentials = new NetworkCredential("your-username", "your-password") // Credenciales para el proxy
        };

        var httpClientHandlerWithProxy = new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true
        };

        var httpClientWithProxy = new HttpClient(httpClientHandlerWithProxy)
        {
            Timeout = TimeSpan.FromMinutes(5) // Configura el tiempo de espera
        };

        var bot = new TelegramBotClient(token, httpClient);

        using var cts = new CancellationTokenSource();

        // Manejador de actualizaciones
        //var receiverOptions = new ReceiverOptions
        //{
        //    AllowedUpdates = Array.Empty<UpdateType>() // Recibir todos los tipos de actualizaciones
        //};

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } // Mensajes y Callback Queries
        };


        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await bot.GetMe();
        Console.WriteLine($"@{me.Username} está funcionando... Presiona Escape para terminar");

        while (Console.ReadKey(true).Key != ConsoleKey.Escape) ;
        cts.Cancel(); // Detener el bot
    }

    // Método para manejar errores
    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Error en la API de Telegram:\n{apiRequestException.ErrorCode}\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine($"Ocurrió un error: {errorMessage}");
        return Task.CompletedTask;
    }


    // Método para manejar actualizaciones
    //private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    //{
    //    Console.WriteLine($"Nueva actualización recibida: {update.Type}");

    //    if (update.Message is Message message && message.Text is not null)
    //    {
    //        Console.WriteLine($"Mensaje recibido: {message.Text}");
    //        await botClient.SendMessage(
    //            chatId: message.Chat.Id,
    //            text: $"Recibí tu mensaje: {message.Text}",
    //            cancellationToken: cancellationToken
    //        );
    //    }
    //}
    private static readonly long AllowedChatId = -4736671935;
    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Verificar si el mensaje proviene del grupo autorizado
        if (update.Message is Message msg && msg.Chat.Id != AllowedChatId)
        {
            Console.WriteLine($"Entro alguien no autorizado: {msg.Chat.Username}");

            // Enviar un mensaje indicando que no tienen permiso para usar el bot
            await botClient.SendMessage(
                chatId: msg.Chat.Id,
                text: "Lo siento, no tienes permiso para usar este bot.",
                cancellationToken: cancellationToken
            );

            // Finalizar la ejecución para este mensaje
            return;
        }

        try
        {
            if (update.Message is Message message && message.Text is not null)
            {
                if (message.Text.ToLower() == "/start")
                {
                    // Mostrar teclado para seleccionar el año
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Selecciona un año:",
                        replyMarkup: GenerateYearKeyboard(),
                        cancellationToken: cancellationToken
                    );
                }
            }

            if (update.CallbackQuery is CallbackQuery callbackQuery)
            {
                if (callbackQuery.Data.StartsWith("year:"))
                {
                    var selectedYear = callbackQuery.Data.Split(':')[1];
                    await botClient.SendMessage(
                        chatId: callbackQuery.Message.Chat.Id,
                        text: $"Seleccionaste el año: {selectedYear}. Ahora selecciona un mes:",
                        replyMarkup: GenerateMonthKeyboard(selectedYear),
                        cancellationToken: cancellationToken
                    );
                }
                else if (callbackQuery.Data.StartsWith("month:"))
                {
                    var dataParts = callbackQuery.Data.Split(':');
                    var selectedYear = dataParts[1];
                    var selectedMonth = dataParts[2];

                    await botClient.SendMessage(
                        chatId: callbackQuery.Message.Chat.Id,
                        text: $"Seleccionaste el mes: {selectedMonth}/{selectedYear}. Ahora selecciona un día:",
                        replyMarkup: GenerateDayKeyboard(selectedYear, selectedMonth),
                        cancellationToken: cancellationToken
                    );
                }
                else if (callbackQuery.Data.StartsWith("day:"))
                {
                    var dataParts = callbackQuery.Data.Split(':');
                    var selectedYear = dataParts[1];
                    var selectedMonth = dataParts[2];
                    var selectedDay = dataParts[3];

                    await botClient.SendMessage(
                        chatId: callbackQuery.Message.Chat.Id,
                        text: $"Seleccionaste el día: {selectedDay}/{selectedMonth}/{selectedYear}. Ahora selecciona la hora:",
                        replyMarkup: GenerateHourKeyboard(selectedYear, selectedMonth, selectedDay),
                        cancellationToken: cancellationToken
                    );
                }
                else if (callbackQuery.Data.StartsWith("hour:"))
                {
                    var dataParts = callbackQuery.Data.Split(':');
                    var selectedYear = dataParts[1];
                    var selectedMonth = dataParts[2];
                    var selectedDay = dataParts[3];
                    var selectedHour = dataParts[4];

                    var folderPath = $@"D:\Grabaciones\{selectedYear}\{selectedMonth}\{selectedDay}";
                    var hourPattern = $"{selectedYear}-{selectedMonth}-{selectedDay}_{selectedHour.PadLeft(2, '0')}h";

                    var matchingFiles = Directory.Exists(folderPath)
                        ? Directory.GetFiles(folderPath, "*.mp3")
                              .Where(file => Path.GetFileName(file).StartsWith(hourPattern))
                              .ToList()
                        : new List<string>();

                    if (matchingFiles.Count > 0)
                    {
                        // Obtener el nombre de usuario si está disponible, de lo contrario usar el primer nombre
                        var userNameOrFirstName = callbackQuery.From.Username ?? callbackQuery.From.FirstName ?? "Usuario desconocido";
                        // Notificar al usuario cuántos archivos se encontraron
                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: $"{userNameOrFirstName} se han encontrado {matchingFiles.Count} archivo(s). En breve se intentará enviar. Por favor, espere y no realice más operaciones.",
                            cancellationToken: cancellationToken
                        );

                        foreach (var filePath in matchingFiles)
                        {
                            try
                            {
                                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                                await botClient.SendDocument(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    document: new InputFileStream(stream, Path.GetFileName(filePath)),
                                    caption: $"Archivo: {Path.GetFileName(filePath)}",
                                    cancellationToken: cancellationToken
                                );
                            }
                            catch (Telegram.Bot.Exceptions.RequestException ex)
                            {
                                Console.WriteLine($"Error al enviar archivo {filePath}: {ex.Message}");
                                await botClient.SendMessage(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    text: $"No se pudo enviar el archivo {Path.GetFileName(filePath)}. Intenta más tarde.",
                                    cancellationToken: cancellationToken
                                );
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error inesperado al enviar archivo {filePath}: {ex.Message}");
                                await botClient.SendMessage(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    text: $"Ocurrió un error inesperado al enviar el archivo {Path.GetFileName(filePath)}.",
                                    cancellationToken: cancellationToken
                                );
                            }
                        }
                    }
                    else
                    {
                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: "No se encontraron audios con la hora seleccionada.",
                            cancellationToken: cancellationToken
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error general: {ex.Message}");
            await botClient.SendMessage(
                chatId: update.Message?.Chat.Id ?? update.CallbackQuery?.Message.Chat.Id ?? 0,
                text: "Ocurrió un error procesando tu solicitud. Por favor, intenta de nuevo más tarde.",
                cancellationToken: cancellationToken
            );
        }
    }



    private static InlineKeyboardMarkup GenerateYearKeyboard()
    {
        var currentYear = DateTime.Now.Year;
        var buttons = Enumerable.Range(currentYear - 3, 4)
            .Select(year => InlineKeyboardButton.WithCallbackData(year.ToString(), $"year:{year}"))
            .ToArray();

        return new InlineKeyboardMarkup(buttons);
    }

    private static InlineKeyboardMarkup GenerateMonthKeyboard(string year)
    {
        // Crear botones para los 12 meses (de "01" a "12")
        var buttons = Enumerable.Range(1, 12)
            .Select(month => InlineKeyboardButton.WithCallbackData(
                $"{month:D2}", // Formato "01", "02", ..., "12"
                $"month:{year}:{month:D2}"))
            .Chunk(4) // Agrupar en filas de 4 meses por fila para mejor visualización
            .Select(row => row.ToArray())
            .ToArray();

        return new InlineKeyboardMarkup(buttons);
    }


    private static InlineKeyboardMarkup GenerateDayKeyboard(string year, string month)
    {
        int daysInMonth = DateTime.DaysInMonth(int.Parse(year), int.Parse(month));
        var buttons = Enumerable.Range(1, daysInMonth)
            .Select(day => InlineKeyboardButton.WithCallbackData(
                $"{day:D2}", // Formato "01", "02", ..., "31"
                $"day:{year}:{month}:{day:D2}"))
            .Chunk(7) // Agrupar botones en filas de 7 para no saturar la pantalla
            .Select(row => row.ToArray())
            .ToArray();

        return new InlineKeyboardMarkup(buttons);
    }

    private static InlineKeyboardMarkup GenerateHourKeyboard(string year, string month, string day)
    {
        // Generar los botones para cada hora del día (de 0 a 23)
        var buttons = Enumerable.Range(0, 24) // De 00 a 23 horas
            .Select(hour =>
            {
                // Convertir la hora de 24 horas a 12 horas con AM/PM
                var hour12 = hour % 12; // Obtiene la hora en formato de 12 horas (0-11)
                var amPm = hour < 12 ? "AM" : "PM"; // Determina si es AM o PM
                var displayHour = (hour12 == 0) ? 12 : hour12; // Si es 0 (medianoche), se muestra 12
                var displayText = $"{displayHour:D2} {amPm}"; // Muestra la hora con AM/PM

                return InlineKeyboardButton.WithCallbackData(displayText, $"hour:{year}:{month}:{day}:{hour:D2}");
            })
            .ToArray();

        // Agrupar botones en filas de 4 (esto evitará demasiados botones en una sola fila)
        var buttonChunks = buttons
            .Chunk(4) // Agrupar en 4 botones por fila
            .Select(row => row.ToArray())
            .ToArray();

        return new InlineKeyboardMarkup(buttonChunks);
    }





}
