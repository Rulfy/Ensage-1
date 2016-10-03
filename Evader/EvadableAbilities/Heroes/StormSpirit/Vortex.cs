﻿namespace Evader.EvadableAbilities.Heroes
{
    using Base;

    using Ensage;

    using static Core.Abilities;

    internal class Vortex : LinearTarget
    {
        #region Constructors and Destructors

        public Vortex(Ability ability)
            : base(ability)
        {
            //todo add aghanim

            CounterAbilities.Add(PhaseShift);
            CounterAbilities.Add(BallLightning);
            CounterAbilities.Add(Eul);
            CounterAbilities.Add(Manta);
            CounterAbilities.Add(SleightOfFist);
            CounterAbilities.AddRange(VsDamage);
            CounterAbilities.AddRange(VsMagic);
            CounterAbilities.AddRange(VsPhys);
            CounterAbilities.Add(Lotus);
            CounterAbilities.AddRange(Invis);
            CounterAbilities.Add(NetherWard);
        }

        #endregion
    }
}