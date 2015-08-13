using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using rawf.Actors;
using rawf.Connectivity;
using rawf.Messaging;
using Server.Messages;

namespace Server
{
    public class Actor : IActor
    {
        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return new MessageMap
                         {
                             Handler = StartProcess,
                             Message = new MessageDefinition
                                       {
                                           Identity = HelloMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       }
                         };
        }

        private async Task<IMessage> StartProcess(IMessage message)
        {
            var hello = message.GetPayload<HelloMessage>();
            //System.Console.WriteLine(hello.Greeting);

            //return await Task.Delay(0)
            //                 .ContinueWith(_ => Message.Create(new EhlloMessage
            //                                                   {
            //                                                       Ehllo = new string(hello.Greeting.Reverse().ToArray())
            //                                                   },
            //                                                   EhlloMessage.MessageIdentity))
            //                                                   .ConfigureAwait(false);

            //throw new Exception("Bla!");

            Thread.Sleep(50000);

            return Message.Create(new EhlloMessage
            {
                Ehllo = new string(hello.Greeting.Reverse().ToArray())
            },
                                  EhlloMessage.MessageIdentity);
        }
    }
}