
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine.AI;
using Unity.Mathematics;
using RPG.Core;


namespace RPG.Mouvement
{
    [UpdateInGroup(typeof(MouvementSystemGroup))]
    public partial class IsMovingSystem : SystemBase
    {
        EntityCommandBufferSystem entityCommandBufferSystem;

        EntityQuery isMovingQuery;
        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        protected override void OnUpdate()
        {
            var commandBuffer = entityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            Entities
            .WithStoreEntityQueryInField(ref isMovingQuery)
            // .WithChangeFilter<MoveTo>()
            .WithNone<IsDeadTag, IsMoving>()
            .ForEach((int entityInQueryIndex, Entity e, in MoveTo moveTo) =>
            {
                if (!moveTo.Stopped)
                {
                    commandBuffer.AddComponent<IsMoving>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
    //FIXME: split in smaller system
    [UpdateInGroup(typeof(MouvementSystemGroup))]
    [UpdateAfter(typeof(IsMovingSystem))]
    public partial class MoveToSystem : SystemBase
    {
        EntityCommandBufferSystem entityCommandBufferSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        protected override void OnUpdate()
        {

            var commandBuffer = entityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            // Calcule distance 
             Entities
            .WithChangeFilter<MoveTo>()
            .WithNone<IsDeadTag>()
            .WithAny<IsMoving>()
            .ForEach((int entityInQueryIndex, Entity e, ref MoveTo moveTo, in Translation t) =>
            {
                if (!moveTo.UseDirection)
                {
                    moveTo.Distance = math.distance(moveTo.Position, t.Value);
                    if (moveTo.Distance <= moveTo.StoppingDistance)
                    {
                        moveTo.Stopped = true;
                        moveTo.Position = t.Value;
                    }
                }

            }).ScheduleParallel();

            Entities
        //    .WithChangeFilter<MoveTo>()
           .WithAny<IsMoving>()
           .WithNone<IsDeadTag>()
           .ForEach((int entityInQueryIndex, Entity e, ref Mouvement mouvement, in MoveTo moveTo) =>
           {
               if (moveTo.Stopped)
               {
                   commandBuffer.RemoveComponent<IsMoving>(entityInQueryIndex, e);
                   mouvement.Velocity = new Velocity { Linear = float3.zero, Angular = float3.zero };
               }

           }).ScheduleParallel();


            // Initialize move to when spawned
            Entities
            .WithAll<Spawned>()
            .ForEach((ref Translation position, ref MoveTo moveTo) =>
            {
                moveTo.Position = position.Value;
            }).ScheduleParallel();

            Entities
            .WithAll<WarpTo, Warped>()
            .ForEach((int entityInQueryIndex, Entity e) =>
            {
                commandBuffer.RemoveComponent<WarpTo>(entityInQueryIndex, e);
                commandBuffer.RemoveComponent<Warped>(entityInQueryIndex, e);
            }).ScheduleParallel();

            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);

        }
    }
    [UpdateInGroup(typeof(MouvementSystemGroup))]
    [UpdateAfter(typeof(MoveToSystem))]
    public partial class MoveToNavMeshAgentSystem : SystemBase
    {
        EntityCommandBufferSystem entityCommandBufferSystem;
        EntityQuery navMeshAgentQueries;
        protected override void OnCreate()
        {
            base.OnCreate();
            entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        protected override void OnUpdate()
        {

            // TODO : Refractor get in paralle
            var lookAts = GetComponentDataFromEntity<LookAt>(true);

            var commandBuffer = entityCommandBufferSystem.CreateCommandBuffer();
            // Warp when spawned or warp to
            Entities.WithoutBurst()
            .WithAny<Spawned, WarpTo>()
            .ForEach((Entity e, NavMeshAgent agent, ref MoveTo moveTo, in Translation position) =>
            {
                if (agent.isOnNavMesh)
                {
                    if (agent.Warp(position.Value))
                    {
                        moveTo.Position = position.Value;
                        commandBuffer.AddComponent<Warped>(e);
                    }
                }

            }).Run();
            var dt = Time.DeltaTime;
            Entities
            .WithAny<IsMoving>()
            .WithNone<IsDeadTag, WarpTo>()
            .WithChangeFilter<MoveTo>()
            .WithReadOnly(lookAts)
            .WithStoreEntityQueryInField(ref navMeshAgentQueries)
            .ForEach((Entity e, NavMeshAgent agent,
            ref Translation position,
            ref Mouvement mouvement,
            ref MoveTo moveTo,
            ref Rotation rotation,
            in LocalToWorld localToWorld) =>
            {
                if (agent.isOnNavMesh)
                {
                    bool isDirection = moveTo.UseDirection;
                    agent.speed = moveTo.CalculeSpeed(in mouvement);
                    if (!isDirection)
                    {
                        agent.SetDestination(moveTo.Position);
                        if (!lookAts.HasComponent(e) || lookAts[e].Entity == Entity.Null)
                        {
                            rotation.Value = agent.transform.rotation;
                        }
                        mouvement.Velocity = new Velocity
                        {
                            Linear = agent.transform.InverseTransformDirection(agent.velocity),
                            Angular = agent.angularSpeed

                        };
                    }
                    else
                    {
                        moveTo.Direction = math.normalizesafe(moveTo.Direction);
                        // agent.destination = (float3)agent.transform.position + moveTo.Direction;
                        var newPosition = moveTo.Direction + (agent.stoppingDistance * math.sign(moveTo.Direction));
                        NavMesh.SamplePosition((float3)agent.transform.position + newPosition, out var hit, 10f, NavMesh.AllAreas);
                        var path = new NavMeshPath();
                        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                        agent.CalculatePath(hit.position, path);
                        agent.path = path;
                        agent.destination = agent.pathEndPosition;
                        agent.Move((agent.transform.position - agent.nextPosition) * dt);
                        moveTo.Position = agent.transform.position;
                        rotation.Value = agent.transform.rotation;
                        moveTo.Direction = float3.zero;
                        moveTo.UseDirection = false;
                        mouvement.Velocity = new Velocity
                        {
                            Linear = new float3(0, 0, 1f) * mouvement.Speed,
                            Angular = agent.angularSpeed
                        };
                    }

                    position.Value = agent.transform.position;


                } else {
                    agent.enabled = true;
                    agent.gameObject.SetActive(true);
                }

            }).WithoutBurst().Run();

            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }

}