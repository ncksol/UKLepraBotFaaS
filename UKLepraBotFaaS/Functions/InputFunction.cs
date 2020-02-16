using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Requests;
using Microsoft.Azure.WebJobs.ServiceBus;
using Telegram.Bot.Types.Enums;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Queue;

namespace UKLepraBotFaaS.Functions
{
    public static class InputFunction
    {
        private static CloudQueue _huifyQueueOutput;

        [FunctionName("InputFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue("huify")] CloudQueue huifyQueueOutput,
            ILogger log)
        {
            _huifyQueueOutput = huifyQueueOutput;

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                log.LogInformation(requestBody);

                var update = JsonConvert.DeserializeObject<Update>(requestBody);

                if(update.Type != UpdateType.Message) return new OkObjectResult("");

                await BotOnMessageReceived(update.Message);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while incoming message");
            }

            return new OkObjectResult("");
        }

        private static async Task BotOnMessageReceived(Message message)
        {
            //if (message.Type == MessageType.ChatMembersAdded && message.NewChatMembers.Any())
            //{
            //    var newUser = message.NewChatMembers.First();//(x => x.IsBot == false);

            //    var name = $"{newUser.FirstName} {newUser.LastName}".TrimEnd();
            //    var reply = $"[{name}](tg://user?id={newUser.Id}), ты вообще с какого посткода";

            //    await _bot.SendTextMessageAsync(
            //            chatId: message.Chat.Id,
            //            replyToMessageId: message.MessageId,
            //            text: reply,
            //            parseMode: ParseMode.MarkdownV2);

            //    Console.WriteLine("Processed ChatMembersAdded event");
            //    return;
            //}

            //if (message.Type == MessageType.ChatMemberLeft)
            //{
            //    var sticker = new InputOnlineFile(Stickers.DaIHuiSNim);
            //    await _bot.SendStickerAsync(chatId: message.Chat.Id, sticker: sticker);

            //    Console.WriteLine("Processed ChatMemberLeft event");
            //    return;
            //}

            if (message.Type == MessageType.Text)
            {
                await _huifyQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
            }
        }
    }
}
