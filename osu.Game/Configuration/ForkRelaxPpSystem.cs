// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ForkRelaxPpSystem
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemMosuRealistik))]
        MosuRealistik,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemLazerVanilla))]
        LazerVanilla,

        /// <summary>
        /// MosuPp: the Mosu/Realistik RX calculator plus the MosuPp rules (see MOSUPP_CHANGELOG.md):
        /// Aim-Focused Flow Guard, Length Bonus + Spike Nerf, Point Variety Nerf, Extreme Jump Nerf, Stream Map Nerf,
        /// Stream-Only Guard, One-Point Map Guard, Short High CS Nerf, CS PP Buff, Heavy Miss Penalty and Traceable = Hidden.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemMosuPp))]
        MosuPp
    }
}
