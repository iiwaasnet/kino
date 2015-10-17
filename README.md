[![Build status](https://ci.appveyor.com/api/projects/status/khn5imataa5uw4oj?svg=true)](https://ci.appveyor.com/project/iiwaasnet/kino)
[![NuGet kino.Client](https://badge.fury.io/nu/kino.Client.svg)](http://badge.fury.io/nu/kino.Client)
[![NuGet kino.Actors](https://badge.fury.io/nu/kino.Actors.svg)](http://badge.fury.io/nu/kino.Actors)
[![NuGet kino.Rendezvous](https://badge.fury.io/nu/kino.Rendezvous.svg)](http://badge.fury.io/nu/kino.Rendezvous)

# Kino - framework for building Actor-like networks
*(Project is in development)*


![Actors](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Actors.png)

**kino** is an *[Actor] (https://en.wikipedia.org/wiki/Actor_model)-like* framework, built to allow scaling of Actors over the network with low efforts for configuration management. A *kino* **Actor** registers itself by declaring message type(s) it can process. There is no hierarchy of Actors, as well as no logical addresses assigned to them.
Actor's message handling method receives one input message and may send one or more output messages, either synchronously or asynchronously. It may produce no output as well.
Actors are hosted by an ActorHost.



**ActorHost** receives messages and calls corresponding Actor's handler based on the message type (and version). All Actors, hosted by the same **ActorHost**, share same receiving thread. This means that until previously fetched message is processed by an Actor, the next one will be waiting in the queue. ActorHost is a unit of in-proc scaling.

![ActorHost](https://cdn.rawgit.com/iiwaasnet/kino/master/img/ActorHost.png)

Every ActorHost connects to a MessageRouter.


**MessageRouter** is responsible for:
  * registering all Actors, which are hosted by connected ActorHosts;
  * type-based message routing to locally connected Actors;
  * typed-based message routing to external, i.e. out-of-proc Actors, if no locally registered Actors are able to process the message.

![MessageRouter](https://cdn.rawgit.com/iiwaasnet/kino/master/img/MessageRouter.png)

In order to be able to discover other Actors, MessageRouter connects to Rendezvous server.



**Rendezvous** server is a well-known point, where all MessageRouters connect, building up an Actors network. Since Rendezvous server is a single point of failure, it is recommended to start several instances of the service on different nodes to build up a *fault-tolerant cluster*. In this case, MessageRouter should be configured with endpoints of all Rendezvous servers from the cluster. On startup, Rendezvous synod [elects](http://www.xtreemfs.org/publications/flease_paper_ipdps.pdf) a Leader, which than starts to broadcast:
  * MessageRouters' registration messages, announcing which type of messages locally registered Actors are able to process;
  * Ping message, to check nodes availability;
  * Pong response from all the registered nodes to all registered nodes.

In case Rendezvous Leader changes, MessageRouter do a round-robin search among all configured endpoints to connect to a new Leader.  Dynamic reconfiguration of Rendezvous cluster is not supported. Nevertheless, the cluster can be stopped. In this case, although Actors will still exchange messages, configuration changes will not be propagated to all nodes of the network.

![Rendezvous](https://cdn.rawgit.com/iiwaasnet/kino/master/img/Rendezvous.png)


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
{Log City Weather}              -> Save City Weather
{City Weather}                  -> Aggregate City Weather              -> {Cities with Highest and Lowest Temperature}
```

![pic]( https://cdn.rawgit.com/iiwaasnet/kino/master/img/Weather.png)

Nevertheless, there are some questions to this design:
  * how does **WeatherAggregator** actor groups all messages together for each client request?
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
                                             },
                                             RequestWeatherHighlightsMessage.Identity);
message.TraceOptions = MessageTraceOptions.Routing;

// Define callback with result message
var callback = new CallbackPoint(WeatherHighlightsMessage.Identity);

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
    public static byte[] Identity = "REQWHIGHLT".GetBytes();

    [ProtoMember(1)]
    public IEnumerable<string> Cities { get; set; }
}
```
Default serializer for all messages, other than Exception, [protobuf-net](https://github.com/mgravell/protobuf-net).

## Actor
Here is the code for one of the actors, responsible for getting current weather in a city:
```csharp
public class WeatherCollector : IActor
{
    // Declare, which messages actor can process and bind them to handlers
    public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
    {
        yield return new MessageHandlerDefinition
        {
                         Message = new MessageDefinition
                                   {
                                       Identity = RequestCityWeatherMessage.Identity,
                                       Version = Message.CurrentVersion
                                   },
                         Handler = Handler
                     };
    }
    
    // Method, which handles RequestCityWeatherMessage message
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
                                      },
                                      CityWeatherMessage.Identity);
                                      
        // Create a message to be processed by a logging actor
        var log = Message.Create(new LogCityWeatherMessage
                                 {
                                     Weather = cityWeather
                                 },
                                 LogCityWeatherMessage.Identity);
                                 
        // Return response messages
        return new ActorResult(response, log);
    }
}
```

Complete solution for Weather example could be found [here](https://github.com/iiwaasnet/weather).

Another example, which uses Rendezvous service, could be found in [Samples](https://github.com/iiwaasnet/kino/tree/master/src/Samples) folder.



Powered by **[NetMQ](https://github.com/zeromq/netmq)**
