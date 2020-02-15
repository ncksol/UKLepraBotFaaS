using System;

namespace UKLepraBotFaaS
{
    public class Configuration
    {
        private static Configuration _instance;

        private Configuration()
        {
            TelegramBotId = GetEnvironmentVariable("TelegramBotId");
            MasterId = GetEnvironmentVariable("MasterId");
            AdminIds = GetEnvironmentVariable("AdminIds");
            SecretKey = GetEnvironmentVariable("SecretKey");
        }

        public static Configuration Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Configuration();

                return _instance;
            }
        }
        public string TelegramBotId { get; private set; }
        public string MasterId {get; private set; }
        public string AdminIds { get; private set; }
        public string SecretKey { get; private set; }

        private DateTimeOffset? _startupTime = null;
        public DateTimeOffset? StartupTime
        {
            get => _startupTime;
            set
            {
                if (_startupTime == null)
                    _startupTime = value;
            }
        }

        private static string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }
}
