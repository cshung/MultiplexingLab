using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexer
{
    public static class Logger
    {
        private static Dictionary<int, List<string>> logStatementsMap = new Dictionary<int, List<string>>();
        
        public static void Log(string logStatement)
        {
            List<string> logStatements;
            List<string> newLogStatements = new List<string>();
            int threadId = Thread.CurrentThread.ManagedThreadId;
            lock (logStatementsMap)
            {
                if (!logStatementsMap.TryGetValue(threadId, out logStatements))
                {
                    logStatements = new List<string>();
                    logStatementsMap.Add(threadId, newLogStatements);
                    logStatements = newLogStatements;
                }
            }
            logStatements.Add(DateTime.Now.Ticks + " " + logStatement);
        }

        public static void Clean()
        {
            logStatementsMap.Clear();
        }

        public static void Dump()
        {
            foreach (var thread in logStatementsMap)
            {
                Console.WriteLine("Thread " + thread.Key);
                foreach (string line in thread.Value)
                {
                    Console.WriteLine(line);
                }
                Console.WriteLine();
            }
        }
    }
}
