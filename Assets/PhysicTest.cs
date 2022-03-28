using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.Systems;
using UnityEngine;
using Unity.Burst;
[BurstCompile]
struct CollisionJob : ICollisionEventsJob
{
    [ReadOnly]
    public ComponentDataFromEntity<PlayerTag> players;
    public EntityCommandBuffer cb;
    public void Execute(CollisionEvent collisionEvent)
    {
        Entity entityA = collisionEvent.EntityA;
        Entity entityB = collisionEvent.EntityB;
        if(players.HasComponent(entityA)){
            cb.DestroyEntity(entityA);
        }
        if(players.HasComponent(entityB)){
            cb.DestroyEntity(entityB);
        }
        Debug.Log($"entity {entityA.Index} collid with {entityB.Index}");
    }
}
[BurstCompile]
struct PickupOnTriggerSystemJob : ITriggerEventsJob
{
    //    [ReadOnly] public ComponentDataFromEntity<PickupTag> allPickups;
    //    [ReadOnly] public ComponentDataFromEntity<PlayerTag> allPlayers;
    //    public EntityCommandBuffer entityCommandBuffer;

    public void Execute(TriggerEvent triggerEvent)
    {
        Entity entityA = triggerEvent.EntityA;
        Entity entityB = triggerEvent.EntityB;
        Debug.Log($"entity {entityA.Index} trigger with {entityB.Index}");
        //    if (allPickups.HasComponent(entityA) && allPickups.HasComponent(entityB))
        //        return;

        //    if (allPickups.HasComponent(entityA) && allPlayers.HasComponent(entityB))
        //        entityCommandBuffer.DestroyEntity(entityA);
        //    else if (allPlayers.HasComponent(entityA) && allPickups.HasComponent(entityB))
        //        entityCommandBuffer.DestroyEntity(entityB);
    }
}
public struct AttractorData : IComponentData
{

    public float MaxDistanceSqrd;

    public float Strengh;
}
public partial class AttractSystem : SystemBase
{
    public float3 center;
    public float maxDistanceSqrd;
    public float strength;

    private EntityQuery attractorQuery;
    StepPhysicsWorld stepPhysicsWorld;

    EntityCommandBufferSystem commandBufferSystem;
    protected override void OnCreate()
    {
        base.OnCreate();
        stepPhysicsWorld = World.GetOrCreateSystem<StepPhysicsWorld>();
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        this.RegisterPhysicsRuntimeSystemReadOnly();
    }
    protected override void OnUpdate()
    {

        var attractors = new NativeList<(AttractorData Attraction, float3 Center)>(attractorQuery.CalculateEntityCount(), Allocator.TempJob);
        var pw = attractors.AsParallelWriter();

        Entities
        .WithStoreEntityQueryInField(ref attractorQuery)
        .ForEach((in AttractorData data, in Translation translation) =>
        {
            pw.AddNoResize((data, translation.Value));
        }).ScheduleParallel();

        Entities
        .WithReadOnly(attractors)
        .WithDisposeOnCompletion(attractors)
        .ForEach((ref PhysicsVelocity velocity, ref Translation position, ref Rotation rotation) =>
        {
            for (int i = 0; i < attractors.Length; i++)
            {
                var attractionData = attractors[i].Attraction;
                float3 diff = attractors[i].Center - position.Value;
                float distSqrd = math.lengthsq(diff);
                if (distSqrd < attractionData.MaxDistanceSqrd)
                {
                    velocity.Linear = attractionData.Strengh * (diff / math.sqrt(distSqrd));
                }
            }
        }).ScheduleParallel();
        var cb = commandBufferSystem.CreateCommandBuffer();
        var players = GetComponentDataFromEntity<PlayerTag>(true);
        // new PickupOnTriggerSystemJob{}.Schedule(stepPhysicsWorld.Simulation,Dependency);
        var job = new CollisionJob{
            cb = cb,
            players =players
        }.Schedule(stepPhysicsWorld.Simulation,Dependency);
        Dependency = job;
        commandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
