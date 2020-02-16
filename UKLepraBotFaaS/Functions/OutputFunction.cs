using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace UKLepraBotFaaS.Functions
{
    public static class OutputFunction
    {
        [FunctionName("OutputFunction")]
        public async static Task Run([QueueTrigger(Constants.OutputQueueName)]string input, ILogger log)
        {
            var bot = new TelegramBotClient(Configuration.Instance.BotToken);
            try
            {
                var me = await bot.GetMeAsync();
                if(me == null) 
                    throw new Exception("Bot initialisation failed");

                dynamic data = JsonConvert.DeserializeObject(input);
                string chatId = data?.ChatId;
                string replyToMessageId = data?.ReplyToMessageId;
                string text = data?.Text;
                string sticker = data?.Sticker;
                bool? disableWebPagePreview = data?.DisableWebPagePreview ?? false;
                int? parseMode = data?.ParseMode ?? (int?)ParseMode.Default;

                if(string.IsNullOrEmpty(text) == false)
                {
                    await bot.SendTextMessageAsync(
                            chatId: chatId,
                            replyToMessageId: Convert.ToInt32(replyToMessageId),
                            text: text,
                            disableWebPagePreview: disableWebPagePreview.Value,
                            parseMode: (ParseMode)parseMode.Value);
                }
                else if(string.IsNullOrEmpty(sticker) == false)
                {
                    await bot.SendStickerAsync(
                            chatId: chatId,
                            replyToMessageId: Convert.ToInt32(replyToMessageId),
                            sticker: sticker);
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while running output function");
            }

        }
    }
}
