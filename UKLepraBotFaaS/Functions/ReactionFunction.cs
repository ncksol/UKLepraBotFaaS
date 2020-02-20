using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
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
            log.LogInformation("Processing ReactionFunction");

            try
            {
                dynamic input;
                dynamic reaction;
                using (new TimingScopeWrapper(log, "Deserializing reaction queue string took: {0}ms"))
                    input = JsonConvert.DeserializeObject<dynamic>(inputString);

                using (new TimingScopeWrapper(log, "Deserializing reaction dynamic class took: {0}ms"))
                    reaction = (input?.reaction as JObject).ToObject<Reaction>();
                
                var chatId = input?.chatid;
                var replyToId = input?.replytoid;

                var reply = DoReaction(reaction);
                using (new TimingScopeWrapper(log, "Adding message to output queue took: {0}ms"))
                { 
                    var data = new { ChatId = chatId, ReplyToMessageId = replyToId, Text = reply.Text, Sticker = reply.Sticker };
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                }
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
