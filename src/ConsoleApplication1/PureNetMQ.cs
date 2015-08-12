using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace ConsoleApplication1
{
  public class PureNetMQ
  {
    private static readonly byte[] ServerIdentity = {1, 2, 3};
    private static readonly byte[] ClientIdentity = {1, 2, 4};
    private static readonly string Address = "tcp://127.0.0.1:5000";

    private static void M(string[] args)
    {
      var cancellationTokenSource = new CancellationTokenSource();
      Task.Factory.StartNew(_ => RunServer(cancellationTokenSource.Token), cancellationTokenSource.Token, TaskCreationOptions.LongRunning);
      Thread.Sleep(TimeSpan.FromSeconds(2));

      RunClient();

      Console.WriteLine("Done");
      Console.ReadLine();
      cancellationTokenSource.Cancel(true);
    }

    private static void RunClient()
    {
      using (var context = NetMQContext.Create())
      {
        using (NetMQSocket socket = context.CreateRouterSocket())
        {
          //socket.Options.Identity = Guid.NewGuid().ToByteArray();
          //socket.Options.RouterMandatory = true;
          socket.Options.Linger = TimeSpan.Zero;

          socket.Connect(Address);

          Thread.Sleep(TimeSpan.FromSeconds(2));

          socket.SendMore(ServerIdentity);
          socket.SendMore(Encoding.UTF8.GetBytes(""));
          socket.Send(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
          Console.WriteLine("Message sent...");

          socket.Disconnect(Address);
          Thread.Sleep(TimeSpan.FromSeconds(5));
          Console.WriteLine("Disconnected");

          //socket.Options.Identity = Guid.NewGuid().ToByteArray();
          socket.Connect(Address);
          Thread.Sleep(TimeSpan.FromSeconds(5));
          Console.WriteLine("Reconnected");


          socket.SendMore(ServerIdentity);
          socket.SendMore(Encoding.UTF8.GetBytes(""));
          socket.Send(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
          Console.WriteLine("Message sent...");

          Thread.Sleep(TimeSpan.FromSeconds(5));
        }
      }
    }

    private static void RunServer(CancellationToken token)
    {
      using (var context = NetMQContext.Create())
      {
        using (var socket = context.CreateRouterSocket())
        {
          socket.Options.Identity = ServerIdentity;
          //socket.Options.RouterMandatory = true;
          socket.Options.Linger = TimeSpan.Zero;

          socket.Bind(Address);

          while (!token.IsCancellationRequested)
          {
            var message = socket.ReceiveMessage();

            for (var i = 2; i < message.FrameCount; i++)
            {
              Console.WriteLine("Message: {0}", Encoding.UTF8.GetString(message[i].Buffer));
            }
          }
        }
      }
    }
  }
}