using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace UKLepraBotFaaS
{
    public class Reaction
    {
        public List<string> Triggers { get; set; } = new List<string>();
        public List<Reply> Replies { get; set; } = new List<Reply>();
        public bool IsAlwaysReply { get; set; }
        public bool IsMentionReply { get; set; }
    }

    public class ReactionsList
    {
        public List<Reaction> Items { get; set; }
    }

    public class Reply
    {
        public string Text { get; set; }
        public string Sticker { get; set; }
    }

    public class ChatSettings
    {
        public Dictionary<string, Tuple<int, int>> DelaySettings { get; set; } = new Dictionary<string, Tuple<int, int>>();
        public Dictionary<string, int> Delay { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, bool> State { get; set; } = new Dictionary<string, bool>();
    }

    public class BoyanEntity:TableEntity
    {
        public string Url { get; set; }
        public string Keyword { get; set; }
        public string MessageId { get; set;}
        public string ChatId { get;set;}
    }
}
