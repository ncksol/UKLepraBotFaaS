using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Blob;
using Telegram.Bot.Types;
using Microsoft.WindowsAzure.Storage.Queue;

namespace UKLepraBotFaaS.Functions
{
    public static class SettingsFunction
    {
        private static ChatSettings _chatSettings;
        private static Random _rnd;
        private static ILogger _log;

        [FunctionName("SettingsFunction")]
        public static async Task Run(
            [QueueTrigger(Constants.SettingsQueueName)]Message input,
            [Blob(Constants.ChatSettingsBlobPath)] string chatSettingsString,
            [Blob(Constants.DataBlobPath)] CloudBlobContainer chatSettingsOutput,
            [Queue(Constants.OutputQueueName)] CloudQueue output,
            ILogger log)
        {
            _log = log;

            log.LogInformation("Processing SettingsFunction");
            try
            {
                _rnd = new Random();

                using (new TimingScopeWrapper(log, "Deserealizing chat settings took: {0}ms"))
                    _chatSettings = JsonConvert.DeserializeObject<ChatSettings>(chatSettingsString);

                var reply = ProcessSettingCommand(input);

                using (new TimingScopeWrapper(log, "Adding message to output queue took: {0}ms"))
                { 
                    var data = new { ChatId = input.Chat.Id, Text = reply };
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                }

                var newChatSettingsString = JsonConvert.SerializeObject(_chatSettings);                
                if(string.Equals(chatSettingsString, newChatSettingsString, StringComparison.InvariantCultureIgnoreCase) == false)
                {
                    using (new TimingScopeWrapper(log, "Saving update chat settings took: {0}ms"))
                    { 
                        var settingsBlob = chatSettingsOutput.GetBlockBlobReference("chatsettings.json");
                        await settingsBlob.UploadTextAsync(newChatSettingsString);
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing Settings function");
            }
        }

        private static string ProcessSettingCommand(Message message)
        {
            var chatId = message.Chat.Id.ToString();
            var from = message.From.Id.ToString();
            var text = message.Text;

            var delaySettings = _chatSettings.DelaySettings.ContainsKey(chatId)
                ? _chatSettings.DelaySettings[chatId]
                : null;
            var state = _chatSettings.State.ContainsKey(chatId)
                ? _chatSettings.State[chatId]
                : (bool?)null;
            var currentDelay = _chatSettings.Delay.ContainsKey(chatId)
                ? _chatSettings.Delay[chatId]
                : (int?)null;

            string reply = null;

            if (text.ToLower().Contains("/huify"))
                reply = StartHuifyCommand(chatId, from, delaySettings);
            else if (text.ToLower().Contains("/unhuify"))
                reply = StopHuifyCommand(chatId, from);
            else if (text.ToLower().Contains("/status"))
                reply = StatusCommand(state, currentDelay, delaySettings);
            else if (text.ToLower().Contains("/uptime"))
                reply = UptimeCommand();
            else if (text.ToLower().Contains("/delay"))
                reply = DelayCommand(text, chatId, from);
            else if (text.ToLower().Contains("/secret"))
                reply = SecretCommand(text);
            else if (text.ToLower().Contains("/sticker"))
                reply = SecretCommand(text);

            return reply;
        }

        private static string DelayCommand(string messageText, string conversationId, string from)
        {
            var reply = string.Empty;

            if (VerifyAdminCommandAccess(from) == false)
            {
                reply = GetAcccessDeniedCommandText();
                return reply;
            }

            var delaySettings = _chatSettings.DelaySettings;

            var messageParts = messageText.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (messageParts.Length == 1)
            {
                var currentDelay = new Tuple<int, int>(0, 4);
                if (delaySettings.ContainsKey(conversationId))
                    currentDelay = delaySettings[conversationId];

                reply = $"Сейчас я пропускаю случайное число сообщений от {currentDelay.Item1} до {currentDelay.Item2}";
            }
            else if (messageParts.Length == 2)
            {
                int newMaxDelay;
                if (!int.TryParse(messageParts[1], out newMaxDelay))
                {
                    reply = "Неправильный аргумент, отправьте /delay N [[M]], где N, M любое натуральное число";
                }
                else
                {
                    delaySettings[conversationId] = new Tuple<int, int>(0, newMaxDelay);
                    reply = $"Я буду пропускать случайное число сообщений от 0 до {newMaxDelay}";
                }
            }
            else if (messageParts.Length == 3)
            {
                int newMaxDelay;
                int newMinDelay;
                if (!int.TryParse(messageParts[2], out newMaxDelay))
                {
                    reply = "Неправильный аргумент, отправьте /delay N [[M]], где N, M любое натуральное число";
                }
                else if (!int.TryParse(messageParts[1], out newMinDelay))
                {
                    reply = "Неправильный аргумент, отправьте /delay N [[M]], где N, M любое натуральное число";
                }
                else
                {
                    if (newMinDelay == newMaxDelay)
                    {
                        newMinDelay = 0;
                    }
                    else if (newMinDelay > newMaxDelay)
                    {
                        var i = newMinDelay;
                        newMinDelay = newMaxDelay;
                        newMaxDelay = i;
                    }

                    _chatSettings.DelaySettings[conversationId] = new Tuple<int, int>(newMinDelay, newMaxDelay);
                    reply = $"Я буду пропускать случайное число сообщений от {newMinDelay} до {newMaxDelay}";
                }
            }

            return reply;
        }

        private static string UptimeCommand()
        {
            var uptime = DateTimeOffset.UtcNow - Configuration.Instance.StartupTime.Value;
            var reply =
                $"Uptime: {(int)uptime.TotalDays} days, {uptime.Hours} hours, {uptime.Minutes} minutes, {uptime.Seconds} seconds.";
            return reply;
        }

        private static string StatusCommand(bool? state, int? currentDelay, Tuple<int, int> delaySettings)
        {
            string reply;

            if (!state.HasValue)
                reply = "Хуятор не инициализирован." + Environment.NewLine;
            else if (state.Value)
                reply = "Хуятор активирован." + Environment.NewLine;
            else
                reply = "Хуятор не активирован." + Environment.NewLine;

            if (!currentDelay.HasValue)
                reply += "Я не знаю когда отреагирую в следующий раз." + Environment.NewLine;
            else
                reply += $"В следующий раз я отреагирую через {currentDelay.Value} сообщений." +
                              Environment.NewLine;

            if (delaySettings == null)
                reply += "Настройки задержки не найдены. Использую стандартные от 0 до 4 сообщений.";
            else
                reply +=
                    $"Сейчас я пропускаю случайное число сообщений от {delaySettings.Item1} до {delaySettings.Item2}";
            return reply;
        }

        private static string StopHuifyCommand(string conversationId, string from)
        {
            string reply;

            if (VerifyAdminCommandAccess(from) == false)
            {
                reply = GetAcccessDeniedCommandText();
                return reply;
            }

            reply = "Хуятор успешно деактивирован.";
            _chatSettings.State[conversationId] = false;
            return reply;
        }

        private static bool VerifyAdminCommandAccess(string from)
        {
            var masterId = Configuration.Instance.MasterId;
            var adminIds = Configuration.Instance.AdminIds?.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            adminIds.Add(masterId);

            return adminIds.Contains(from);
        }

        private static string GetAcccessDeniedCommandText()
        {
            return "Не положено холопам королеве указывать!";
        }

        private static string StartHuifyCommand(string conversationId, string from, Tuple<int, int> delaySettings)
        {
            string reply;

            if (VerifyAdminCommandAccess(from) == false)
            {
                reply = GetAcccessDeniedCommandText();
                return reply;
            }

            reply = "Хуятор успешно активирован.";
            _chatSettings.State[conversationId] = true;

            if (delaySettings != null)
                _chatSettings.Delay[conversationId] = _rnd.Next(delaySettings.Item1, delaySettings.Item2 + 1);
            else
                _chatSettings.Delay[conversationId] = _rnd.Next(4);

            return reply;
        }

        private static string SecretCommand(string messageText)
        {
            var messageParts = messageText.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

            var secretKey = Configuration.Instance.SecretKey;
            if (messageParts[1] != secretKey) return null;

            var secretMessage = string.Join(" ", messageParts.Skip(2));

            return secretMessage;
        }
    }
}
