# Kino - framework for building Actor-like networks

(Project is in development)

[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
[![NuGet version](https://badge.fury.io/nu/kino.svg)](https://badge.fury.io/nu/kino)
[![NuGet version](https://badge.fury.io/nu/kino.Rendezvous.svg)](https://badge.fury.io/nu/kino.Rendezvous)

Kino
------------------------
Building a Web service is one of the ways to implement a component, accessible over the network. But what if the functionality we would like to expose is too small for a stand-alone service?  Although, WS might still be a proper design choice, let's try something else...

**Kino** is an *[Actor] (https://en.wikipedia.org/wiki/Actor_model)-like* framework for implementing and hosting components, accessible over the network. In other words, **kino** allows to build *networks* of components. It is a foundation for your applications based on Microservices architecture, but can be small enough to be used just within one process.

Fault-tolerance and load scale-out by redundant deployment of actors, possibility to broadcast messages – everything without additional infrastructure dependencies. Rendezvous service provides actors auto-discovery and reduces amount of required configuration.

Platform requirements: Windows with .NET 4.6+.

Tell me more!
-------------------------------------
If you are interested in the project, please, read [wiki](https://github.com/iiwaasnet/kino/wiki/Introduction) for details about the framework, ask related questions or suggest features in chat! [![Join the chat at https://gitter.im/iiwaasnet/kino](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/iiwaasnet/kino?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)


* [Introduction](https://github.com/iiwaasnet/kino/wiki)
* [Messages](https://github.com/iiwaasnet/kino/wiki/Messages)
* [Actors](https://github.com/iiwaasnet/kino/wiki/Actors)
* [ActorHost](https://github.com/iiwaasnet/kino/wiki/ActorHost)
* [MessageHub](https://github.com/iiwaasnet/kino/wiki/MessageHub)
* [MessageRouter](https://github.com/iiwaasnet/kino/wiki/MessageRouter)
* [Rendezvous](https://github.com/iiwaasnet/kino/wiki/Rendezvous)

Powered by **[NetMQ](https://github.com/zeromq/netmq)**
