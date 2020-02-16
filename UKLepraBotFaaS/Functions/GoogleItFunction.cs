using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using Telegram.Bot.Types;
using Microsoft.WindowsAzure.Storage.Queue;
using Telegram.Bot.Types.Enums;

namespace UKLepraBotFaaS.Functions
{
    public static class GoogleItFunction
    {
        private static string[] _rubbish = new[] { ".", ",", "-", "=", "#", "!", "?", "%", "@", "\"", "£", "$", "^", "&", "*", "(", ")", "_", "+", "]", "[", "{", "}", ";", ":", "~", "/", "<", ">", };

        [FunctionName("GoogleItFunction")]
        public static async Task Run(
            [QueueTrigger(Constants.GoogleItQueueName)]Message input,
            [Queue(Constants.OutputQueueName)] CloudQueue output,
            ILogger log)
        {
            log.LogInformation("Processing GoogleItFunction");

            try
            {
                var messageText = input.Text.ToLower() ?? string.Empty;

                if (messageText.ToLower().Contains("погугли"))
                {
                    var reply = GoogleCommand(input);

                    var data = new { ChatId = input.Chat.Id, ReplyToMessageId = input.MessageId, Text = reply, DisableWebPagePreview = true, ParseMode = (int)ParseMode.MarkdownV2 };
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing AI function");
            }
        }

        private static string GoogleCommand(Message message)
        {
            var activationWord = "погугли";

            var cleanedMessageText = message.Text;
            _rubbish.ToList().ForEach(x => cleanedMessageText = cleanedMessageText.Replace(x, " "));

            var messageParts = cleanedMessageText.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var activationWordPosition = messageParts.FindIndex(x => x.Equals(activationWord));
            if (activationWordPosition == -1 || activationWordPosition > 3) return string.Empty;

            var queryParts = messageParts.Skip(activationWordPosition + 1);
            if (!queryParts.Any()) return string.Empty;

            var query = string.Join("%20", queryParts);
            var reply = $"[Самому слабо было?](http://google.co.uk/search?q={query})";

            return reply;
        }
    }
}
