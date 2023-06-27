namespace YogaClassManager.Models
{
    public class Message
    {
        public Message(object parameter)
        {
            Parameter = parameter;
        }

        public object Parameter { get; }
    }
}
