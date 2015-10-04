[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
[![NuGet kino.Client](https://badge.fury.io/nu/kino.Client.svg)](http://badge.fury.io/nu/kino.Client)
[![NuGet kino.Actors](https://badge.fury.io/nu/kino.Actors.svg)](http://badge.fury.io/nu/kino.Actors)
[![NuGet kino.Rendezvous](https://badge.fury.io/nu/kino.Rendezvous.svg)](http://badge.fury.io/nu/kino.Rendezvous)

# Kino - framework for building actors networks
*(Project is in development)*
## In a nutshell

In *kino*, an **Actor** registers itself by declaring message types it can process. There is no hierarchy of actors, as well as no logical addresses assigned to them.
Actor's message handling method receives one input message and may send one or more output messages, either synchronously or asynchronously. It may produce no output as well.
Actors are hosted by an ActorHost.



**ActorHost** receives messages and calles corresponding Actor's handler based on the message type (and version). All Actors, hosted by the same **ActorHost**, share same receiving thread. 
This means that until previously fetched message is processed by an Actor, the next one will be waiting in the queue. ActorHost is a unit of in-proc scaling.
Every ActorHost connectes to a MessageRouter.



**MessageRouter** is responsible for:
  * registering all Actors, which are hosted by connected ActorHosts;
  * type-based message routing to locally connected Actors;
  * typed-based message routing to external, i.e. out-of-proc Actors, if non of the locally registered Actors is able to process a message.

In order to be able to discover other Actors, MessageRouter connects to Rendezvous server.



**Rendezvous** server is a well-known point, where all MessageRouters connect, building up an Actors network. 
Rendezvous server broadcasts:
  * MessageRouters' registration messages, announcing which type of messages locally registered Actors are able to process;
  * Ping message, to check nodes availability;
  * Pong response from all the registered nodes to all registered nodes.

Since Rendezvous server is a single point of failure, it is recommended to start several instances of the service on different nodes to build a fault-tolorent cluster.



**MessageHub** is one of the ways to send messages into Actors network. It is a *starting point of the flow*. First message sent from MessageHub gets CorrelationId assigned, 
which is then copied to any other message, created during the message flow. It is possible to create a *callback point*, which is defined by message type and caller address. 
Whenever an Actor responds with the message, which type corresponds to the one registered in the callback, it is immediatelly routed back to the address in the callback point.
Thus, clients may emulate synchronous calls, waiting for the callback to be resolved. Callback may return back a message or an exception, whatever happens first.


## Message declaration
```csharp
[ProtoContract]
public class HelloMessage : Payload
{
    public static readonly byte[] MessageIdentity = "HELLO".GetBytes();

    [ProtoMember(1)]
    public string Greeting { get; set; }
}
```
Default serializer for all messages, other than Exception, [protobuf-net](https://github.com/mgravell/protobuf-net).

## Sample Actor
```csharp
public class RevertStringActor : IActor
{
    public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
    {
        yield return new MessageHandlerDefinition
                     {
                         Handler = StartProcess,
                         Message = new MessageDefinition
                                   {
                                       Identity = HelloMessage.MessageIdentity,
                                       Version = Message.CurrentVersion
                                   }
                     };
    }

    private async Task<IActorResult> StartProcess(IMessage message)
    {
        var hello = message.GetPayload<HelloMessage>();

        return new ActorResult(Message.Create(new EhlloMessage
                              {
                                  Ehllo = new string(hello.Greeting.Reverse().ToArray())
                              },
                              EhlloMessage.MessageIdentity));
    }
}
```

## Sending a message with callback
```csharp
// ctor parameters are omitted for clarity
var messageRouter = new MessageRouter(...);
messageRouter.Start();

var clusterMonitor = new ClusterMonitor(...);
clusterMonitor.Start();

var messageHub = new MessageHub(...);
messageHub.Start();

var request = Message.CreateFlowStartMessage(new HelloMessage(), HelloMessage.MessageIdentity);
var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);
var promise = messageHub.EnqueueRequest(request, callbackPoint);
var response = promise.GetResponse().Result.GetPayload<EhlloMessage>();
```

## Starting Actors
```csharp
// ctor parameters are omitted for clarity
var messageRouter = new MessageRouter(...);
messageRouter.Start();

var clusterMonitor = new ClusterMonitor(...);
clusterMonitor.Start();

var actorHost = new ActorHost(...);
actorHost.Start();
foreach (IActor actor in GetAvailableActors())
{
    actorHost.AssignActor(actor);
}
```
For basic usage, please, check [Samples](https://github.com/iiwaasnet/kino/tree/master/src/Samples) folder.
Another [example](https://github.com/iiwaasnet/weather) of scaling out requests and grouping final result.


**Powered by: [NetMQ](https://github.com/zeromq/netmq)**
