using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;

namespace UKLepraBotFaaS.Functions
{
    public static class HuifyFunction
    {
        private static Random _rnd = new Random();

        [FunctionName("HuifyFunction")]
        public async static Task Run(
            [QueueTrigger(Constants.HuifyQueueName)]Message input,
            [Blob(Constants.ChatSettingsBlobPath)] string chatSettingsString,
            [Blob(Constants.DataBlobPath)] CloudBlobContainer chatSettingsOutput,
            [Queue(Constants.OutputQueueName)] CloudQueue output, 
            ILogger log)
        {
            log.LogInformation("Processing HuifyFunction");

            try
            {
                var chatSettings = JsonConvert.DeserializeObject<ChatSettings>(chatSettingsString);

                var shouldProcessMessage = ShouldProcessMessage(chatSettings, input);

                var settingsBlob = chatSettingsOutput.GetBlockBlobReference("chatsettings.json");
                await settingsBlob.UploadTextAsync(JsonConvert.SerializeObject(chatSettings));

                if(shouldProcessMessage == false) return;

                var message = input.Text;
                var huifiedMessage = HuifyMeInternal(message);

                var data = new {ChatId = input.Chat.Id, ReplyToMessageId = input.MessageId, Text = huifiedMessage};
                await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while huifying message");
            }
        }

        private static bool ShouldProcessMessage(ChatSettings chatSettings, Message message)
        {            
            var conversationId = message.Chat.Id.ToString();

            var state = chatSettings.State;
            var delay = chatSettings.Delay;
            var delaySettings = chatSettings.DelaySettings;
            if (!state.ContainsKey(conversationId) || !state[conversationId])//huify is not active or was never activated
                return false;

            if (delay.ContainsKey(conversationId))
            {
                if(delay[conversationId] > 0)
                    delay[conversationId] -= 1;
            }
            else
            {
                Tuple<int, int> delaySetting;
                if (delaySettings.TryGetValue(conversationId, out delaySetting))
                    delay[conversationId] = _rnd.Next(delaySetting.Item1, delaySetting.Item2 + 1);
                else
                    delay[conversationId] = _rnd.Next(4);
            }

            if (delay[conversationId] != 0 || message.From.Id.ToString() == Configuration.Instance.MasterId) return false;

            delay.Remove(conversationId);

            return true;
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
            if (messageParts.Length < 1) return String.Empty;

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
