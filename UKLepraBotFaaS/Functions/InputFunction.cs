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
using Telegram.Bot.Types.Enums;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Generic;

namespace UKLepraBotFaaS.Functions
{
    public static class InputFunction
    {
        private static CloudQueue _huifyQueueOutput;
        private static CloudQueue _settingsQueueOutput;
        private static CloudQueue _aiQueueOutput;
        private static CloudQueue _reactionsQueueOutput;
        private static CloudQueue _chatMembersUpdateOutput;

        private static ReactionsList _reactions;

        private static readonly List<string> _settingsFunctionActivators = new List<string> { "/status", "/huify", "/unhuify", "/uptime", "/delay", "/secret", "/reload" };
        public static readonly List<string> _aiFunctionActivators = new List<string> { "погугли" };

        private static ILogger _log;

        [FunctionName("InputFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue(Constants.HuifyQueueName)] CloudQueue huifyQueueOutput,
            [Queue(Constants.SettingsQueueName)] CloudQueue settingsQueueOutput,
            [Queue(Constants.GoogleItQueueName)] CloudQueue aiQueueOutput,
            [Queue(Constants.ReactionsQueueName)] CloudQueue reactionsQueueOutput,
            [Queue(Constants.ChatMembersUpdateQueueName)] CloudQueue chatMembersUpdateOutput,
            [Blob(Constants.ReactionsBlobPath)] string reactionsString,
            ILogger log)
        {
            _log = log;
            _huifyQueueOutput = huifyQueueOutput;
            _settingsQueueOutput = settingsQueueOutput;
            _aiQueueOutput = aiQueueOutput;
            _reactionsQueueOutput = reactionsQueueOutput;
            _chatMembersUpdateOutput = chatMembersUpdateOutput;

            log.LogInformation("Processing InputFuction");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();                
                var update = JsonConvert.DeserializeObject<Update>(requestBody);
                if(update.Type != UpdateType.Message) return new OkObjectResult("");

                _reactions = JsonConvert.DeserializeObject<ReactionsList>(reactionsString);

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
            if(message.Type == MessageType.ChatMembersAdded || message.Type == MessageType.ChatMemberLeft)
            {
                _log.LogInformation("Matched chatmemebersupdate queue");

                var data = new {Type = (int)message.Type, message.NewChatMembers, ChatId = message.Chat.Id, message.MessageId};

                await _chatMembersUpdateOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
            }
            else if (message.Type == MessageType.Text)
            {
                await ProcessMessage(message);
            }
        }

        public async static Task ProcessMessage(Message message)
        {
            if (HelperMethods.MentionsBot(message) && !string.IsNullOrEmpty(message.Text) && _settingsFunctionActivators.Any(x => message.Text.ToLower().Contains(x)))
            {
                _log.LogInformation("Matched settings queue");
                await _settingsQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
            }
            else if (!string.IsNullOrEmpty(message.Text) && _aiFunctionActivators.Any(x => message.Text.ToLower().Contains(x)))
            {
                _log.LogInformation("Matched GoogleIt queue");
                await _aiQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
            }
            else if (IsReaction(message, out var reaction))
            {
                _log.LogInformation("Matched reaction queue");
                var data = new { reaction = reaction, chatid = message.Chat.Id, replytoid = message.MessageId };
                await _reactionsQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
            }
            else
            {
                _log.LogInformation("Matched huify queue");
                await _huifyQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
            }
        }

        private static bool IsReaction(Message message, out Reaction reaction)
        {
            var messageText = message.Text?.ToLower() ?? string.Empty;

            reaction = _reactions.Items.FirstOrDefault(x => x.Triggers.Any(messageText.Contains));

            if (reaction == null) return false;
            if (reaction.IsMentionReply && HelperMethods.MentionsBot(message) == false) return false;
            if (reaction.IsAlwaysReply == false && HelperMethods.YesOrNo() == false) return false;

            return true;
        }
    }
}
