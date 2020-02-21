using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types;

namespace UKLepraBotFaaS.Functions
{
    public static class BoyanFunction
    {
        [FunctionName("BoyanFunction")]
        public async static Task Run(
            [QueueTrigger(Constants.BoyansQueueName)]string inputString,
            [Table("boyans")] CloudTable cloudTable,
            [Queue(Constants.OutputQueueName)] CloudQueue output,
            ILogger log)
        {
            try
            {
                dynamic input;
                using (new TimingScopeWrapper(log, "Deserializing boyan queue string took: {0}ms"))
                    input = JsonConvert.DeserializeObject<dynamic>(inputString);

                var message = (input?.message as JObject).ToObject<Message>();
                var url = ((string)input?.url).TrimEnd('/', '?');
                var uri = new Uri(url);

                string cleanUrl;
                if(uri.Host.Contains("youtube.com"))
                {
                    cleanUrl = uri.Host.Replace("www.", "") + uri.PathAndQuery;
                }
                else
                {
                    cleanUrl = uri.Host.Replace("www.", "") + uri.AbsolutePath;
                }

                var urlQuery = new TableQuery<BoyanEntity>().Where(TableQuery.CombineFilters(
                                                                                    TableQuery.GenerateFilterCondition("Url", QueryComparisons.Equal, cleanUrl), 
                                                                                    TableOperators.And, 
                                                                                    TableQuery.GenerateFilterCondition("ChatId", QueryComparisons.Equal, message.Chat.Id.ToString())));

                var results = await cloudTable.ExecuteQuerySegmentedAsync(urlQuery, null);
                
                var firstResult = results.Results.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                if(firstResult != null)
                {
                    var data = CreateReactionData(message);
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(data)));

                    var forwardData = new { ChatId = message.Chat.Id, ForwardMessageId = firstResult.MessageId};
                    await output.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(forwardData)));
                }
                else
                {
                    var entity = new BoyanEntity 
                    {
                        Url = cleanUrl, 
                        MessageId = message.MessageId.ToString(), 
                        ChatId = message.Chat.Id.ToString(), 
                        PartitionKey = message.Chat.Id.ToString(), 
                        RowKey = message.MessageId.ToString() 
                    };
                    var operation = TableOperation.Insert(entity);
                    await cloudTable.ExecuteAsync(operation);
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing boyan function");
            }
        }

        private static dynamic CreateReactionData(Message message)
        {
            dynamic data;

            var choice = HelperMethods.RandomInt(5);

            switch(choice)
            {
                case(0):
                    data = new { ChatId = message.Chat.Id, ReplyToMessageId = message.MessageId, Gif = "CgACAgQAAxkBAAIEQ15QD_bCNvjf36sYlUAOCls-ND4LAALPfgACKh5kB1o5iiPKfPqXGAQ" };//women
                    break;
                case(1):
                    data = new { ChatId = message.Chat.Id, ReplyToMessageId = message.MessageId, Gif = "CgACAgQAAxkBAAIESF5QEQcF2NgB1NEQomyNme2gs0_kAAKFnwACthtkB5DZuQn_m46kGAQ" };//slowpoke
                    break;
                case(2):
                    data = new { ChatId = message.Chat.Id, ReplyToMessageId = message.MessageId, Sticker = "CAACAgIAAxkBAAIERl5QEPOpZIIDh43WBVdSRbwvbtKyAAIXAwACLCJ0A3JH7gsBzApwGAQ" };//slowpoke wos
                    break;
                case(3):
                    data = new { ChatId = message.Chat.Id, ReplyToMessageId = message.MessageId, Sticker = "CAACAgIAAxkBAAIEKV5P-nwebZRQyH8P54v4QuKa1ZZNAAJlAANcIA0AAerRSEyi5jbSGAQ" };//slowpoke gik.me
                    break;
                default:
                    data = new { ChatId = message.Chat.Id, ReplyToMessageId = message.MessageId, Text = "Боян!" };
                    break;
            }

            return data;
        }
    }    
}
