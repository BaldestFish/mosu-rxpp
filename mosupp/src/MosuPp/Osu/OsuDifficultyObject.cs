using System;
using System.Collections.Generic;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    internal sealed class OsuDifficultyObject
    {
        public const int NormalizedRadius = 50;
        public const int NormalizedDiameter = NormalizedRadius * 2;

        public const double MinDeltaTime = 25.0;
        private const float MaxSliderRadius = NormalizedRadius * 2.4f;
        private const float AssumedSliderRadius = NormalizedRadius * 1.8f;

        public readonly int Idx;
        public readonly OsuObject Base;
        public readonly double StartTime;
        public readonly double DeltaTime;

        public readonly double AdjustedDeltaTime;
        public double LazyJumpDist;
        public double MinJumpDist;
        public double MinJumpTime;
        public double TravelDist;
        public double TravelTime;
        public Pos? LazyEndPos;
        public double LazyTravelDist;
        public double LazyTravelTime;
        public double? Angle;

        public readonly double SmallCircleBonus;

        public OsuDifficultyObject(OsuObject hitObject, OsuObject lastObject, OsuDifficultyObject? lastDiffObj, OsuDifficultyObject? lastLastDiffObj,
                                   double clockRate, int idx, ScalingFactor scalingFactor)
        {
            DeltaTime = (hitObject.StartTime - lastObject.StartTime) / clockRate;
            StartTime = hitObject.StartTime / clockRate;

            double strainTime = RustMath.Max(DeltaTime, MinDeltaTime);
            SmallCircleBonus = RustMath.Max(1.0 + (30.0 - scalingFactor.Radius) / 40.0, 1.0);

            Idx = idx;
            Base = hitObject;
            AdjustedDeltaTime = strainTime;

            ComputeSliderCursorPos(scalingFactor.Radius);
            SetDistances(lastObject, lastDiffObj, lastLastDiffObj, clockRate, scalingFactor);
        }

        /// <summary><c>previous(backwardsIdx)</c> from rosu-pp's IDifficultyObject.</summary>
        public OsuDifficultyObject? Previous(int backwardsIdx, List<OsuDifficultyObject> diffObjects)
        {
            int idx = Idx - (backwardsIdx + 1);
            return idx >= 0 && idx < diffObjects.Count ? diffObjects[idx] : null;
        }

        public OsuDifficultyObject? Next(int forwardsIdx, List<OsuDifficultyObject> diffObjects)
        {
            int idx = Idx + (forwardsIdx + 1);
            return idx < diffObjects.Count ? diffObjects[idx] : null;
        }

        public double OpacityAt(double time, bool hidden, double timePreempt, double timeFadeIn)
        {
            if (time > Base.StartTime)
            {
                // * Consider a hitobject as being invisible when its start time is passed.
                // * In reality the hitobject will be visible beyond its start time up until its hittable window has passed,
                // * but this is an approximation and such a case is unlikely to be hit where this function is used.
                return 0.0;
            }

            double fadeInStartTime = Base.StartTime - timePreempt;
            double fadeInDuration = timeFadeIn;

            if (hidden)
            {
                // * Taken from OsuModHidden.
                double fadeOutStartTime = Base.StartTime - timePreempt + timeFadeIn;
                double fadeOutDuration = timePreempt * OsuDifficultyCalculator.HdFadeOutDurationMultiplier;

                return RustMath.Min(RustMath.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0),
                    1.0 - RustMath.Clamp((time - fadeOutStartTime) / fadeOutDuration, 0.0, 1.0));
            }

            return RustMath.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0);
        }

        public double GetDoubletapness(OsuDifficultyObject? next, double hitWindow)
        {
            if (next == null)
                return 0.0;

            if (Base.IsSpinner)
                hitWindow = 0.0;

            double currDeltaTime = RustMath.Max(DeltaTime, 1.0);
            double nextDeltaTime = RustMath.Max(next.DeltaTime, 1.0);
            double deltaDiff = Math.Abs(nextDeltaTime - currDeltaTime);
            double speedRatio = currDeltaTime / RustMath.Max(currDeltaTime, deltaDiff);
            double windowRatio = RustMath.Pow2(RustMath.Min(currDeltaTime / hitWindow, 1.0));

            return 1.0 - Math.Pow(speedRatio, 1.0 - windowRatio);
        }

        private void SetDistances(OsuObject lastObject, OsuDifficultyObject? lastDiffObj, OsuDifficultyObject? lastLastDiffObj, double clockRate,
                                  ScalingFactor scalingFactor)
        {
            if (Base.Kind == OsuObjectKind.Slider)
            {
                TravelDist = LazyTravelDist * Math.Pow(1.0 + Base.Slider!.RepeatCount() / 2.5, 1.0 / 2.5);
                TravelTime = RustMath.Max(LazyTravelTime / clockRate, MinDeltaTime);
            }

            if (Base.IsSpinner || lastObject.IsSpinner)
                return;

            float scaling = scalingFactor.Factor;

            Pos lastCursorPos = lastDiffObj != null ? GetEndCursorPos(lastDiffObj) : lastObject.StackedPos;

            LazyJumpDist = (Base.StackedPos * scaling - lastCursorPos * scaling).Length();
            MinJumpTime = AdjustedDeltaTime;
            MinJumpDist = LazyJumpDist;

            if (lastDiffObj == null)
                return;

            if (lastObject.Kind == OsuObjectKind.Slider)
            {
                double lastTravelTime = RustMath.Max(lastDiffObj.LazyTravelTime / clockRate, MinDeltaTime);
                MinJumpTime = RustMath.Max(AdjustedDeltaTime - lastTravelTime, MinDeltaTime);

                Pos tailPos = lastObject.Slider!.Tail()?.Pos ?? lastObject.Pos;
                Pos stackedTailPos = tailPos + lastObject.StackOffset;

                float tailJumpDist = (stackedTailPos - Base.StackedPos).Length() * scaling;

                double diff = MaxSliderRadius - AssumedSliderRadius;

                double min = tailJumpDist - MaxSliderRadius;
                MinJumpDist = RustMath.Max(RustMath.Min(LazyJumpDist - diff, min), 0.0);
            }

            if (lastLastDiffObj == null)
                return;

            if (!lastLastDiffObj.Base.IsSpinner)
            {
                Pos lastLastCursorPos = GetEndCursorPos(lastLastDiffObj);

                Pos v1 = lastLastCursorPos - lastObject.StackedPos;
                Pos v2 = Base.StackedPos - lastCursorPos;

                float dot = v1.Dot(v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                Angle = Math.Abs(Math.Atan2(det, dot));
            }
        }

        private void ComputeSliderCursorPos(double radius)
        {
            const double tailLeniency = -36.0;

            if (Base.Kind != OsuObjectKind.Slider || LazyEndPos != null)
                return;

            OsuSlider slider = Base.Slider!;

            Pos pos = Base.Pos;
            Pos stackOffset = Base.StackOffset;
            double startTime = Base.StartTime;
            double duration = slider.EndTime - startTime;

            List<NestedSliderObject> nestedObjects = slider.NestedObjects;

            double trackingEndTime = RustMath.Max(startTime + duration + tailLeniency, startTime + duration / 2.0);

            int lastRealTickIdx = -1;

            for (int i = nestedObjects.Count - 1; i >= 0; i--)
            {
                if (nestedObjects[i].IsTick)
                {
                    lastRealTickIdx = i;
                    break;
                }
            }

            if (lastRealTickIdx >= 0 && nestedObjects[lastRealTickIdx].StartTime > trackingEndTime)
            {
                trackingEndTime = nestedObjects[lastRealTickIdx].StartTime;

                // * When the last tick falls after the tracking end time, we need to re-sort the nested objects
                // * based on time. This creates a somewhat weird ordering which is counter to how a user would
                // * understand the slider, but allows a zero-diff with known diffcalc output.
                // *
                // * To reiterate, this is definitely not correct from a difficulty calculation perspective
                // * and should be revisited at a later date (likely by replacing this whole code with the commented
                // * version above).
                nestedObjects = new List<NestedSliderObject>(nestedObjects);
                NestedSliderObject moved = nestedObjects[lastRealTickIdx];
                nestedObjects.RemoveAt(lastRealTickIdx);
                nestedObjects.Add(moved);
            }

            LazyTravelTime = trackingEndTime - startTime;

            double spanDuration = duration / slider.SpanCount;

            double endTimeMin = LazyTravelTime / spanDuration;

            if (endTimeMin % 2.0 >= 1.0)
                endTimeMin = 1.0 - endTimeMin % 1.0;
            else
                endTimeMin %= 1.0;

            Pos lazyEndPos = pos + stackOffset + slider.Path.PositionAt(endTimeMin);

            Pos currCursorPos = pos + stackOffset;
            double scalingFactor = NormalizedRadius / radius;

            for (int k = 0; k < nestedObjects.Count; k++)
            {
                NestedSliderObject currMovementObj = nestedObjects[k];
                int i = k + 1;

                Pos currMovement = currMovementObj.Pos + stackOffset - currCursorPos;
                double currMovementLen = scalingFactor * currMovement.Length();
                double requiredMovement = AssumedSliderRadius;

                if (i == nestedObjects.Count)
                {
                    Pos lazyMovement = lazyEndPos - currCursorPos;

                    if (lazyMovement.Length() < currMovement.Length())
                        currMovement = lazyMovement;

                    currMovementLen = scalingFactor * currMovement.Length();
                }
                else if (currMovementObj.IsRepeat)
                {
                    requiredMovement = NormalizedRadius;
                }

                if (currMovementLen > requiredMovement)
                {
                    currCursorPos += currMovement * (float)((currMovementLen - requiredMovement) / currMovementLen);
                    currMovementLen *= (currMovementLen - requiredMovement) / currMovementLen;
                    LazyTravelDist += currMovementLen;
                }

                if (i == nestedObjects.Count)
                    lazyEndPos = currCursorPos;
            }

            LazyEndPos = lazyEndPos;
        }

        private static Pos GetEndCursorPos(OsuDifficultyObject hitObject) => hitObject.LazyEndPos ?? hitObject.Base.StackedPos;
    }
}
