using Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;


public struct RebuildHierachy : IComponentData
    {
        public float4x4 LocalToWorld;
    }

    public struct IsFollowingTarget : IComponentData { }

    public struct IsLookingAtTarget : IComponentData { }
    public struct FollowedBy : IComponentData
    {
        public Entity Entity;
    }

    public struct LookAtBy : IComponentData
    {
        public Entity Entity;
    }

    public struct CinemachineBrainTag : IComponentData
    {
    }
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    public partial class CinemachineCameraDeclareConversionSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((CinemachineVirtualCamera virtualCamera) =>
            {
                var virtualCameraEntity = TryGetPrimaryEntity(virtualCamera);
                DeclareDependency(virtualCamera.gameObject,virtualCamera);
                if (virtualCameraEntity != Entity.Null)
                {
                    if(virtualCamera.m_Follow != null){
                         DeclareDependency(virtualCamera.gameObject,virtualCamera.m_Follow.gameObject);
                         DeclareDependency(virtualCamera.m_Follow.gameObject,virtualCamera.m_Follow.gameObject.GetComponent<Transform>());
                         DeclareDependency(virtualCamera.gameObject,virtualCamera.m_Follow.gameObject.GetComponent<Transform>());
                        //  DstEntityManager.AddComponentObject(followedEntity,virtualCamera.m_Follow.gameObject.GetComponent<Transform>());
                    }
                }

            });
        }
    }       
    public partial class CinemachineCameraConversionSystem : GameObjectConversionSystem
    {
        // TODO: Clean up put all follow target in a same parent game object
        protected override void OnUpdate()
        {
            // Entities.ForEach((CinemachineBrain brain) =>
            // {
            //     var brainEntity = TryGetPrimaryEntity(brain);
            //     DstEntityManager.AddComponentObject(brainEntity, brain);
            //     DstEntityManager.AddComponentData(brainEntity, new CinemachineBrainTag() { });
            // });

            Entities.ForEach((CinemachineVirtualCamera virtualCamera) =>
            {
                var virtualCameraEntity = TryGetPrimaryEntity(virtualCamera);
                if (virtualCameraEntity != Entity.Null)
                {
                    // DstEntityManager.AddComponentObject(virtualCameraEntity,virtualCamera);
                    // DstEntityManager.AddComponentObject(virtualCameraEntity,virtualCamera.GetComponent<Transform>());
                    DeclareLinkedEntityGroup(virtualCamera.gameObject);
                    LoadCinemachineComponents(virtualCamera);
                    if(virtualCamera.m_Follow != null){
                        //  var followedEntity = TryGetPrimaryEntity(virtualCamera.m_Follow.gameObject);

                        //  DstEntityManager.AddComponentObject(followedEntity,virtualCamera.m_Follow.gameObject.GetComponent<Transform>());
                    }
                    // if (virtualCamera.m_Follow != null)
                    // {
                    //     var followedEntity = TryGetPrimaryEntity(virtualCamera.m_Follow.gameObject);
                    //     if (followedEntity != Entity.Null)
                    //     {
                    //         Debug.Log("Follow " + followedEntity.Index);
                    //         DstEntityManager.AddComponentData(virtualCameraEntity, new Follow() { Entity = followedEntity });
                    //     }
                    //     AddHybridComponent(virtualCamera.m_Follow);
                    // }
                    // if (virtualCamera.m_LookAt != null)
                    // {
                    //     var lookAtEntity = TryGetPrimaryEntity(virtualCamera.m_LookAt.gameObject);
                    //     if (lookAtEntity != Entity.Null)
                    //     {
                    //         Debug.Log("Look At " + lookAtEntity.Index);
                    //         DstEntityManager.AddComponentData(virtualCameraEntity, new LookAt() { Entity = lookAtEntity });
                    //         AddHybridComponent(virtualCamera.m_LookAt);
                    //     }

                    // }
                }

            });
            Entities.ForEach((CinemachinePipeline pipeline) =>
            {
                ConvertPipeline(this, pipeline);
            });
        }

        private void LoadCinemachineComponents(CinemachineVirtualCamera virtualCamera)
        {

            foreach (Transform child in virtualCamera.transform)
            {
                var pipeline = child.GetComponent<CinemachinePipeline>();
                if (pipeline != null)
                {
                    DeclareDependency(virtualCamera.gameObject, pipeline.gameObject);
                    /*      var pipelineEntity = ConvertPipeline(this, pipeline); */

                }
            }


        }

        public static Entity ConvertPipeline(GameObjectConversionSystem conversionsSystem, CinemachinePipeline pipeline)
        {
            var pipelineEntity = conversionsSystem.TryGetPrimaryEntity(pipeline);
            if (pipelineEntity != Entity.Null)
            {
                conversionsSystem.DeclareLinkedEntityGroup(pipeline.gameObject);
                conversionsSystem.DstEntityManager.AddComponentObject(pipelineEntity,pipeline);
                // conversionsSystem.AddHybridComponent(pipeline);
                // conversionsSystem.AddHybridComponent(pipeline.GetComponent<Transform>());
                CinemachineComponentBase[] components = pipeline.GetComponents<CinemachineComponentBase>();

                foreach (CinemachineComponentBase c in components)
                {

                    // conversionsSystem.AddHybridComponent(c);
                    // conversionsSystem.AddHybridComponent(c.GetComponent<Transform>());
                }
                conversionsSystem.DstEntityManager.AddComponentData(pipelineEntity, new RebuildHierachy { LocalToWorld = pipeline.transform.parent.localToWorldMatrix });

            }
            return pipelineEntity;
        }
    }

// [UpdateBefore(typeof(CinemachineVirtualCameraHybridSystem))]
// public class RebuildGameObjectHierachySystem : SystemBase
// {
//     EntityCommandBufferSystem entityCommandBufferSystem;
//     protected override void OnCreate()
//     {
//         base.OnCreate();
//         entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
//     }

//     protected override void OnUpdate()
//     {


//         var em = EntityManager;
//         var commandBuffer = entityCommandBufferSystem.CreateCommandBuffer();
//         Entities
//         .WithChangeFilter<RebuildHierachy>()
//         .ForEach((Entity e, Transform t, in Parent parent, in RebuildHierachy rebuildHierachy) =>
//         {
//             if (em.HasComponent<Transform>(parent.Value))
//             {

//                 var parentTransform = em.GetComponentObject<Transform>(parent.Value);
//                 t.parent = parentTransform;
//                 t.transform.position = rebuildHierachy.LocalToWorld.c3.xyz;
//                 commandBuffer.RemoveComponent<RebuildHierachy>(e);
//                 commandBuffer.RemoveComponent<EditorRenderData>(e);
//             }
//         }).WithoutBurst().Run();
//         entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
//     }
// }