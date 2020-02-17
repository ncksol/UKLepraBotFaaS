using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace UKLepraBotFaaS.Functions
{
    public static class ChatMembersUpdateFunction
    {
        [FunctionName("ChatMembersUpdateFunction")]
        public async static Task Run(
            [QueueTrigger(Constants.ChatMembersUpdateQueueName)]string input,
            [Queue(Constants.OutputQueueName)] CloudQueue output,
            ILogger log)
        {
            log.LogInformation("Processing ChatMembersUpdateFunction");

            try
            {
                var inputData = JsonConvert.DeserializeObject<dynamic>(input);
                var type = (int?)inputData?.Type;
                var newChatMembers = ((JArray)inputData?.NewChatMembers)?.ToObject<List<User>>() ?? new List<User>();
                var chatId = inputData?.ChatId;
                var messageId = inputData?.MessageId;

                if (type.GetValueOrDefault() == (int)MessageType.ChatMembersAdded && newChatMembers.Any())
                {
                    var newUser = newChatMembers.First();//(x => x.IsBot == false);

                    var name = $"{newUser.FirstName} {newUser.LastName}".TrimEnd();
                    var reply = $"[{name}](tg://user?id={newUser.Id}), ты вообще с какого посткода";

                    log.LogInformation("Processed ChatMembersAdded event");

                    var data = new { ChatId = chatId, ReplyToMessageId = messageId, Text = reply, ParseMode = (int)ParseMode.MarkdownV2 };
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                }

                if (type.GetValueOrDefault() == (int)MessageType.ChatMemberLeft)
                {
                    var data = new { ChatId = chatId, ReplyToMessageId = messageId, Sticker = Constants.MemberLeftSticker};
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));

                    log.LogInformation("Processed ChatMemberLeft event");
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while huifying message");
            }
        }
    }
}
