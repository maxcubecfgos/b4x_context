using System;
using System.Collections.Generic;
using System.Linq;

namespace B4XContext.Services
{
    /// <summary>
    /// Token budget for a local model context window (inspired by opencode's <c>usable()</c>):
    /// the prompt never occupies the whole context — a share of it is reserved so the model
    /// can actually answer. 25% of the context (min 256) is kept for the response.
    /// </summary>
    public static class ContextBudget
    {
        public const int DefaultContext = 4096;
        public const int MinContext = 1024;
        public const int MaxContext = 1024 * 1024;
        public const int ReservePercent = 25;

        /// <summary>Usual context sizes, from 1K up to 1M (slider stops).</summary>
        public static readonly int[] Stops =
        {
            1024, 2048, 4096, 8192, 16384, 32768, 65536,
            131072, 262144, 524288, 1048576
        };

        /// <summary>Quick selector presets shown in the Pack Summary card.</summary>
        public static readonly int[] Presets = { 2048, 4096, 8192, 16384 };

        public static int ReservedOutput(int context) => Math.Max(256, context * ReservePercent / 100);

        public static int Usable(int context) => Math.Max(0, context - ReservedOutput(context));

        /// <summary>Nearest stop for a raw context value (clamped to the supported range).</summary>
        public static int NearestStop(int context)
        {
            if (context <= Stops[0]) return Stops[0];
            if (context >= Stops[Stops.Length - 1]) return Stops[Stops.Length - 1];
            int best = Stops[0];
            int bestDelta = int.MaxValue;
            foreach (var s in Stops)
            {
                int d = Math.Abs(s - context);
                if (d < bestDelta) { bestDelta = d; best = s; }
            }
            return best;
        }

        /// <summary>Label like 4K / 64K / 1M.</summary>
        public static string Label(int context)
        {
            if (context >= 1024 * 1024) return "1M";
            if (context >= 1024)
            {
                int k = context / 1024;
                return (context % 1024 == 0 ? $"{k}K" : $"{context / 1000.0:0.#}K");
            }
            return context.ToString();
        }

        /// <summary>
        /// Warn-only overflow analysis: totals vs. usable budget and the files that would have to
        /// be dropped (largest first) to fit. Nothing is removed automatically — the user decides.
        /// </summary>
        public static BudgetReport Analyze(int total, int context, IEnumerable<(string Name, int Tokens)> included)
        {
            int reserved = ReservedOutput(context);
            int usable = Math.Max(0, context - reserved);

            var files = (included ?? Enumerable.Empty<(string Name, int Tokens)>())
                .Where(f => f.Tokens > 0)
                .OrderByDescending(f => f.Tokens)
                .Select(f => (Name: f.Name, Tokens: f.Tokens))
                .ToList();

            int fileSum = files.Sum(f => f.Tokens);
            int baseTokens = Math.Max(0, total - fileSum);
            int excess = Math.Max(0, total - usable);

            var offenders = new List<(string Name, int Tokens)>();
            if (excess > 0)
            {
                int running = total;
                foreach (var f in files)
                {
                    if (running <= usable) break;
                    running -= f.Tokens;
                    offenders.Add(f);
                }
            }

            return new BudgetReport
            {
                Context = context,
                ReservedOutput = reserved,
                Usable = usable,
                Total = total,
                Excess = excess,
                BaseOverBudget = baseTokens > usable,
                Offenders = offenders
            };
        }
    }

    public sealed class BudgetReport
    {
        public int Context { get; init; }
        public int ReservedOutput { get; init; }
        public int Usable { get; init; }
        public int Total { get; init; }
        /// <summary>Tokens over the usable budget (0 when it fits).</summary>
        public int Excess { get; init; }
        /// <summary>True when preamble/task/tree alone exceed the usable budget (no file removal helps).</summary>
        public bool BaseOverBudget { get; init; }
        /// <summary>Largest-first files that would need to be dropped or compacted to fit.</summary>
        public IReadOnlyList<(string Name, int Tokens)> Offenders { get; init; } = Array.Empty<(string, int)>();

        public bool Fits => Excess <= 0;
    }
}
