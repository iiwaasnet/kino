[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
[![NuGet kino.Client](https://badge.fury.io/nu/kino.Client.svg)](http://badge.fury.io/nu/kino.Client)
[![NuGet kino.Actors](https://badge.fury.io/nu/kino.Actors.svg)](http://badge.fury.io/nu/kino.Actors)
[![NuGet kino.Rendezvous](https://badge.fury.io/nu/kino.Rendezvous.svg)](http://badge.fury.io/nu/kino.Rendezvous)

# Kino - framework for building Actor-like networks

[![Join the chat at https://gitter.im/iiwaasnet/kino](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/iiwaasnet/kino?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
*(Project is in development)*


Building a Web service is one of the ways to implement component, accessible over the network. 
Following good design principles, we will create reusable, granular and relevant interface for our service. To be able to survive failures,
we would deploy it redundantly and make it accessible over the load-balancer at some well-known URL.

But what if the functionality we would like to expose is too small for a stand-alone service? What if we would like to scale out or extract for better reusability
just some parts of already existing service?  Do we want to end up with dozens of new services, URLs and a lot of network configurations?
This still might be a proper design choice, but let's try something else...

**Kino** - is an *[Actor] (https://en.wikipedia.org/wiki/Actor_model)-like* framework for implementing and hosting components,
accessible over the network. In other words, **kino** allows to build networks of components.
Although, the framework does not implement classical Actor Model, we will still call our components as actors.

So, an **Actor** contains implementation, i.e. methods, which others can invoke by sending corresponding **Message**.
You don't have to know address of an actor, the only thing needed - is the message itself. Message **Identity** (or type) and **Version** are used to find concrete actor and 
invoke it's method. That's it. You send a message *somewhere* and it is magically routed to a proper actor's method for you.


![Actors](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Actors.png)


For this magic to work we need someone, who will create the mapping between messages and actors' methods.
In **kino** this is done by the **ActorHost**.  When actor is *registered* at ActorHost, it *queries* actor's interface, list of methods and
message types they can accept, and builds the **ActorHandlerMap** table. This mapping table is then later used to find corresponding method for each message
in the incoming messages queue.

![ActorHost](https://cdn.rawgit.com/iiwaasnet/kino/master/img/ActorHost.png)


Earlier, it was written that with **kino** you can build networks of components. But until now, we saw just an ActorHost which holds *local references* to methods.
How do we build up a network of actors? With the help of **MessageRouter**. ActorHost contains registration information, necessary to perform *in-proc* routing
of the messages. MessageRouter, in it's turn, makes this registration information available for *out-of-proc* message routing.
Let's take a look how this is achieved.

![MessageRouter](https://cdn.rawgit.com/iiwaasnet/kino/master/img/MessageRouter.png)


Message passing between core **kino** components (ActorHosts, MessageRouters and some others) is done over the **sockets**.
Along with URL, every *receiving* socket has globally unique socket **Identity** assigned. This socket identity together with message identity and message version
are used for message routing.

During actor registration process, after ActorHost has added corresponding entries with message identities and method references to it's mapping table,
it sends the same registration information to MessageRouter, replacing methods references with the identity of it's *receiving* socket.
MessageRouter stores this registration into it's **InternalRoutingTable**. MessageRouter as well can connect to other MessageRouter(s) over
the **scale out socket**s (for simplicity, receiving and sending scale out sockets shown as one) and exchange information about the registrations,
they have in their InternalRoutingTables. But this time, MessageRouter replaces socket identities of ActorHosts with it's own identity of the *scale out socket*.
When other MessageRouter receives such a registration information, it stores it into it's **ExternalRoutingTable**. This is how in **kino** network everyone knows everything.

Now, when MessageRouter receives a message, either via local or scale out socket, it looks up by *message identity* it's InternalRoutingTable to find a
*socket identity* of an ActorHost, which is able to process the message. If there is an entry, incoming message is routed to ActorHost socket. 
ActorHost picks up the message, does a lookup in it's *ActorHandlerMap* for a handling method and passes the message to corresponding actor.

If, nevertheless, MessageRouter doesn't find any entry in it's InternalRoutingTable, it does a lookup in *ExternalRoutingTable*. The socket identity, if found,
in this case points to another MessageRouter, to which then the message is finally forwarded for processing.

Now, we know how actors are registered within ActorHosts and how MessageRouters can exchange registrations and forward messages to each other.
Let's take a look how MessageRouters, eventually deployed on different nodes, can find each other in this big world. **kino** provides a **Rendezvous** Service
for sole MessageRouters. *Rendezvous* Service endpoint is the **single** well-known endpoint, which should be configured on every MessageRouter connected
to the same **kino** network. No need to share or configure any other URLs.


![Rendezvous](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Rendezvous.png)


Rendezvous Service is that glue, which keeps all parts together: it forwards registration information from newly connected routers to all
members of the network, sends PINGs and broadcasts PONGs as a part of health check process. Since it's so important, the Rendezvous service
is usually deployed on a *cluster* of servers, so that it can survive hardware failures. Nevertheless, even if the Rendezvous
cluster dies (or stopped because of deployment), **kino** network still continues to operate, except that network configuration changes will
not be propagated until the cluster is online again.


**MessageHub** is one of the ways to send messages into Actors network. It is a *starting point of the flow*. First message sent from MessageHub gets CorrelationId assigned, which is then copied to any other message, created during the message flow. It is possible to create a *callback point*, which is defined by message type and caller address. 
Whenever an Actor responds with the message, which type corresponds to the one registered in the callback, it is immediately routed back to the address in the callback point.
Thus, clients may emulate synchronous calls, waiting for the callback to be resolved. Callback may return back a message or an exception, whatever happens first.

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
