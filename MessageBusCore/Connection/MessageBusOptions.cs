namespace MessageBusCore
{
    public class MessageBusOptions
    {
        public string ExchangeName { get; set; }
        public string ExchangeType { get; set; }
        public string QueueDefault { get; set; }

        public string HostName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
