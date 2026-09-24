using System;
using System.Collections.Generic;

namespace MosuPp.Model
{
    internal enum SliderEventType
    {
        Head,
        Tick,
        Repeat,
        LastTick,
        Tail,
    }

    internal readonly struct SliderEvent
    {
        public readonly SliderEventType Kind;
        public readonly int SpanIdx;
        public readonly double SpanStartTime;
        public readonly double Time;
        public readonly double PathProgress;

        public SliderEvent(SliderEventType kind, int spanIdx, double spanStartTime, double time, double pathProgress)
        {
            Kind = kind;
            SpanIdx = spanIdx;
            SpanStartTime = spanStartTime;
            Time = time;
            PathProgress = pathProgress;
        }
    }

    /// <summary>Port of rosu-map's <c>SliderEventsIter</c>, eagerly producing all events in order.</summary>
    internal static class SliderEvents
    {
        private const double MaxLen = 100_000.0;
        private const double TailLeniency = -36.0;

        public static List<SliderEvent> Generate(double startTime, double spanDuration, double velocity, double tickDist,
                                                 double totalDist, int spanCount)
        {
            double len = Util.RustMath.Min(MaxLen, totalDist);

            // Rust's f64::clamp panics on NaN bounds; lazer would behave just as badly on such maps.
            tickDist = Util.RustMath.Clamp(tickDist, 0.0, len);
            double minDistFromEnd = velocity * 10.0;

            var events = new List<SliderEvent>
            {
                new SliderEvent(SliderEventType.Head, 0, startTime, startTime, 0.0),
            };

            var ticks = new List<SliderEvent>();

            for (int span = 0; span < spanCount; span++)
            {
                bool reversed = span % 2 == 1;
                double spanStartTime = startTime + span * spanDuration;
                bool withRepeat = span < spanCount - 1;

                ticks.Clear();
                double d = tickDist;

                if (d > 0.0)
                {
                    while (d <= len)
                    {
                        if (d >= len - minDistFromEnd)
                            break;

                        double pathProgress = d / len;
                        double timeProgress = reversed ? 1.0 - pathProgress : pathProgress;

                        ticks.Add(new SliderEvent(SliderEventType.Tick, span, spanStartTime, spanStartTime + timeProgress * spanDuration, pathProgress));
                        d += tickDist;
                    }
                }

                // rosu-map pops from a stack: for reversed spans the ticks come out
                // in descending distance order, otherwise in ascending order.
                if (reversed)
                    ticks.Reverse();

                events.AddRange(ticks);

                if (withRepeat)
                    events.Add(new SliderEvent(SliderEventType.Repeat, span, spanStartTime, spanStartTime + spanDuration, (span + 1) % 2));
            }

            // Last tick
            {
                double totalDuration = spanCount * spanDuration;
                int finalSpanIdx = spanCount - 1;
                double finalSpanStartTime = startTime + finalSpanIdx * spanDuration;
                double lastTickTime = Util.RustMath.Max(startTime + totalDuration / 2.0, (finalSpanStartTime + spanDuration) + TailLeniency);
                double lastTickProgress = (lastTickTime - finalSpanStartTime) / spanDuration;

                if (spanCount % 2 == 0)
                    lastTickProgress = 1.0 - lastTickProgress;

                events.Add(new SliderEvent(SliderEventType.LastTick, finalSpanIdx, finalSpanStartTime, lastTickTime, lastTickProgress));
            }

            // Tail
            {
                double totalDuration = spanCount * spanDuration;
                int finalSpanIdx = spanCount - 1;

                events.Add(new SliderEvent(SliderEventType.Tail, finalSpanIdx, startTime + (spanCount - 1) * spanDuration,
                    startTime + totalDuration, spanCount % 2));
            }

            return events;
        }
    }
}
