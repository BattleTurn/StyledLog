using System;
using System.Diagnostics;

namespace BattleTurn.StyledLog
{
    public static class StyledDebugBenchmark
    {
        public struct Result
        {
            public double milliseconds;   // elapsed time for one log
            public long bytes;            // GC delta in bytes (can be negative if GC happened)
            public int outputLength;      // length of the generated rich-text string
        }

        /// <summary>
        /// Benchmarks a single StyledDebug.Log call (tag + parts).
        /// Measures GC memory delta and elapsed time around the call.
        /// </summary>
        public static Result RunOnce(string tag, params StyledText[] parts)
        {
            // Force a GC to stabilize baseline a bit (optional):
            // GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

            long beforeMem = GC.GetTotalMemory(false);
            var sw = Stopwatch.StartNew();

            StyledDebug.Log(tag, parts);

            sw.Stop();
            long afterMem = GC.GetTotalMemory(false);

            // Estimate resulting string length for reference
            int outLen = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                // Roughly approximate per-part length to avoid allocating a new string
                var p = parts[i];
                outLen += (p.text?.Length ?? 0)
                          + (string.IsNullOrEmpty(p.hexColor) ? 0 : 17)   // <color=...></color>
                          + (string.IsNullOrEmpty(p.font) ? 0 : 15)       // <font="..."></font>
                          + (p.style.HasFlag(TextStyle.Bold) ? 7 : 0)     // <b></b>
                          + (p.style.HasFlag(TextStyle.Underline) ? 7 : 0)// <u></u>
                          + (p.style.HasFlag(TextStyle.Strikethrough) ? 7 : 0);// <s></s>
            }

            return new Result
            {
                milliseconds = sw.Elapsed.TotalMilliseconds,
                bytes = afterMem - beforeMem,
                outputLength = outLen
            };
        }

        /// <summary>
        /// Runs N iterations and returns average numbers. A warmup pass is done first.
        /// </summary>
        public static Result RunMany(string tag, int iterations, params StyledText[] parts)
        {
            if (iterations <= 0) iterations = 1;

            // Warmup once to JIT and populate caches
            RunOnce(tag, parts);

            long memSum = 0;
            double msSum = 0.0;
            int outLen = 0;

            for (int i = 0; i < iterations; i++)
            {
                var r = RunOnce(tag, parts);
                memSum += r.bytes;
                msSum  += r.milliseconds;
                outLen  = r.outputLength; // same for each iteration
            }

            return new Result
            {
                milliseconds = msSum / iterations,
                bytes = (long)(memSum / (double)iterations),
                outputLength = outLen
            };
        }
    }
}
