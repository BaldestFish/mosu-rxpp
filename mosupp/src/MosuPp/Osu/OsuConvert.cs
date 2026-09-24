using MosuPp.Model;

namespace MosuPp.Osu
{
    /// <summary>
    /// Fields around the scaling of hit objects.
    /// osu!lazer stores these in each hit object but since all objects share the
    /// same scaling (w.r.t. difficulty &amp; performance), rosu-pp stores them only once.
    /// </summary>
    internal sealed class ScalingFactor
    {
        private const float BrokenGamefieldRoundingAllowance = 1.00041f;

        /// <summary><c>NORMALIZED_RADIUS / Radius</c></summary>
        public readonly float Factor;
        public readonly double Radius;
        public readonly float Scale;

        public ScalingFactor(float cs)
        {
            Scale = (float)((double)1.0f - (double)0.7f * BeatmapAttributesExt.DifficultyRangeValue(cs)) / 2f * BrokenGamefieldRoundingAllowance;

            Radius = OsuObject.ObjectRadius * Scale;
            Factor = OsuDifficultyObject.NormalizedRadius / (float)Radius;
        }

        public Pos StackOffset(int stackHeight)
        {
            float stackOffset = stackHeight * Scale * -6.4f;
            return new Pos(stackOffset, stackOffset);
        }
    }

    internal static class OsuConvert
    {
        private const float StackDistance = 3f;

        public static OsuObject[] ConvertObjects(Beatmap map, ScalingFactor scalingFactor, Reflection reflection, double timePreempt, int take,
                                                 OsuDifficultyAttributes attrs)
        {
            var osuObjects = new OsuObject[map.HitObjects.Count];

            for (int i = 0; i < osuObjects.Length; i++)
            {
                OsuObject h = OsuObject.Create(map.HitObjects[i], map, reflection);
                osuObjects[i] = h;

                if (take == 0)
                    continue;

                take--;
                attrs.MaxCombo++;

                switch (h.Kind)
                {
                    case OsuObjectKind.Circle:
                        attrs.NCircles++;
                        break;
                    case OsuObjectKind.Slider:
                        attrs.NSliders++;
                        attrs.NLargeTicks += (uint)h.Slider!.LargeTickCount();
                        attrs.MaxCombo += (uint)h.Slider.NestedObjects.Count;
                        break;
                    case OsuObjectKind.Spinner:
                        attrs.NSpinners++;
                        break;
                }
            }

            foreach (OsuObject h in osuObjects)
            {
                switch (reflection)
                {
                    case Reflection.None:
                        h.FinalizeNested();
                        break;
                    case Reflection.Vertical:
                        h.ReflectVertically();
                        break;
                    case Reflection.Horizontal:
                        h.ReflectHorizontally();
                        break;
                    case Reflection.Both:
                        h.ReflectBothAxes();
                        break;
                }
            }

            double stackThreshold = timePreempt * map.StackLeniency;

            if (map.Version >= 6)
                Stacking(osuObjects, stackThreshold);
            else
                OldStacking(osuObjects, stackThreshold);

            foreach (OsuObject h in osuObjects)
                h.StackOffset = scalingFactor.StackOffset(h.StackHeight);

            return osuObjects;
        }

        private static void Stacking(OsuObject[] hitObjects, double stackThreshold)
        {
            int extendedStartIdx = 0;

            if (hitObjects.Length == 0)
                return;

            int extendedEndIdx = hitObjects.Length - 1;

            // First big `if` in osu!lazer's function can be skipped

            for (int i = extendedEndIdx; i >= 1; i--)
            {
                int n = i;
                int objIIdx = i;
                // * We should check every note which has not yet got a stack.
                // * Consider the case we have two interwound stacks and this will make sense.
                // *   o <-1      o <-2
                // *    o <-3      o <-4
                // * We first process starting from 4 and handle 2,
                // * then we come backwards on the i loop iteration until we reach 3 and handle 1.
                // * 2 and 1 will be ignored in the i loop because they already have a stack value.

                if (hitObjects[objIIdx].StackHeight != 0 || hitObjects[objIIdx].IsSpinner)
                    continue;

                // * If this object is a hitcircle, then we enter this "special" case.
                // * It either ends with a stack of hitcircles only,
                // * or a stack of hitcircles that are underneath a slider.
                // * Any other case is handled by the "is_slider" code below this.
                if (hitObjects[objIIdx].IsCircle)
                {
                    while (--n >= 0)
                    {
                        if (hitObjects[n].IsSpinner)
                            continue;

                        if (hitObjects[objIIdx].StartTime - hitObjects[n].EndTime > stackThreshold)
                            break; // * We are no longer within stacking range of the previous object.

                        // * HitObjects before the specified update range haven't been reset yet
                        if (n < extendedStartIdx)
                        {
                            hitObjects[n].StackHeight = 0;
                            extendedStartIdx = n;
                        }

                        // * This is a special case where hticircles are moved DOWN and RIGHT (negative stacking)
                        // * if they are under the *last* slider in a stacked pattern.
                        // *    o==o <- slider is at original location
                        // *        o <- hitCircle has stack of -1
                        // *         o <- hitCircle has stack of -2
                        if (hitObjects[n].IsSlider && hitObjects[n].EndPos.Distance(hitObjects[objIIdx].Pos) < StackDistance)
                        {
                            int offset = hitObjects[objIIdx].StackHeight - hitObjects[n].StackHeight + 1;

                            for (int j = n + 1; j <= i; j++)
                            {
                                // * For each object which was declared under this slider, we will offset
                                // * it to appear *below* the slider end (rather than above).
                                if (hitObjects[n].EndPos.Distance(hitObjects[j].Pos) < StackDistance)
                                    hitObjects[j].StackHeight -= offset;
                            }

                            // * We have hit a slider. We should restart calculation using this as the new base.
                            // * Breaking here will mean that the slider still has StackCount of 0,
                            // * so will be handled in the i-outer-loop.
                            break;
                        }

                        if (hitObjects[n].Pos.Distance(hitObjects[objIIdx].Pos) < StackDistance)
                        {
                            // * Keep processing as if there are no sliders.
                            // * If we come across a slider, this gets cancelled out.
                            // * NOTE: Sliders with start positions stacking
                            // * are a special case that is also handled here.

                            hitObjects[n].StackHeight = hitObjects[objIIdx].StackHeight + 1;
                            objIIdx = n;
                        }
                    }
                }
                else if (hitObjects[objIIdx].IsSlider)
                {
                    // * We have hit the first slider in a possible stack.
                    // * From this point on, we ALWAYS stack positive regardless.
                    while (--n >= 0)
                    {
                        if (hitObjects[n].IsSpinner)
                            continue;

                        if (hitObjects[objIIdx].StartTime - hitObjects[n].StartTime > stackThreshold)
                            break; // * We are no longer within stacking range of the previous object.

                        if (hitObjects[n].EndPos.Distance(hitObjects[objIIdx].Pos) < StackDistance)
                        {
                            hitObjects[n].StackHeight = hitObjects[objIIdx].StackHeight + 1;
                            objIIdx = n;
                        }
                    }
                }
            }
        }

        private static void OldStacking(OsuObject[] hitObjects, double stackThreshold)
        {
            for (int i = 0; i < hitObjects.Length; i++)
            {
                if (hitObjects[i].StackHeight != 0 && !hitObjects[i].IsSlider)
                    continue;

                double startTime = hitObjects[i].EndTime;

                Pos pos2;
                OsuObject h = hitObjects[i];

                if (h.Kind == OsuObjectKind.Slider)
                {
                    // We need the path endpos instead of the slider endpos
                    int repeatCount = h.Slider!.RepeatCount();
                    NestedSliderObject? nested = null;

                    if (repeatCount % 2 == 0)
                    {
                        nested = h.Slider.Tail();
                    }
                    else
                    {
                        foreach (NestedSliderObject n in h.Slider.NestedObjects)
                        {
                            if (n.Kind == NestedSliderObjectKind.Repeat)
                            {
                                nested = n;
                                break;
                            }
                        }
                    }

                    pos2 = nested?.Pos ?? h.Pos;
                }
                else
                {
                    pos2 = h.Pos;
                }

                int sliderStack = 0;

                for (int j = i + 1; j < hitObjects.Length; j++)
                {
                    if (hitObjects[j].StartTime - stackThreshold > startTime)
                        break;

                    // * Note the use of `StartTime` in the code below doesn't match stable's use of `EndTime`.
                    // * This is because in the stable implementation, `UpdateCalculations` is not called on the inner-loop hitobject (j)
                    // * and therefore it does not have a correct `EndTime`, but instead the default of `EndTime = StartTime`.

                    if (hitObjects[j].Pos.Distance(hitObjects[i].Pos) < StackDistance)
                    {
                        hitObjects[i].StackHeight++;
                        startTime = hitObjects[j].StartTime;
                    }
                    else if (hitObjects[j].Pos.Distance(pos2) < StackDistance)
                    {
                        sliderStack++;
                        hitObjects[j].StackHeight -= sliderStack;
                        startTime = hitObjects[j].StartTime;
                    }
                }
            }
        }
    }
}
