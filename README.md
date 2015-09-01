[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
# Kino - framework for building Actors networks

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
