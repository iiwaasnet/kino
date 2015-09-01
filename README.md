[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
# Kino - framework for building Actors networks
*(Project is in development)*
## In a nutshell

A kino **Actor** exposes some piece of functionality accessible over the network by declaring the incoming message type it is able to process. 
Actor receives an input message and may send one or more output messages, either synchronously or asynchronously. Actors are hosted by an ActorHost.


All Actors, hosted by the same **ActorHost**, share same "mailbox" thread, i.e. until previously fetched message is not passed to an Actor, the next one is waiting in the queue.
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


**MessageHub** is one of the ways to send messages into Actors network. It a *starting point of the flow*. First message sent from MessageHub gets CorrelationId assigned, which is then copied to any other messages, created during the message flow. It is possible to create a *callback point*, which allows to route back to the caller the message, on which the callback is defined. Thus, clients may emulate a synchronous call, waiting for the callback to be resolved with the resulting message or exception.


## Some details
All communication between Actors happens by message passing. Messages are distinguished by **Identity**, i.e. string representation of message type. There are two distribution patterns for a message: unicast and broadcast. Messages of the latest type are sent to **all** registered Actors, which can handle this type of message.

[NetMQ](https://github.com/zeromq/netmq) is selected as a transport.
