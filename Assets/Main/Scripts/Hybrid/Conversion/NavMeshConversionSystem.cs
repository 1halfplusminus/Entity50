using UnityEngine.AI;
using RPG.Mouvement;
using Unity.AI.Navigation;
using Unity.Entities;
using UnityEngine;

public class NavMeshAgentConversionSystem : GameObjectConversionSystem
{
    protected override void OnCreate()
    {
        base.OnCreate();
        this.AddTypeToCompanionWhiteList(typeof(MeshFilter));
        this.AddTypeToCompanionWhiteList(typeof(NavMeshSurface));
        this.AddTypeToCompanionWhiteList(typeof(NavMeshAgent));
        this.AddTypeToCompanionWhiteList(typeof(NavMeshObstacle));
    }
    protected override void OnUpdate()
    {
        Entities.ForEach((NavMeshSurface surface) =>
       {
           var entity = GetPrimaryEntity(surface);
           DstEntityManager.AddComponentObject(entity,surface);
           DstEntityManager.AddComponentObject(entity,surface.GetComponent<MeshFilter>());
           if (surface.navMeshData)
           {
               DeclareAssetDependency(surface.gameObject, surface.navMeshData);
           }
       });
        Entities.ForEach((NavMeshObstacle obstacle) =>
        {
            var entity = GetPrimaryEntity(obstacle);
            DstEntityManager.AddComponentObject(entity, obstacle);
        });
        Entities.ForEach((NavMeshAgent agent) =>
        {
            var entity = GetPrimaryEntity(agent);
            DstEntityManager.AddComponentObject(entity,agent);
            DstEntityManager.AddComponentData(entity, new Mouvement { Speed = agent.speed });
            DstEntityManager.AddComponentData(entity, new MoveTo(agent.transform.position) { StoppingDistance = agent.stoppingDistance });
        });
    }
}

public partial class InitialiseNavMeshSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithChangeFilter<NavMeshSurface>().ForEach((NavMeshSurface surface) =>
        {
            surface.gameObject.SetActive(true);
            surface.enabled = true;
            surface.RemoveData();
            surface.AddData();
            NavMesh.AddNavMeshData(surface.navMeshData);
        }).WithoutBurst().Run();
    }
}