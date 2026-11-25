using System;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Render;
using LCAnomalyOrdeals.Utilities;
using LCAnomalyOrdeals.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using LCDamageDefOf = LCAnomalyCore.Defs.DamageDefOf;

namespace LCAnomalyOrdeals.Comps
{
    /// <summary>琥珀色高阶考验层级（黄昏《食物链》 / 午夜《永恒的盛宴》）。</summary>
    public enum AmberOrdealTier
    {
        Dusk = 0,
        Midnight = 1
    }

    /// <summary>琥珀黄昏与琥珀午夜多部件骨骼动画与钻地/裂颚孵化机制配置。</summary>
    public class CompProperties_AmberOrdeal : CompProperties
    {
        public AmberOrdealTier tier = AmberOrdealTier.Dusk;
        public float emergenceRadius = 3.8f;
        public float emergenceDamageMin = 24f;
        public float emergenceDamageMax = 34f;
        public float spawnShockwaveRadius = 4.6f;

        public CompProperties_AmberOrdeal()
        {
            compClass = typeof(CompAmberOrdeal);
        }
    }

    /// <summary>
    /// 琥珀黄昏《食物链》(WAW) 与琥珀午夜《永恒的盛宴》(ALEPH) 原生骨骼动画与行为状态机：
    /// - 琥珀黄昏：三段式可视钻地（DigIn -> 地下潜行土浪 -> DigOut 破土红伤重击）+ 弓身吐卵孵化（Spawn）+ 巨颚啃噬（Eat）；
    /// - 琥珀午夜：巨型竖立母虫三段式破土重压 + 裂瓣咆哮（左右卵囊巨瓣向两侧张开，露出中央锯齿内颚嘶鸣并喷吐琥珀黄昏）。
    /// </summary>
    public class CompAmberOrdeal : ThingComp, IOrdealArticulatedComp
    {
        private bool isFacingLeft;
        private int attackAnimTicksLeft;
        private int spawnAnimTicksLeft;
        private int digInTicksLeft;
        private int burrowedTicksLeft;
        private int digOutTicksLeft;
        private IntVec3 pendingBurrowDest = IntVec3.Invalid;

        public CompProperties_AmberOrdeal Props => (CompProperties_AmberOrdeal)props;
        public Pawn Pawn => parent as Pawn;
        public AmberOrdealTier Tier => Props.tier;

        public bool IsFacingLeft => Tier == AmberOrdealTier.Dusk && isFacingLeft;
        public bool IsBurrowedHidden => burrowedTicksLeft > 0;
        public bool IsBurrowing => digInTicksLeft > 0 || burrowedTicksLeft > 0 || digOutTicksLeft > 0;
        public bool IsAlertVisual => spawnAnimTicksLeft > 0 || attackAnimTicksLeft > 0;
        public float ChargeProgress => 0f;
        public float EmitterSpinAngle => 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isFacingLeft, "isFacingLeft", false);
            Scribe_Values.Look(ref attackAnimTicksLeft, "attackAnimTicksLeft", 0);
            Scribe_Values.Look(ref spawnAnimTicksLeft, "spawnAnimTicksLeft", 0);
            Scribe_Values.Look(ref digInTicksLeft, "digInTicksLeft", 0);
            Scribe_Values.Look(ref burrowedTicksLeft, "burrowedTicksLeft", 0);
            Scribe_Values.Look(ref digOutTicksLeft, "digOutTicksLeft", 0);
            Scribe_Values.Look(ref pendingBurrowDest, "pendingBurrowDest", IntVec3.Invalid);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Pawn != null && Pawn.Spawned)
            {
                digOutTicksLeft = Tier == AmberOrdealTier.Midnight ? 52 : 44;
                Vector3 pos = Pawn.Position.ToVector3Shifted();
                Color amberFx = new Color(0.94f, 0.56f, 0.14f, 0.92f);
                SoundDef sfx = Tier == AmberOrdealTier.Midnight
                    ? OrdealDefOf.LCOrdeal_Sound_Bug3DigOut
                    : OrdealDefOf.LCOrdeal_Sound_BugDuskDigOut;
                sfx?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                OrdealVfxUtility.SpawnCrater(Pawn.Map, pos, Tier == AmberOrdealTier.Midnight ? 5.6f : 4.0f, amberFx, 160);
                OrdealVfxUtility.SpawnShockwave(Pawn.Map, pos, 0.8f, Props.emergenceRadius, amberFx, 38);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (attackAnimTicksLeft > 0) attackAnimTicksLeft--;
            if (spawnAnimTicksLeft > 0) spawnAnimTicksLeft--;

            UpdateFacing(pawn);
            TickBurrow(pawn);
            UpdateAnimation(pawn);
        }

        public void NotifyMeleeAttack(Thing target)
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null || IsBurrowing)
            {
                return;
            }

            if (target != null && Tier == AmberOrdealTier.Dusk)
            {
                UpdateFacingToward(pawn, target.Position);
            }

            attackAnimTicksLeft = Tier == AmberOrdealTier.Midnight ? 56 : 42;
            Vector3 fxPos = target != null ? target.DrawPos : pawn.DrawPos;
            Color amberFx = new Color(0.94f, 0.54f, 0.14f, 0.92f);
            if (Tier == AmberOrdealTier.Midnight)
            {
                OrdealDefOf.LCOrdeal_Sound_Bug3Shout?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                OrdealVfxUtility.SpawnShockwave(pawn.Map, fxPos, 0.6f, 4.0f, amberFx, 32);
                OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 3.2f, amberFx, 24);
            }
            else
            {
                OrdealDefOf.LCOrdeal_Sound_BugDuskEat?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.6f, amberFx, 22);
            }
        }

        /// <summary>触发孵化/吐虫咆哮骨骼动画（琥珀黄昏弓身吐虫 / 琥珀午夜巨瓣裂开咆哮）。</summary>
        public void TriggerSpawnRoar()
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            spawnAnimTicksLeft = Tier == AmberOrdealTier.Midnight ? 64 : 52;
            Color amberFx = new Color(0.96f, 0.60f, 0.16f, 0.92f);
            if (Tier == AmberOrdealTier.Midnight)
            {
                OrdealDefOf.LCOrdeal_Sound_Bug3Shout?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                OrdealVfxUtility.SpawnShockwave(pawn.Map, pawn.DrawPos, 0.8f, Props.spawnShockwaveRadius, amberFx, 38);
            }
            else
            {
                OrdealDefOf.LCOrdeal_Sound_BugDuskSpawn?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                OrdealVfxUtility.SpawnShockwave(pawn.Map, pawn.DrawPos, 0.5f, 3.4f, amberFx, 30);
            }
        }

        /// <summary>启动真实三段式钻地潜行突袭。</summary>
        public bool BeginBurrow(IntVec3 destination)
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null || IsBurrowing || !destination.IsValid)
            {
                return false;
            }

            pendingBurrowDest = destination.ClampInsideMap(pawn.Map);
            digInTicksLeft = Tier == AmberOrdealTier.Midnight ? 44 : 38;
            pawn.pather?.StopDead();
            if (pawn.CurJob != null) pawn.jobs?.StopAll();

            Color amberFx = new Color(0.92f, 0.56f, 0.16f, 0.90f);
            SoundDef sfx = Tier == AmberOrdealTier.Midnight
                ? OrdealDefOf.LCOrdeal_Sound_Bug3DigIn
                : OrdealDefOf.LCOrdeal_Sound_BugDuskDigIn;
            sfx?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            OrdealVfxUtility.SpawnCrater(pawn.Map, pawn.DrawPos, Tier == AmberOrdealTier.Midnight ? 5.0f : 3.6f, amberFx, 130);
            FleckMaker.ThrowDustPuffThick(pawn.DrawPos, pawn.Map, 2.2f, new Color(0.76f, 0.50f, 0.20f));
            return true;
        }

        private void TickBurrow(Pawn pawn)
        {
            if (digInTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                digInTicksLeft--;
                if (digInTicksLeft == 0)
                {
                    burrowedTicksLeft = Tier == AmberOrdealTier.Midnight ? 48 : 42;
                }
                return;
            }

            if (burrowedTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                burrowedTicksLeft--;
                if (burrowedTicksLeft % 10 == 0 && pendingBurrowDest.IsValid)
                {
                    float denom = Tier == AmberOrdealTier.Midnight ? 48f : 42f;
                    float frac = 1f - burrowedTicksLeft / denom;
                    Vector3 trailPos = Vector3.Lerp(pawn.DrawPos, pendingBurrowDest.ToVector3Shifted(), frac);
                    FleckMaker.ThrowDustPuffThick(trailPos, pawn.Map, 1.8f, new Color(0.76f, 0.50f, 0.20f));
                }

                if (burrowedTicksLeft == 0)
                {
                    Map map = pawn.Map;
                    IntVec3 dest = pendingBurrowDest.IsValid && pendingBurrowDest.Standable(map)
                        ? pendingBurrowDest
                        : pawn.Position;
                    pendingBurrowDest = IntVec3.Invalid;
                    if (dest != pawn.Position)
                    {
                        pawn.Position = dest;
                        pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                    }

                    digOutTicksLeft = Tier == AmberOrdealTier.Midnight ? 52 : 44;
                    Color amberFx = new Color(0.95f, 0.58f, 0.16f, 0.94f);
                    SoundDef sfx = Tier == AmberOrdealTier.Midnight
                        ? OrdealDefOf.LCOrdeal_Sound_Bug3DigOut
                        : OrdealDefOf.LCOrdeal_Sound_BugDuskDigOut;
                    sfx?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnCrater(map, pawn.DrawPos, Tier == AmberOrdealTier.Midnight ? 6.0f : 4.4f, amberFx, 170);
                    OrdealVfxUtility.SpawnShockwave(map, pawn.DrawPos, 0.8f, Props.emergenceRadius, amberFx, 38);
                    FleckMaker.ThrowDustPuffThick(pawn.DrawPos, map, 2.6f, new Color(0.76f, 0.50f, 0.20f));

                    DamageDef redDmg = LCDamageDefOf.LC_RedDamage ?? DamageDefOf.Blunt;
                    foreach (Pawn victim in OrdealTargetUtility.AllTargets(map))
                    {
                        if (!victim.Dead && victim.Spawned && victim.Position.InHorDistOf(pawn.Position, Props.emergenceRadius))
                        {
                            victim.TakeDamage(new DamageInfo(redDmg, Rand.Range(Props.emergenceDamageMin, Props.emergenceDamageMax), 0.22f, instigator: pawn, weapon: pawn.def));
                            OrdealVfxUtility.SpawnSlash(map, victim.DrawPos, 2.4f, amberFx, 20);
                        }
                    }
                }
                return;
            }

            if (digOutTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                digOutTicksLeft--;
            }
        }

        private void UpdateFacing(Pawn pawn)
        {
            if (Tier == AmberOrdealTier.Midnight)
            {
                isFacingLeft = false;
                return;
            }

            if (pawn.pather != null && pawn.pather.Moving)
            {
                int dx = pawn.pather.nextCell.x - pawn.Position.x;
                if (dx < 0) isFacingLeft = true;
                else if (dx > 0) isFacingLeft = false;
                return;
            }

            if (pawn.Rotation == Rot4.West) isFacingLeft = true;
            else if (pawn.Rotation == Rot4.East) isFacingLeft = false;
        }

        private void UpdateFacingToward(Pawn pawn, IntVec3 targetCell)
        {
            int dx = targetCell.x - pawn.Position.x;
            if (dx < 0) isFacingLeft = true;
            else if (dx > 0) isFacingLeft = false;
        }

        private void UpdateAnimation(Pawn pawn)
        {
            AnimationDef desired = ResolveDesiredAnimation(pawn);
            if (desired == null)
            {
                return;
            }

            PawnRenderTree tree = pawn.Drawer?.renderer?.renderTree;
            if (tree == null)
            {
                return;
            }

            if (tree.currentAnimation != desired)
            {
                pawn.Drawer.renderer.SetAnimation(desired);
            }
        }

        private AnimationDef ResolveDesiredAnimation(Pawn pawn)
        {
            if (Tier == AmberOrdealTier.Dusk)
            {
                if (digInTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberDusk_DigIn;
                if (digOutTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberDusk_DigOut;
                if (spawnAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberDusk_Spawn;
                if (attackAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberDusk_Attack;
                bool moving = pawn.pather != null && pawn.pather.Moving;
                return moving ? OrdealDefOf.LCOrdeal_AmberDusk_Walk : OrdealDefOf.LCOrdeal_AmberDusk_Idle;
            }

            if (digInTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberMidnight_DigIn;
            if (digOutTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberMidnight_DigOut;
            if (spawnAnimTicksLeft > 0 || attackAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberMidnight_Roar;
            return OrdealDefOf.LCOrdeal_AmberMidnight_Idle;
        }
    }
}
