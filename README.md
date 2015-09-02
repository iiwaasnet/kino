[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
# Kino - framework for building Actors networks
*(Project is in development)*
## In a nutshell

In *kino*, an **Actor** exposes a piece of functionality, accessible over the network, by declaring incoming message types it is able to process.
Actor receives an input message and may send one or more output messages, either synchronously or asynchronously. Actors are hosted by an ActorHost.


All Actors, hosted by the same **ActorHost**, share same receiving thread, i.e. until previously fetched message is not passed to an Actor, the next one is waiting in the queue.
ActorHost is a unit of in-proc scaling. Every ActorHost connectes to a MessageRouter.


**MessageRouter** is responsible for:
  * registering all Actors, which are hosted by connected ActorHosts;
  * type-based message routing to locally registered Actors;
  * typed-based message routing to external, i.e. out-of-proc Actors, if non of the locally registered Actors is able to process a message.

MessageRouter provides Actors connectivity between processes/nodes. MessageRouter connects to Rendezvous server.


**Rendezvous** server is a point, where all MessageRouters connect to build up the Actors network. 
Rendezvous server broadcasts:
  * MessageRouters' registration messages, announcing which type of messages Actors are able to process;
  * Ping to check nodes availability;
  * Pong response from all the registered nodes to all registered nodes.

Since Rendezvous server is a single point of failure, it is recommended to start several instances of the service on different nodes to build a fault-tolorent cluster.


**MessageHub** is one of the ways to send messages into Actors network. It a *starting point of the flow*. First message sent from MessageHub gets CorrelationId assigned, 
which is then copied to any other messages, created during the message flow. It is possible to create a *callback point*, which allows to route back to the caller the message, 
on which the callback is defined. Thus, clients may emulate a synchronous call, waiting for the callback to be resolved with the resulting message or exception.


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
Default serializer for all messages, other than Exception, - protobuf-net. Different serializer could be injected via protected contructor (?):
```csharp
protected Payload(IMessageSerializer messageSerializer)
```

## Sample Actor
```csharp
public class RevertStringActor : IActor
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
