[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
[![NuGet version](https://badge.fury.io/nu/kino.svg)](https://badge.fury.io/nu/kino)
[![NuGet version](https://badge.fury.io/nu/kino.Rendezvous.svg)](https://badge.fury.io/nu/kino.Rendezvous)

# Kino - framework for building Actor-like networks

[![Join the chat at https://gitter.im/iiwaasnet/kino](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/iiwaasnet/kino?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
*(Project is in development)*

Building a Web service is one of the ways to implement a component, accessible over the network. 
Following good design principles, we will create reusable, granular and relevant interface for our service. To be able to survive failures,
we would deploy it redundantly and make it accessible over the load-balancer at some well-known URL.

But what if the functionality we would like to expose is too small for a stand-alone service? What if we would like to scale out or extract for better reusability
just some parts of existing service?  At some point we might find ourselves managing dozens of new services, URLs and a lot of network configurations.
This still might be a proper design choice, but let's try something else...

**Kino** - is an *[Actor] (https://en.wikipedia.org/wiki/Actor_model)-like* framework for implementing and hosting components,
accessible over the network. In other words, **kino** allows to build *networks* of components.
Although, the framework does not implement classical Actor Model, we will still call our components as actors.

An **Actor** contains implementation, some methods, which others can invoke by sending corresponding **Message**.
You don't have to know address of an actor to send a message. The only thing needed - is the message itself. Message **Identity** (or type) and **Version** are used to find concrete actor and 
invoke its method. That's it. You send a message *somewhere* and it is magically routed to a proper actor's method for you.


![Actors](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Actors.png)


For this magic to work we need someone, who will create the mapping between messages and actors' methods.
In **kino** this is done by the **ActorHost**.  During registration ActorHost queries actor's interface, tuples of method and
message type, and builds the **ActorHandlerMap** table. This mapping table is used to find corresponding handling method for each message
from the incoming message queue.

![ActorHost](https://cdn.rawgit.com/iiwaasnet/kino/master/img/ActorHost.png)


This looks good but still far from being a network of actors. We need to scale in number of actors and physical nodes.
We do it with the help of **MessageRouter**. ActorHost contains registration information, necessary to perform *in-proc* routing
of the messages. MessageRouter, in its turn, makes this registration information available for *out-of-proc* message routing. Let's take a look how this is achieved.

Message passing between core **kino** components (ActorHosts, MessageRouters and some others) is done over the **sockets**.
Along with URL, every *receiving* socket has globally unique socket **Identity** assigned. This socket identity together with message identity and message version
are used for message routing.


![MessageRouter](https://cdn.rawgit.com/iiwaasnet/kino/master/img/MessageRouter.png)


During actor registration process, after ActorHost has added corresponding entries with message identities and method references to ActorHandlerMap table,
it sends the same registration information to MessageRouter, replacing method references with the identity of its *receiving* socket.
MessageRouter stores this registration into **InternalRoutingTable**. MessageRouter can also connect to other MessageRouter(s) over
the **scale out socket** (for simplicity's sake, receiving and sending scale out sockets shown as one) and exchange information about the registrations
they have in their InternalRoutingTable. But this time, MessageRouter replaces socket identities of ActorHosts with its own identity of the *scale out socket*.
When other MessageRouter receives such a registration information, it stores it into it's **ExternalRoutingTable**. This is how in **kino** network everyone knows everything.

Now, when MessageRouter receives a message, either via local or scale out socket, it looks up InternalRoutingTable by *message identity* to find a
*socket identity* of an ActorHost, which is able to process the message. If there is an entry, incoming message is routed to ActorHost socket. 
ActorHost picks up the message, does a lookup in its *ActorHandlerMap* for a handling method and passes the message to a corresponding actor.

If, nevertheless, MessageRouter doesn't find any entry in its InternalRoutingTable, it does a lookup in *ExternalRoutingTable*. The socket identity, if found,
in this case points to another MessageRouter, to which then the message is finally forwarded for processing.

It is also worth mentioning, that the same actors can be hosted redundantly within one network. In this case, messages will be fairly distributed among them.

Now, we know how actors are registered within ActorHosts and how MessageRouters can exchange registrations and forward messages to each other.
Let's take a look how MessageRouters, eventually deployed on different nodes, can find each other in this big world.

**kino** provides a **Rendezvous** Service for sole MessageRouters. *Rendezvous* Service endpoint is the single well-known endpoint,
which should be configured on every MessageRouter connected to the same **kino** network. No need to share or configure any other URLs.


![Rendezvous](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Rendezvous.png)


Rendezvous Service is that glue, which keeps all parts together: it forwards registration information from newly connected routers to all
members of the network, sends PINGs and broadcasts PONGs as a part of health check process. Because of its importance, Rendezvous service
is usually deployed on a *cluster* of servers, so that it can survive hardware failures. Nevertheless, even if the Rendezvous
cluster dies (or stops because of deployment), **kino** network still continues to operate, except that network configuration changes will
not be propagated until the cluster is online again.

Actor's method, that was invoked by ActorHost to process incoming message, may respond with a new message. This new message will be traveling over the network until
it reaches other actor and so on. If an actor can only create a response message, how can we create initial request message?
**MessageHub** is used to send a message into **kino** network. Depending on your needs, you may send either one-way message,
or specify a *callback point*. To explain what the callback point is, let's talk a bit about message flows.

When we build an application, we try to group relevant code into some components. These components have well-defined interface and responsibilities.
At high level, code of the application looks like a sequence of calls to some components with expected type of return value.
If we replace components with actors, which are sending and receiving messages, we may say that we have a message flow, which implements desired behavior
of the application. Return value of expected type - this is a callback point in message flow. When building application with **kino**, we design a message flow,
which we expect to finish at some point in time with predefined result - message. So, callback point, is nothing else than identity of the message, which should be routed
back to initiator of the flow.

In nutshell, caller sends initial flow message via MessageHub into **kino** network and defines a callback point. Dozens of new messages may be created during this flow,
traveling over different nodes, but as soon as somewhere someone creates a message with the identity, defined in the callback, this message will be immediately routed
back to the caller. Voila, you've got your return value!

![Callback](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Callback.png)


## Example
We've got a very important task to find out the cities with highest and lowest temperature.

How we are going to do that:
  * get list of all cities
  * retrieve current weather for each city from the list
  * collect all weather data and find cities with highest and lowest temperature
  
Let's define logical flow of the messages for this task:
```
In Message                        Action                                  Out Message

                                   Send                                -> {List of Cities}
{List of Cities}                -> For each City Send                  -> {Weather Request for a City}
{Weather Request for a City}    -> Get Current Weather for a City      -> [{City Weather}, {Log City Weather}]
{Log City Weather}              -> Log City Weather
{City Weather}                  -> Aggregate City Weather              -> {Cities with Highest and Lowest Temperature}
```

![pic]( https://cdn.rawgit.com/iiwaasnet/kino/master/img/Weather.png)

Nevertheless, there are some questions to this design:
  * how does **WeatherAggregator** actor group all messages together for each client request?
  * how does it know, when the last message arrives?
  * if we host several instances of **WeatherAggregator** actor, which one will be responsible for grouping up final result?

The first problem is solved by using **CorrelationId**, which is generated once for initial message and then copied onto every other message within the same flow. Additionally, in every **CityWeather** message we provide the **Total Number** of all messages to be expected. 
Now, we can use combination of **CorrelationId** and **Total Number** properties to aggregate all the messages of current flow.
To solve the last problem, we use some central storage, where all instances of **CityWeather** actor save intermediate results:
  * what are the currently known highest and lowest temperatures;
  * how many messages from the **Total Number** of expected messages are already processed.

Instance, which updates the shared storage with the last message of the flow, will send the resulting message for the client.

Now, some code. 
### Client
[WeatherRequestScheduler.cs](https://github.com/iiwaasnet/weather/blob/master/src/weather.stat/Scheduler/WeatherRequestScheduler.cs)
``` csharp
// Read list of cities
var cities = (await GetCityList()).Select(c => c.Name);

// Create initial message with CorrelationId assigned to it
var message = Message.CreateFlowStartMessage(new RequestWeatherHighlightsMessage
                                             {
                                                 Cities = cities
                                             });
message.TraceOptions = MessageTraceOptions.Routing;

// Define callback with result message
var callback = CallbackPoint.Create<WeatherHighlightsMessage>();

// Send message into actors network and specify wait timeout
var promise = messageHub.EnqueueRequest(message, callback, TimeSpan.FromMinutes(1));

// Wait for result
var weatherAggregates = (await promise.GetResponse()).GetPayload<WeatherHighlightsMessage>();

// Print result
WriteLine($"Lowest T: {weatherAggregates.LowestTemperature.CityName} {weatherAggregates.LowestTemperature.Temperature} C " +
          $"Highest T: {weatherAggregates.HighestTemperature.CityName} {weatherAggregates.HighestTemperature.Temperature} C. ");

```
  
### Sample Message declaration
```csharp
[ProtoContract]
public class RequestWeatherHighlightsMessage: Payload
{
    private static byte[] MessageIdentity = "REQWHIGHLT".GetBytes();
    private static byte[] MessageVersion = "1.0".GetBytes();

    [ProtoMember(1)]
    public IEnumerable<string> Cities { get; set; }

    public override byte[] Version => MessageVersion;
    public override byte[] Identity => MessageIdentity;
}
```
Default serializer for all messages, other than Exception, [protobuf-net](https://github.com/mgravell/protobuf-net).

## Actor
Here is the code for one of the actors, responsible for getting current weather in a city:
```csharp
public class WeatherCollector : IActor
{
    // Method, which handles RequestCityWeatherMessage message
    [MessageHandlerDefinition(typeof (RequestCityWeatherMessage))]
    private async Task<IActorResult> Handler(IMessage message)
    {
        // Get message payload
        var request = message.GetPayload<RequestCityWeatherMessage>();
        
        // Request weather from external service and wait for result
        var weather = await GetCityWeather(request.CityName);        
        var cityWeather = new CityWeather
                          {
                              CityName = request.CityName,
                              Temperature = weather?.Main?.Temp
                          };
                          
        // Create CityWeatherMessage message with current weather in the city
        // Forward TotalCityCount to the next actor
        var response = Message.Create(new CityWeatherMessage
                                      {
                                          Weather = cityWeather,
                                          TotalCityCount = request.TotalCityCount
                                      });
                                      
        // Create a message to be processed by a logging actor
        var log = Message.Create(new LogCityWeatherMessage
                                 {
                                     Weather = cityWeather
                                 });
                                 
        // Return response messages
        return new ActorResult(response, log);
    }
}
```

Complete solution for Weather example could be found [here](https://github.com/iiwaasnet/weather).
Another example, which uses Rendezvous service, could be found in [Samples](https://github.com/iiwaasnet/kino/tree/master/src/Samples) folder.



Powered by **[NetMQ](https://github.com/zeromq/netmq)**
