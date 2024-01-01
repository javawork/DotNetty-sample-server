using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using Pbsum;

namespace SumServer
{
    public class SumHandler : ChannelHandlerAdapter
    {
        public override void ChannelActive(IChannelHandlerContext context)
        {
            ++_channelCount;
            base.ChannelInactive(context);
            //Console.WriteLine("SumHandler.ChannelActive");
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            --_channelCount;
            base.ChannelInactive(context);
            //Console.WriteLine("SumHandler.ChannelInactive");
            if (_channelCount == 0)
            {
                _globalSum = 0;
                _localSum = 0;
                Console.WriteLine("Reset Sums");
            }
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer buffer)
            {
                var code = (Pbsum.MessageCode)buffer.ReadByte();
                //Console.WriteLine($"SumHandler: {code}, {Thread.CurrentThread.ManagedThreadId}");
                switch (code)
                {
                    case MessageCode.Add:
                        HandleAdd(context, buffer);
                        break;
                    case MessageCode.LocalSum:
                        HandleLocalSum(context, buffer);
                        break;
                }
            }
        }

        protected void HandleAdd(IChannelHandlerContext context, IByteBuffer buffer)
        {
            var msg = _addParser.ParseFrom(buffer.Array, buffer.ArrayOffset + buffer.ReaderIndex,
                buffer.ReadableBytes);
            if (msg == null)
                return;
            
            if (Interlocked.CompareExchange(ref _acquired, 1, 0) == 0)
            {
                _globalSum += msg.Num;

                while (_queue.TryDequeue(out var num))
                {
                    Console.WriteLine($"Dequeue {num}");
                    _globalSum += num;
                }

                Interlocked.Exchange(ref _acquired, 0);
            }
            else
            {
                _queue.Enqueue(msg.Num);
                Console.WriteLine($"Enqueue {msg.Num}");
            }
        }

        protected void HandleLocalSum(IChannelHandlerContext context, IByteBuffer buffer)
        {
            var msg = _localSumParser.ParseFrom(buffer.Array, buffer.ArrayOffset + buffer.ReaderIndex,
                buffer.ReadableBytes);
            if (msg == null)
                return;

            Interlocked.Add(ref _localSum, msg.Sum);
            Console.WriteLine($"GlobalSum: {_globalSum}, LocalSum: {_localSum}");
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            Console.WriteLine($"{_queue.Count}");
            context.CloseAsync();
        }

        public override bool IsSharable => true;
        private int _acquired = 0;
        private readonly ConcurrentQueue<int> _queue = new ConcurrentQueue<int>();
        private readonly MessageParser<Pbsum.Add> _addParser = new(() => new Pbsum.Add());
        private readonly MessageParser<Pbsum.LocalSum> _localSumParser = new(() => new Pbsum.LocalSum());
        private int _globalSum = 0;
        private int _localSum = 0;
        private int _channelCount = 0;

    }
}