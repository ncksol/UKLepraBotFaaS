using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UKLepraBotFaaS.Functions
{
    public static class ReactionFunction
    {
        private static Random _rnd = new Random();

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
                Reaction reaction;
                Message message;

                using (new TimingScopeWrapper(log, "Deserializing reaction queue string took: {0}ms"))
                    input = JsonConvert.DeserializeObject<dynamic>(inputString);

                using (new TimingScopeWrapper(log, "Deserializing reaction dynamic class took: {0}ms"))
                    reaction = (input?.reaction as JObject)?.ToObject<Reaction>();
                
                using (new TimingScopeWrapper(log, "Deserializing message dynamic class took: {0}ms"))
                    message = (input?.message as JObject)?.ToObject<Message>();
                
                var chatId = message.Chat.Id.ToString();
                var replyToId = message.MessageId.ToString();

                if (reaction != null)
                {
                    var reactionReply = DoReaction(reaction);
                    using (new TimingScopeWrapper(log, "Adding message to output queue took: {0}ms"))
                    {
                        var data = new { ChatId = chatId, ReplyToMessageId = replyToId, reactionReply.Text, reactionReply.Sticker };
                        await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                    }
                }
                else
                {
                    var huifiedMessage = HuifyMeInternal(message.Text);
                    using (new TimingScopeWrapper(log, "Adding message to output queue took: {0}ms")) 
                    { 
                        var data = new { ChatId = chatId, ReplyToMessageId = replyToId, Text = huifiedMessage };
                        await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
                    }
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

        private static string HuifyMeInternal(string message)
        {
            var vowels = "оеаяуюы";
            var rulesValues = "еяюи";
            var rules = new Dictionary<string, string>
            {
                {"о", "е"},
                {"а", "я"},
                {"у", "ю"},
                {"ы", "и"}
            };
            var nonLettersPattern = new Regex("[^а-яё-]+");
            var onlyDashesPattern = new Regex("^-*$");
            var prefixPattern = new Regex("^[бвгджзйклмнпрстфхцчшщьъ]+");

            var messageParts = message.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (messageParts.Length < 1) return string.Empty;

            var word = messageParts[messageParts.Length - 1];
            var prefix = string.Empty;

            if (messageParts.Length > 1 && _rnd.Next(0, 1) == 1)
            {
                prefix = messageParts[messageParts.Length - 2];
            }

            word = nonLettersPattern.Replace(word.ToLower(), "");
            if (word == "бот")
            {
                return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "хуебот";
            }

            if (onlyDashesPattern.IsMatch(word))
                return string.Empty;

            var postFix = prefixPattern.Replace(word, "");
            if (postFix.Length < 3) return string.Empty;

            var foo = postFix.Substring(1, 1);
            if (word.Substring(2) == "ху" && rulesValues.Contains(foo))
            {
                return string.Empty;
            }

            if (rules.ContainsKey(postFix.Substring(0, 1)))
            {
                if (!vowels.Contains(foo))
                {
                    return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + rules[postFix.Substring(0, 1)] + postFix.Substring(1);
                }
                else
                {
                    if (rules.ContainsKey(foo))
                    {
                        return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + rules[foo] + postFix.Substring(2);
                    }
                    else
                    {
                        return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + postFix.Substring(1);
                    }
                }
            }
            else
            {
                return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + postFix;
            }
        }
    }
}
