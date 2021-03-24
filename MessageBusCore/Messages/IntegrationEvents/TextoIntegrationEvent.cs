
namespace MessageBusCore.Messages.IntegrationEvents
{
    public class TextoIntegrationEvent : IntegrationEvent
    {
        public TextoIntegrationEvent(string text)
        {
            Text = text;
        }

        public string Text { get; private set; }
    }
}
