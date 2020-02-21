using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace UKLepraBotFaaS.Functions
{
    public static class OutputFunction
    {
        [FunctionName("OutputFunction")]
        public async static Task Run([QueueTrigger(Constants.OutputQueueName)]string input, ILogger log)
        {
            log.LogInformation("Processing OutputFunction");

            var bot = new TelegramBotClient(Configuration.Instance.BotToken);
            try
            {
                using (new TimingScopeWrapper(log, "Bot GetMeAsync call took: {0}ms"))
                { 
                    var me = await bot.GetMeAsync();
                    if(me == null) 
                        throw new Exception("Bot initialisation failed");
                }

                dynamic data;
                using (new TimingScopeWrapper(log, "Desearializing input string in output queue took: {0}ms"))
                { 
                    data = JsonConvert.DeserializeObject(input);
                }

                string chatId = data?.ChatId;
                string replyToMessageId = data?.ReplyToMessageId;
                string text = data?.Text;
                string sticker = data?.Sticker;
                string gif = data?.Gif;
                bool? disableWebPagePreview = data?.DisableWebPagePreview ?? false;
                int? parseMode = data?.ParseMode ?? (int?)ParseMode.Default;
                

                if(string.IsNullOrEmpty(text) == false)
                {
                    using (new TimingScopeWrapper(log, "Replying with text message took: {0}ms"))
                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            replyToMessageId: Convert.ToInt32(replyToMessageId),
                            text: text,
                            disableWebPagePreview: disableWebPagePreview.Value,
                            parseMode: (ParseMode)parseMode.Value);
                }
                else if(string.IsNullOrEmpty(sticker) == false)
                {
                    using (new TimingScopeWrapper(log, "Replying with sticker message took: {0}ms"))
                        await bot.SendStickerAsync(
                            chatId: chatId,
                            replyToMessageId: Convert.ToInt32(replyToMessageId),
                            sticker: sticker);
                }
                else if(string.IsNullOrEmpty(gif) == false)
                {
                    using (new TimingScopeWrapper(log, "Replying with gif message took: {0}ms"))
                        await bot.SendDocumentAsync(
                            chatId: chatId,
                            replyToMessageId: Convert.ToInt32(replyToMessageId), 
                            document: new InputOnlineFile(gif));
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while running output function");
            }

        }
    }
}
