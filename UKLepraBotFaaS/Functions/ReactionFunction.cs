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

namespace UKLepraBotFaaS.Functions
{
    public static class ReactionFunction
    {
        [FunctionName("ReactionFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Blob("data/reactions.json")] string reactionsString,
            ILogger log)
        {
            Reply reply = null;

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                var message = Convert.ToString(data?.message);

                if (string.IsNullOrEmpty(message))
                    return new BadRequestObjectResult("Please pass a message in the request body");

                reply = DoReaction(message, reactionsString);

            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing Reaction function");
            }

            return new ObjectResult(reply);
        }

        private static Reply DoReaction(string message, string reactionsString)
        {
            var reactions = JsonConvert.DeserializeObject<ReactionsList>(reactionsString);

            var messageText = message.ToLower();

            var reaction = reactions.Items.FirstOrDefault(x => x.Triggers.Any(messageText.Contains));
            
            if (reaction == null) return null;
            if (reaction.IsMentionReply && HelperMethods.MentionsBot(message) == false) return null;
            if (reaction.IsAlwaysReply == false && HelperMethods.YesOrNo() == false) return null;

            var reactionReply = reaction.Replies.Count <= 1 ? reaction.Replies.FirstOrDefault() : reaction.Replies[HelperMethods.RandomInt(reaction.Replies.Count)];
            return reactionReply;
        }
    }
}
