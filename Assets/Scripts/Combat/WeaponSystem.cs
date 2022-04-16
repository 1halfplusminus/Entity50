using Unity.Entities;
using RPG.Core;
using Unity.Transforms;
using Unity.Jobs;
using UnityEngine;
using RPG.Animation;
using Unity.Rendering;
using UnityEngine.VFX;

using RPG.Stats;
using Unity.Collections;

namespace RPG.Combat
{
    public struct WeaponSpawning : IComponentData
    {

    }
    public struct InstanciedWeapon : IComponentData
    {
        public Entity Entity;
    }
    public struct WeaponInstance : IComponentData
    {

    }
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial class FighterEquipWeaponSystem : SystemBase
    {

        EntityQuery fighterEquipWeaponQuery;
        EntityCommandBufferSystem entityCommandBufferSystem;
        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            RequireForUpdate(fighterEquipWeaponQuery);
        }

        protected override void OnUpdate()
        {
            var cbp = entityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            Entities
            .WithStoreEntityQueryInField(ref fighterEquipWeaponQuery)
            .ForEach((Entity e, ref Fighter fighter, ref DynamicBuffer<HitEvent> hitEvents, in FighterEquipped equiped) =>
            {
                ref var weaponRef = ref equiped.WeaponAsset.Value;
                var weapon = weaponRef.Weapon;
                Debug.Log($"Fighter {e.Index} equip weapon {weapon.GUID}");
                var weaponHitEvents = weapon.HitEvents;
                fighter.AttackDuration = weapon.AttackDuration;
                fighter.Cooldown = weapon.Cooldown;
                fighter.Damage = weapon.Damage;
                fighter.Range = weapon.Range;
                hitEvents.Clear();
                hitEvents.Capacity = weaponHitEvents.Length;
                for (int i = 0; i < weaponHitEvents.Length; i++)
                {
                    hitEvents.Add(new HitEvent { Time = weaponHitEvents[i], Trigger = equiped.Instance });
                }
            }).ScheduleParallel();
            EntityManager.RemoveComponent<FighterEquipped>(fighterEquipWeaponQuery);
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }

    }
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial class SpawnWeaponSystem : SystemBase
    {
        EntityCommandBufferSystem entityCommandBufferSystem;
        EntityQuery spawnWeaponQuery;
        EntityQuery weaponInstanceWithoutParent;
        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            weaponInstanceWithoutParent = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    typeof(WeaponInstance)
                },
                None = new ComponentType[]{
                    typeof(Parent)
                }
            });
            // RequireForUpdate(spawnWeaponQuery);
        }
        protected override void OnUpdate()
        {
            var cb = entityCommandBufferSystem.CreateCommandBuffer();
            var cbp = cb.AsParallelWriter();
            Entities
            .WithStoreEntityQueryInField(ref spawnWeaponQuery)
            .ForEach((int entityInQueryIndex, Entity e, in SpawnWeapon spawn, in LocalToWorld localToWorld, in EquippedBy equipedBy) =>
            {
                Debug.Log($"spawn weapon prefab {spawn.Prefab.Index} at : ${localToWorld.Position}");
                Entity instance = Entity.Null;
                if (spawn.Prefab != Entity.Null)
                {
                    instance = cbp.Instantiate(entityInQueryIndex, spawn.Prefab);
                    cbp.AddComponent(entityInQueryIndex, instance, new Parent { Value = e });
                    cbp.AddComponent<LocalToParent>(entityInQueryIndex, instance);
                    cbp.AddComponent<WeaponInstance>(entityInQueryIndex, instance);

                    cbp.AddComponent(entityInQueryIndex, e, new InstanciedWeapon { Entity = instance });

                }
                cbp.RemoveComponent<SpawnWeapon>(entityInQueryIndex, e);
                cbp.AddComponent(entityInQueryIndex, equipedBy.Entity, new FighterEquipped { WeaponAsset = spawn.Weapon, Instance = instance });
                if (spawn.Projectile != Entity.Null)
                {
                    cbp.AddComponent(entityInQueryIndex, equipedBy.Entity, new ShootProjectile() { Prefab = spawn.Projectile, Socket = e });
                }
                cbp.AddComponent(entityInQueryIndex, equipedBy.Entity, new ChangeAttackAnimation() { Animation = spawn.Animation });
            }).ScheduleParallel();

            cb.DestroyEntitiesForEntityQuery(weaponInstanceWithoutParent);
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }

    }
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial class EquipWeaponInSocketSystem : SystemBase
    {
        EntityCommandBufferSystem entityCommandBufferSystem;

        EntityQuery equipPrefabInSocketQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

            RequireForUpdate(equipPrefabInSocketQuery);

        }
        protected override void OnUpdate()
        {
            var cb = entityCommandBufferSystem.CreateCommandBuffer();
            var cbp = cb.AsParallelWriter();

            Entities
            .WithNone<Equipped>()
            .ForEach((int entityInQueryIndex, Entity e, in ShootProjectile shootProjectile, in WeaponAssetData weaponAssetData) =>
            {
                weaponAssetData.Weapon.Value.ProjectileEntity = shootProjectile.Prefab;
                if (shootProjectile.Prefab == Entity.Null)
                {
                    cbp.RemoveComponent<ShootProjectile>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

            Entities
            .WithNone<Equipped>()
            .WithStoreEntityQueryInField(ref equipPrefabInSocketQuery)
            .ForEach((int entityInQueryIndex, Entity e, in EquipInSocket equipWeapon, in EquippedBy equipedBy) =>
            {
                if (HasComponent<WeaponAssetData>(equipWeapon.Weapon) && HasComponent<ChangeAttackAnimation>(equipWeapon.Weapon))
                {
                    var weaponData = GetComponent<WeaponAssetData>(equipWeapon.Weapon);
                    var changeAttackAnimation = GetComponent<ChangeAttackAnimation>(equipWeapon.Weapon);
                    var equippedPrefab = GetComponent<EquippedPrefab>(equipWeapon.Weapon);
                    Debug.Log($"Equip {weaponData.Weapon.Value.Weapon.GUID} in socket : ${equipWeapon.Socket.Index}");
                    cbp.AddComponent(entityInQueryIndex, equipWeapon.Socket, new SpawnWeapon
                    {
                        Prefab = equippedPrefab.Value,
                        Animation = changeAttackAnimation.Animation,
                        Weapon = weaponData.Weapon,
                        Projectile = weaponData.Weapon.Value.ProjectileEntity
                    });
                    cbp.RemoveComponent<EquipInSocket>(entityInQueryIndex, e);
                    cbp.AddComponent(entityInQueryIndex, equipWeapon.Socket, new Equipped { Equipable = weaponData.Weapon });
                    //FIXME: BAD
                    var buffer = cbp.AddBuffer<StatsModifier>(entityInQueryIndex, equipWeapon.Socket);
                    buffer.Clear();
                    buffer.Add(new StatsModifier { Type = StatModifierType.Additive, Entity = equipedBy.Entity, Stats = Stats.Stats.Damage, Value = weaponData.Weapon.Value.Weapon.Damage });
                    buffer.Add(new StatsModifier { Type = StatModifierType.Percent, Entity = equipedBy.Entity, Stats = Stats.Stats.Damage, Value = weaponData.Weapon.Value.Weapon.BonusDamagePercent });
                }

            }).ScheduleParallel();


            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }

    }

    // [UpdateInGroup(typeof(CombatSystemGroup))]
    // public partial class CollidWithPickableWeaponSystem : SystemBase
    // {
    //     EntityCommandBufferSystem entityCommandBufferSystem;

    //     EntityQuery collidWithPickableweaponQuery;

    //     protected override void OnCreate()
    //     {
    //         base.OnCreate();
    //         entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

    //     }
    //     protected override void OnUpdate()
    //     {
    //         var cb = entityCommandBufferSystem.CreateCommandBuffer();
    //         var cbp = cb.AsParallelWriter();

    //         Entities
    //         .WithNone<Picked>()
    //         .WithStoreEntityQueryInField(ref collidWithPickableweaponQuery)
    //         .ForEach((int entityInQueryIndex, Entity e, in CollidWithPlayer collidWithPlayer, in PickableWeapon picked) =>
    //         {
    //             cbp.AddComponent<Picked>(entityInQueryIndex, e);
    //             cbp.AddComponent(entityInQueryIndex, e, new HideForSecond { Time = 5f });
    //             // cbp.AddComponent(entityInQueryIndex, collidWithPlayer.Entity, new Equip { Equipable = picked.Entity, SocketType = picked.SocketType });
    //             cbp.RemoveComponent<StatefulTriggerEvent>(entityInQueryIndex, e);

    //         }).ScheduleParallel();

    //         Entities
    //         .WithAll<Picked>()
    //         .WithNone<DisableRendering>()
    //         .ForEach((int entityInQueryIndex, Entity e, in LocalToWorld localToWorld) =>
    //         {
    //             Debug.Log($"{e.Index} was picked");
    //             cbp.AddComponent<DisableRendering>(entityInQueryIndex, e);
    //             cbp.RemoveComponent<StatefulTriggerEvent>(entityInQueryIndex, e);

    //         }).ScheduleParallel();

    //         Entities
    //         .WithAll<Picked, UnHide>()
    //         .ForEach((int entityInQueryIndex, Entity e) =>
    //         {
    //             Debug.Log($"{e.Index} pick up respawn");
    //             cbp.RemoveComponent<DisableRendering>(entityInQueryIndex, e);
    //             cbp.AddBuffer<StatefulTriggerEvent>(entityInQueryIndex, e);
    //             cbp.RemoveComponent<Picked>(entityInQueryIndex, e);
    //         }).ScheduleParallel();

    //         Entities
    //         .WithAll<Picked, LocalToWorld>()
    //         .ForEach((int entityInQueryIndex, Entity e, VisualEffect visualEffect) =>
    //         {
    //             visualEffect.Stop();
    //         })
    //         .WithoutBurst()
    //         .Run();

    //         Entities
    //         .WithAll<Picked, UnHide>()
    //         .ForEach((int entityInQueryIndex, Entity e, VisualEffect visualEffect) =>
    //         {
    //             visualEffect.Play();
    //         })
    //         .WithoutBurst()
    //         .Run();

    //         entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    //     }

    // }
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial class EquipPickedWeaponSystem : SystemBase
    {
        EntityCommandBufferSystem entityCommandBufferSystem;

        EntityQuery fighterEquipQuery;
        EntityQuery unequipWeaponQuery;

        EntityQuery weaponInstanceWithoutParent;
        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            unequipWeaponQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                     typeof(UnEquiped)
                },
                None = new ComponentType[]{
                    typeof(EquippedBy)
                }
            });



        }
        protected override void OnUpdate()
        {
            var cb = entityCommandBufferSystem.CreateCommandBuffer();
            var cbp = cb.AsParallelWriter();


            Entities
            .WithAll<UnEquiped>().ForEach((int entityInQueryIndex, Entity e) =>
            {
                cbp.RemoveComponent<UnEquiped>(entityInQueryIndex, e);
                cbp.RemoveComponent<Equipped>(entityInQueryIndex, e);
                if (HasComponent<InstanciedWeapon>(e))
                {
                    var weaponInstance = GetComponent<InstanciedWeapon>(e);
                    if (weaponInstance.Entity != Entity.Null)
                    {
                        Debug.Log($"Remove weapons {weaponInstance.Entity.Index}");
                        cbp.DestroyEntity(entityInQueryIndex, weaponInstance.Entity);
                    }
                    cbp.RemoveComponent<InstanciedWeapon>(entityInQueryIndex, e);
                }
                var buffer = cbp.AddBuffer<StatsModifier>(entityInQueryIndex, e);
            }).ScheduleParallel();

            Entities
            .WithAll<UnEquiped>()
            .ForEach((int entityInQueryIndex, Entity e, DynamicBuffer<Child> childrens) =>
            {
                cbp.RemoveComponent<Child>(entityInQueryIndex, e);
                for (int i = 0; i < childrens.Length; i++)
                {
                    cbp.DestroyEntity(entityInQueryIndex, childrens[i].Value);
                }
            }).ScheduleParallel();

            Entities
            .WithStoreEntityQueryInField(ref fighterEquipQuery)
            .ForEach((int entityInQueryIndex, Entity e, in Equip picked, in EquipableSockets sockets) =>
            {
                var listSockets = sockets.ToList();
                var unequipWeapon = false;
                //TODO: Should only unequip left hand weapon if equip a weapon in right hand
                for (int i = 0; i < listSockets.Length; i++)
                {
                    if (HasComponent<Equipped>(listSockets[i]))
                    {
                        cbp.AddComponent<UnEquiped>(entityInQueryIndex, listSockets[i]);
                    }
                }
                if (!unequipWeapon)
                {
                    var socket = sockets.GetSocketForType(picked.SocketType);
                    Debug.Log($"Player {e.Index} equip pickup Weapon: ${picked.Equipable.Index} in socket: {socket.Index}");
                    cbp.AddComponent(entityInQueryIndex, socket, new EquipInSocket { Socket = socket, Weapon = picked.Equipable });
                    cbp.RemoveComponent<Equip>(entityInQueryIndex, e);
                    cbp.RemoveComponent<ShootProjectile>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }

    }
}