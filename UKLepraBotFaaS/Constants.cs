using System;
using System.Collections.Generic;
using System.Text;

namespace UKLepraBotFaaS
{
    public static class Constants
    {
        public const string OutputQueueName = "output";
        public const string ChatMembersUpdateQueueName = "chatmembersupdate";
        public const string HuifyQueueName = "huify";
        public const string SettingsQueueName = "settings";
        public const string GoogleItQueueName = "googleit";
        public const string ReactionsQueueName = "reactions";
        public const string ReactionsBlobPath = "data/reactions.json";
        public const string ChatSettingsBlobPath = "data/chatsettings.json";
        public const string DataBlobPath = "data";

        public static string MemberLeftSticker = "CAADAgADXgEAAhmGAwABgntLLoS0m94C";
    }
}
