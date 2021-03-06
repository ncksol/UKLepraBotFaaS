﻿using System;
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
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage.Blob;

namespace UKLepraBotFaaS.Functions
{
    public static class InputFunction
    {
        private static CloudQueue _settingsQueueOutput;
        private static CloudQueue _aiQueueOutput;
        private static CloudQueue _boyanQueueOutput;
        private static CloudQueue _reactionsQueueOutput;
        private static CloudQueue _chatMembersUpdateOutput;
        private static CloudQueue _outputQueue;

        private static CloudBlobContainer _dataBlobContainer;

        private static ReactionsList _reactions;
        private static ChatSettings _chatSettings;

        private static readonly List<string> _settingsFunctionActivators = new List<string> { "/status", "/huify", "/unhuify", "/uptime", "/delay", "/secret", "/reload", "/sticker" };
        public static readonly List<string> _aiFunctionActivators = new List<string> { "погугли" };

        private static ILogger _log;

        [FunctionName("InputFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue(Constants.SettingsQueueName)] CloudQueue settingsQueueOutput,
            [Queue(Constants.GoogleItQueueName)] CloudQueue aiQueueOutput,
            [Queue(Constants.BoyansQueueName)] CloudQueue boyanQueueOutput,
            [Queue(Constants.ReactionsQueueName)] CloudQueue reactionsQueueOutput,
            [Queue(Constants.ChatMembersUpdateQueueName)] CloudQueue chatMembersUpdateOutput,
            [Blob(Constants.ReactionsBlobPath)] string reactionsString,
            [Blob(Constants.ChatSettingsBlobPath)] string chatSettingsString,
            [Blob(Constants.DataBlobPath)] CloudBlobContainer dataBlobContainer,
            [Queue(Constants.OutputQueueName)] CloudQueue outputQueue,
            ILogger log)
        {
            _log = log;
            _settingsQueueOutput = settingsQueueOutput;
            _aiQueueOutput = aiQueueOutput;
            _boyanQueueOutput = boyanQueueOutput;
            _reactionsQueueOutput = reactionsQueueOutput;
            _chatMembersUpdateOutput = chatMembersUpdateOutput;
            _outputQueue = outputQueue;
            _dataBlobContainer = dataBlobContainer;

            log.LogInformation("Processing InputFuction");

            try
            {
                Update update;
                using(new TimingScopeWrapper(log, "Reading request body took: {0}ms"))
                { 
                    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();                
                    update = JsonConvert.DeserializeObject<Update>(requestBody);
                };

                if(update.Type != UpdateType.Message) return new OkObjectResult("");
                
                _reactions = JsonConvert.DeserializeObject<ReactionsList>(reactionsString);
                _chatSettings = JsonConvert.DeserializeObject<ChatSettings>(chatSettingsString);

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
            else if (message.Type == MessageType.Text || message.Type == MessageType.Sticker)
            {
                await ProcessMessage(message);
            }
        }

        public async static Task ProcessMessage(Message message)
        {            
            if (message.Type == MessageType.Text && HelperMethods.MentionsBot(message) && !string.IsNullOrEmpty(message.Text) && _settingsFunctionActivators.Any(x => message.Text.ToLower().Contains(x)))
            {
                _log.LogInformation("Matched settings queue");
                await _settingsQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
            }
            else if (message.Type == MessageType.Text && !string.IsNullOrEmpty(message.Text) && _aiFunctionActivators.Any(x => message.Text.ToLower().Contains(x)))
            {
                _log.LogInformation("Matched GoogleIt queue");
                using (new TimingScopeWrapper(_log, "Adding message to google queue took: {0}ms"))
                    await _aiQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
            }
            else if(message.Type == MessageType.Text && IsUrl(message, out var url))
            {
                _log.LogInformation("Matched Boyan queue");
                var data = new { message, url};
                using (new TimingScopeWrapper(_log, "Adding message to boyan queue took: {0}ms"))
                    await _boyanQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
            }
            else if (message.Chat.Type == ChatType.Private && message.From.Id.ToString() == Configuration.Instance.MasterId && message.Sticker != null)
            {
                _log.LogInformation("Matched sticker queue");
                var data = new { ChatId = message.Chat.Id, Text = message.Sticker.FileId };
                await _outputQueue.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
            }
            else
            {
                _log.LogInformation("Matched reaction queue");
                if (IsReaction(message, out var reaction))
                {
                    var data = new { reaction, message };
                    using (new TimingScopeWrapper(_log, "Adding message to reaction queue took: {0}ms"))
                        await _reactionsQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                }
                else
                {
                    //if not a special reaction check chat settings if should react to the message
                    var shouldProcessMessage = ShouldProcessMessage(_chatSettings, message);

                    var settingsBlob = _dataBlobContainer.GetBlockBlobReference("chatsettings.json");
                    await settingsBlob.UploadTextAsync(JsonConvert.SerializeObject(_chatSettings));

                    if (shouldProcessMessage)
                    {
                        var data = new { reaction = "", message };
                        using (new TimingScopeWrapper(_log, "Adding message to huify queue took: {0}ms"))
                            await _reactionsQueueOutput.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                    }
                }
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

        private static bool IsUrl(Message message, out string url)
        {
            var rgx = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&\/\/=]*)");

            var match = rgx.Match(message.Text);
            url = match.ToString();

            return match.Success;
        }

        private static bool ShouldProcessMessage(ChatSettings chatSettings, Message message)
        {
            var rnd = new Random();
            var conversationId = message.Chat.Id.ToString();

            var state = chatSettings.State;
            var delay = chatSettings.Delay;
            var delaySettings = chatSettings.DelaySettings;
            if (!state.ContainsKey(conversationId) || !state[conversationId])//huify is not active or was never activated
                return false;
           
            var shouldProcessMessage = false;
            var resetDelay = false;
            if (delay.ContainsKey(conversationId))
            {
                if (delay[conversationId] > 0)
                {
                    delay[conversationId] -= 1;
                }
                else if(delay[conversationId] == 0 && message.From.Id.ToString() != Configuration.Instance.MasterId)
                {
                    shouldProcessMessage = true;                    
                }
            }
            else
            {
                resetDelay = true;
            }

            if(resetDelay || shouldProcessMessage)
            {
                Tuple<int, int> delaySetting;
                if (delaySettings.TryGetValue(conversationId, out delaySetting))
                    delay[conversationId] = rnd.Next(delaySetting.Item1, delaySetting.Item2 + 1);
                else
                    delay[conversationId] = rnd.Next(4);
            }

            return shouldProcessMessage;
        }
    }
}
