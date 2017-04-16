﻿namespace CompleteLastHitMarker.Abilities.Passive.Base
{
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;

    using Interfaces;

    using Units;
    using Units.Base;

    internal abstract class DefaultPassive : DefaultAbility, IPassiveAbility
    {
        protected List<AbilityId> DoesNotStackWith = new List<AbilityId>();

        protected DefaultPassive(Ability ability)
            : base(ability)
        {
        }

        public abstract float GetBonusDamage(Hero hero, KillableUnit unit, IEnumerable<IPassiveAbility> abilities);

        protected virtual bool CanDoDamage(Hero hero, KillableUnit unit, IEnumerable<IPassiveAbility> abilities)
        {
            if (unit.Team == hero.Team)
            {
                return false;
            }

            if (DamageType != DamageType.Physical && unit is KillableTower)
            {
                return false;
            }

            return !abilities.Any(x => DoesNotStackWith.Contains(x.AbilityId));
        }
    }
}