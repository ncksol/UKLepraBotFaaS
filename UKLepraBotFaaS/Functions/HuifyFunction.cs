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
using System.Text.RegularExpressions;

namespace UKLepraBotFaaS.Functions
{
    public static class HuifyFunction
    {
        private static Random _rnd = new Random();

        [FunctionName("HuifyFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var huifiedMessage = string.Empty;

            try
            { 
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                var message = Convert.ToString(data?.message);

                if (string.IsNullOrEmpty(message))
                    return new BadRequestObjectResult("Please pass a message in the request body");

                huifiedMessage = HuifyMeInternal(message);

            }
            catch (Exception e)
            {
                log.LogError(e, "Error while huifying message");
            }

            if (string.IsNullOrEmpty(huifiedMessage))
                return new ObjectResult(null);

            return new ObjectResult(huifiedMessage);
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
