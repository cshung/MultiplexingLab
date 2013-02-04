namespace Multiplexer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;

    public static class Logger
    {
        private static int sequenceNumber;

        private static Dictionary<int, List<string>> logStatementsMap = new Dictionary<int, List<string>>();
        private static Dictionary<string, StringBuilder> logStreamMap = new Dictionary<string, StringBuilder>();
        private static long start;
        private static long end;
        private static int count;

        public static void Log(string logStatement)
        {
            List<string> logStatements;
            List<string> newLogStatements = new List<string>();
            int threadId = Thread.CurrentThread.ManagedThreadId;
            lock (logStatementsMap)
            {
                if (!logStatementsMap.TryGetValue(threadId, out logStatements))
                {
                    logStatementsMap.Add(threadId, newLogStatements);
                    logStatements = newLogStatements;
                }
            }
            logStatements.Add(Interlocked.Increment(ref sequenceNumber) + " " + logStatement);
        }

        public static void MarkStart(long start)
        {
            Interlocked.Exchange(ref Logger.start, start);
        }

        public static void MarkEnd(long end)
        {
            Interlocked.Exchange(ref Logger.end, end);
            Interlocked.Increment(ref Logger.count);
        }

        public static void LogStream(string streamName, IEnumerable<byte> streamData)
        {
            StringBuilder logStream;
            StringBuilder newLogStream = new StringBuilder();
            lock (logStreamMap)
            {
                if (!logStreamMap.TryGetValue(streamName, out logStream))
                {
                    logStreamMap.Add(streamName, newLogStream);
                    logStream = newLogStream;
                }
            }
            foreach (byte data in streamData)
            {
                if (data >= 32)
                {
                    logStream.Append((char)data);
                }
                else
                {
                    logStream.Append(string.Format("({0})", data));
                }
            }
            logStream.Append("|");
        }

        public static void Clean()
        {
            logStatementsMap.Clear();
            logStreamMap.Clear();
        }

        public static void Dump()
        {
            Console.WriteLine(count);
            Console.WriteLine(start);
            Console.WriteLine(end);
            //foreach (var thread in logStatementsMap)
            //{
            //    Console.WriteLine("Thread " + thread.Key);
            //    foreach (string line in thread.Value)
            //    {
            //        Console.WriteLine(line);
            //    }
            //    Console.WriteLine();
            //}

            //foreach (var stream in logStreamMap)
            //{
            //    Console.WriteLine(stream.Key + ": " + stream.Value.ToString());
            //}
        }
    }
}
