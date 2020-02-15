using System;
using System.Collections.Generic;
using System.Text;

namespace UKLepraBotFaaS
{
    public class HelperMethods
    {
        public static bool YesOrNo()
        {
            var rnd = new Random();
            return rnd.Next() % 2 == 0;
        }

        public static int RandomInt(int max)
        {
            var rnd = new Random();
            return rnd.Next(max);
        }

        public static bool MentionsId(string message, string id)
        {
            //var channelData = (JObject)message.ChannelData;
            //var messageData = JsonConvert.DeserializeObject<JsonModels.Message>(channelData["message"].ToString());

            //if (messageData?.reply_to_message?.@from?.username == WebApiApplication.TelegramBotName)
            //    return true;

            if (string.IsNullOrEmpty(message)) return false;

            return message.Contains($"@{id}");
        }

        public static bool MentionsBot(string message)
        {
            return MentionsId(message, Configuration.Instance.TelegramBotId);
        }
    }
}
