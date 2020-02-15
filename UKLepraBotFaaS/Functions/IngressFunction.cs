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
using Telegram.Bot.Requests;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace UKLepraBotFaaS.Functions
{
    public static class IngressFunction
    {
        [FunctionName("IngressFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [ServiceBus("messages", entityType:EntityType.Topic)] string test,
            ILogger log)
        {

            SendMessageRequest reply = null;
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                log.LogInformation(requestBody);

                var update = JsonConvert.DeserializeObject<Update>(requestBody);

                var text = update.Message.Text;

                log.LogInformation(text);

                reply = new SendMessageRequest(update.Message.Chat.Id, $"You said: {text}");

                log.LogInformation(JsonConvert.SerializeObject(reply));

            }
            catch (Exception e)
            {
                log.LogError(e, "Error while huifying message");
            }

            return new OkObjectResult(reply);
        }
    }
}
