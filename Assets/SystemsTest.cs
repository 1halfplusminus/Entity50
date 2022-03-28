using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial  struct  CopyPositionJob: IJobEntity{
    public NativeArray<float3> CopyPositions;

   public void Execute([EntityInQueryIndex] int entityInQueryIndex, in LocalToWorld localToWorld)
    {
        CopyPositions[entityInQueryIndex] = localToWorld.Position;
    }

}

public partial class EntityInQuerySystem : SystemBase
{
    EntityQuery query;   
    protected override void OnCreate()
    {
        base.OnCreate();
        query = GetEntityQuery(ComponentType.ReadOnly<LocalToWorld>());
    }
    protected override void OnUpdate()
    {
        
        // var positions = new NativeArray<float3>(query.CalculateEntityCount(),Allocator.TempJob);

        // new CopyPositionJob{CopyPositions = positions}.ScheduleParallel(query);

        // positions.Dispose(Dependency);
    }
}