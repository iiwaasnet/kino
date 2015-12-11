# Kino - framework for building Actor-like networks

(Project is in development)

[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
[![NuGet version](https://badge.fury.io/nu/kino.svg)](https://badge.fury.io/nu/kino)
[![NuGet version](https://badge.fury.io/nu/kino.Rendezvous.svg)](https://badge.fury.io/nu/kino.Rendezvous)

Why?
-------------------------------------
Building a Web service is one of the ways to implement a component, accessible over the network. 
Following good design principles, we will create reusable, granular and relevant interface for our service. To be able to survive failures,
we would deploy it redundantly and make it accessible over the load-balancer at some well-known URL.

But what if the functionality we would like to expose is too small for a stand-alone service? What if we would like to scale out or extract for better reusability
just some parts of existing service?  At some point we might find ourselves managing dozens of new services, URLs and a lot of network configurations.
This still might be a proper design choice, but let's try something else...

Kino
-------------------------------------
**Kino** - is an *[Actor] (https://en.wikipedia.org/wiki/Actor_model)-like* framework for implementing and hosting components,
accessible over the network. In other words, **kino** allows to build *networks* of components.

Explain it!
-------------------------------------
If you are interested, please, read [wiki](https://github.com/iiwaasnet/kino/wiki) for details about the framework, ask related questions or suggest features in chat [![Join the chat at https://gitter.im/iiwaasnet/kino](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/iiwaasnet/kino?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
