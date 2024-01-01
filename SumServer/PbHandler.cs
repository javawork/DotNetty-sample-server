using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Pbsum;


namespace SumServer
{
    public class PbHandler : ChannelHandlerAdapter
    {
        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            //Console.WriteLine("PbHandler.ChannelActive");
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
            //Console.WriteLine("PbHandler.ChannelInactive");
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            
            if (message is IByteBuffer buffer)
            {
                var code = (Pbsum.MessageCode)buffer.ReadByte();
                //Console.WriteLine($"Received from client: {code}");
                switch (code)
                {
                    case Pbsum.MessageCode.Ping:
                        HandlePing(context, buffer);
                        break;
                    default:
                        buffer.ResetReaderIndex();
                        SendPong(context, 0);
                        context.FireChannelRead(buffer);
                        break;
                }
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }

        protected void SendPong(IChannelHandlerContext context, long tick)
        {
            var ping = new Pbsum.Pong()
            {
                ClientTick = tick,
            };
            var pbArr = ping.ToByteArray();

            var writeBuffer = Unpooled.Buffer(1024);
            writeBuffer.WriteByte((int)Pbsum.MessageCode.Pong);
            writeBuffer.WriteBytes(pbArr);
            context.WriteAndFlushAsync(writeBuffer);
        }

        protected void HandlePing(IChannelHandlerContext context, IByteBuffer buffer)
        {
            var ping = _pingParser.ParseFrom(buffer.Array, buffer.ArrayOffset + buffer.ReaderIndex, buffer.ReadableBytes);
            SendPong(context, ping.ClientTick);
        }

        private readonly MessageParser<Pbsum.Ping> _pingParser = new(() => new Pbsum.Ping());
    }
}
