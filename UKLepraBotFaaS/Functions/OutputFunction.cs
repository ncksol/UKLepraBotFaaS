using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;

namespace UKLepraBotFaaS.Functions
{
    public static class OutputFunction
    {
        [FunctionName("OutputFunction")]
        public async static Task Run([QueueTrigger("output")]string input, ILogger log)
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

                if(string.IsNullOrEmpty(text) == false)
                {
                    await bot.SendTextMessageAsync(
                            chatId: chatId,
                            replyToMessageId: Convert.ToInt32(replyToMessageId),
                            text: text);
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
