using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Google.Protobuf;

namespace SumClient
{

    public class PbHandler : ChannelHandlerAdapter
    {
        readonly IByteBuffer initialMessage;
        public PbHandler()
        {
            this.initialMessage = Unpooled.Buffer(1024);

        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            Console.WriteLine("ChannelActive");

            SendAdd(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
            Console.WriteLine("ChannelInactive");
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer buffer)
            {
                var code = buffer.ReadByte();
                //Console.WriteLine($"Received from server: {code}");
                //var ping = _parser.ParseFrom(buffer.Array, buffer.ArrayOffset + buffer.ReaderIndex, buffer.ReadableBytes);
            }

            ++_count;

            if ( _count == 500)
            {
                SendLocalSum(context);
            }
            else if (_count < 500)
            {
                SendAdd(context);
            }

        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }

        private void SendPing(IChannelHandlerContext context)
        {
            var ping = new Pbsum.Ping()
            {
                ClientTick = Environment.TickCount64,
            };
            var pbArr = ping.ToByteArray();
            var writeBuffer = Unpooled.Buffer(1024);
            writeBuffer.WriteByte((int)Pbsum.MessageCode.Ping);
            writeBuffer.WriteBytes(pbArr);

            context.WriteAndFlushAsync(writeBuffer);
        }

        private void SendAdd(IChannelHandlerContext context)
        {
            var msg = new Pbsum.Add()
            {
                Num = _rnd.Next(100)
            };

            _localSum += msg.Num;
            var pbArr = msg.ToByteArray();
            var writeBuffer = Unpooled.Buffer(32);
            writeBuffer.WriteByte((int)Pbsum.MessageCode.Add);
            writeBuffer.WriteBytes(pbArr);
            context.WriteAndFlushAsync(writeBuffer);
            Console.WriteLine($"Add: {msg.Num}");

        }

        private void SendLocalSum(IChannelHandlerContext context)
        {
            var msg = new Pbsum.LocalSum()
            {
                Sum = _localSum
            };

            var pbArr = msg.ToByteArray();
            var writeBuffer = Unpooled.Buffer(32);
            writeBuffer.WriteByte((int)Pbsum.MessageCode.LocalSum);
            writeBuffer.WriteBytes(pbArr);
            context.WriteAndFlushAsync(writeBuffer);
        }

        private int _count = 0;
        private int _localSum = 0;
        private readonly Random _rnd = new Random();
        private readonly MessageParser<Pbsum.Pong> _parser = new(() => new Pbsum.Pong());
    }
}
