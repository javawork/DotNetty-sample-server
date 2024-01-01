using DotNetty.Codecs;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Libuv;

namespace SumServer
{
    class Program
    {
        private static readonly int Port = 4242;
        static async Task RunServerAsync()
        {
            var dispatcher = new DispatcherEventLoopGroup();
            IEventLoopGroup bossGroup = dispatcher;
            IEventLoopGroup workerGroup = new WorkerEventLoopGroup(dispatcher);

            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap.Group(bossGroup, workerGroup);

                var sumHandler = new SumHandler();
                bootstrap.Channel<TcpServerChannel>();
                bootstrap
                    .Option(ChannelOption.SoBacklog, 100)
                    .Handler(new LoggingHandler("SRV-LSTN"))
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast(new LoggingHandler("SRV-CONN"));

                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));
                        pipeline.AddLast("pb", new PbHandler());
                        pipeline.AddLast("sum", sumHandler);

                    }));

                IChannel boundChannel = await bootstrap.BindAsync(Port);

                Console.ReadLine();

                await boundChannel.CloseAsync();
            }
            finally
            {
                await Task.WhenAll(
                    bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        public static void Main() => RunServerAsync().Wait();

    }
}