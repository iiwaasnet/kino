//using System;

//namespace Console.Messages
//{
//    public class TypedMessage<T> : Message
//        where T : class
//    {
//        protected TypedMessage(IMessage message)
//        {
//            Body = message.Body;
//            Distribution = message.Distribution;
//            Identity = message.Identity;
//            Version= message.Version;
//            TTL = message.TTL;
//        }

//        protected TypedMessage(T payload, string messageIdentity)
//        {
//            Body = Serialize(payload);
//            Identity = messageIdentity;
//            Distribution = DistributionPattern.Unicast;
//            Version = MessagesVersion;
//            TTL = TimeSpan.Zero;
//        }
//    }
//}