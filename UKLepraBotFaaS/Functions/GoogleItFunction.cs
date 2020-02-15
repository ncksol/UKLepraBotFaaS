using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;

namespace UKLepraBotFaaS.Functions
{
    public static class GoogleItFunction
    {
        private static string[] _rubbish = new[] { ".", ",", "-", "=", "#", "!", "?", "%", "@", "\"", "£", "$", "^", "&", "*", "(", ")", "_", "+", "]", "[", "{", "}", ";", ":", "~", "/", "<", ">", };

        [FunctionName("GoogleItFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var reply = string.Empty;

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                var message = Convert.ToString(data?.message);

                if (string.IsNullOrEmpty(message))
                    return new BadRequestObjectResult("Please pass a message in the request body");

                reply = GoogleCommand(message);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing GoogleIt function");
            }

            if (string.IsNullOrEmpty(reply))
                return new ObjectResult(null);

            return new ObjectResult(reply);
        }

        private static string GoogleCommand(string message)
        {
            var activationWord = "погугли";

            var cleanedMessageText = message;
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
