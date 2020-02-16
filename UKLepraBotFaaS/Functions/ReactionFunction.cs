using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;

namespace UKLepraBotFaaS.Functions
{
    public static class ReactionFunction
    {
        [FunctionName("ReactionFunction")]
        public static async Task Run(
            [QueueTrigger(Constants.ReactionsQueueName)]string inputString,
            [Queue(Constants.OutputQueueName)] CloudQueue output,
            ILogger log)
        {
            try
            {
                log.LogInformation(inputString);
                var input = JsonConvert.DeserializeObject<dynamic>(inputString);
                var reaction = (input?.reaction as JObject).ToObject<Reaction>();
                var chatId = input?.chatid;
                var replyToId = input?.replytoid;

                var reply = DoReaction(reaction);
                var data = new { ChatId = chatId, ReplyToMessageId = replyToId, Text = reply.Text, Sticker = reply.Sticker };
                await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing Reaction function");
            }
        }

        private static Reply DoReaction(Reaction reaction)
        {
            var reactionReply = reaction.Replies.Count <= 1 ? reaction.Replies.FirstOrDefault() : reaction.Replies[HelperMethods.RandomInt(reaction.Replies.Count)];
            return reactionReply;
        }
    }
}
