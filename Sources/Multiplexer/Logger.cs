namespace Multiplexer
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	public static class Logger
	{
		// [ThreadStatic]
		public volatile static bool Tracing;

		private static int sequenceNumber;

		private static Dictionary<int, List<string>> logStatementsMap = new Dictionary<int, List<string>>();
		private static Dictionary<string, StringBuilder> logStreamMap = new Dictionary<string, StringBuilder>();
		
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

		public static void LogStream(string streamName, IEnumerable<byte> streamData)
		{
			StringBuilder logStream;
			StringBuilder newLogStream = new StringBuilder();
			lock(logStreamMap)
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
			foreach (var thread in logStatementsMap)
			{
				Console.WriteLine("Thread " + thread.Key);
				foreach (string line in thread.Value)
				{
					Console.WriteLine(line);
				}
				Console.WriteLine();
			}

			foreach (var stream in logStreamMap)
			{
				Console.WriteLine(stream.Key + ": " + stream.Value.ToString());
			}
		}
	}
}
