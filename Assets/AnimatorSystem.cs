using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Unity.Collections;
using Unity.NetCode;
using Unity.Rendering;

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public partial class AnimatorSystem : SystemBase
{
    EntityCommandBufferSystem entityCommandBufferSystem;
    EntityQuery bonesQuery;
    protected override void OnCreate()
    {
        base.OnCreate();
        entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        bonesQuery = GetEntityQuery(ComponentType.ReadOnly<BoneTransform>());
    }
    protected override void OnUpdate()
    {
        Entities
        .WithNone<NotInitialized>()
        .ForEach((
            ref DynamicBuffer<BlendShapeWeight> blendShapeWeights,
            in DynamicBuffer<AnimatedBlendShape> animatedBlendShapes, 
            in PlayableStream stream) =>
        {
            for (int i = 0; i < animatedBlendShapes.Length; i++)
            {
                var animatedBlendShape = animatedBlendShapes[i];
                if (stream.Stream.isValid)
                {
                    if (animatedBlendShape.PropertyStream.IsValid(stream.Stream))
                    {
                        if (!animatedBlendShape.PropertyStream.IsResolved(stream.Stream))
                        {
                            animatedBlendShape.PropertyStream.Resolve(stream.Stream);
                        }
                        var value = animatedBlendShape.PropertyStream.GetFloat(stream.Stream);
                        blendShapeWeights[i] = new BlendShapeWeight { Value = value };
                       
                    }
                }
            }
        }).ScheduleParallel();
        // var allBones = GetBufferFromEntity<Bone>(true);
        // Entities
        // .WithReadOnly(bones)
        // .ForEach((
        // int entityInQueryIndex,
        // Entity e,
        // ref LocalToParent localToParent,
        // in BoneTransform bone
        // ) =>
        // {
        //     var currentBones = bones[bone.Stream];
        //     var stream = GetComponent<PlayableStream>(bone.Stream);
        //     if(stream.Stream.isValid){
        //         var transformHandle = currentBones[bone.Index].TransformHandle;
        //         transformHandle.Resolve(stream.Stream);
        //         var localPosition = transformHandle.GetLocalPosition(stream.Stream);
        //         var localRotation = transformHandle.GetLocalRotation(stream.Stream);
        //         localToParent.Value = math.float4x4(math.RigidTransform(localRotation, localPosition));
        //     }
        // }).ScheduleParallel();
        Entities
        .WithNone<NotInitialized>()
        .ForEach((ref DynamicBuffer<SkinMatrix> skinMatrices, in DynamicBuffer<Bone> bones, in LocalToWorld localToWorld, in PlayableStream stream) =>
        {
            var rootMatrixInv = math.inverse(localToWorld.Value);
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                var bindPose = bone.BindPose;
                var boneLocalToWorld = GetComponent<LocalToWorld>(bone.Entity);

                if (i == 0)
                {
                    var boneLocalToParent = GetComponent<LocalToParent>(bone.Entity);
                    rootMatrixInv = math.inverse(math.mul(localToWorld.Value,boneLocalToParent.Value));
                }
                var boneMatRootSpace = math.mul(rootMatrixInv, boneLocalToWorld.Value);
                var skinMatRootSpace = math.mul(boneMatRootSpace, bindPose);

                skinMatrices[i] = new SkinMatrix { Value = new float3x4(skinMatRootSpace.c0.xyz, skinMatRootSpace.c1.xyz, skinMatRootSpace.c2.xyz, skinMatRootSpace.c3.xyz) };

            }

        }).ScheduleParallel();
        var delta = Time.DeltaTime;

        Entities
        .WithAll<NotInitialized>().ForEach((
            Entity e,
            ref PlayableStream playableStream,
            in AnimatorRef animatorRef
            )=>{

            // var bones = allBones[rendererRef.Value];
            var animator = EntityManager.GetComponentObject<Animator>(animatorRef.Value);
            var instance = animator.gameObject.activeInHierarchy ? animator :  Object.Instantiate(animator);
            // var renderer = new SkinnedMeshRenderer();
            // renderer.sharedMesh = renderMesh.mesh;
            // var destroyWithEntity = instance.gameObject.AddComponent<DestroyWithEntity>();
            // destroyWithEntity.entity = e;
        
            for(int i = 0; i < GetBuffer<Bone>(e).Length; i++){
                var bone = new GameObject();
                var boneLocalToWorld = EntityManager.GetComponentObject<BoneAuthoring>(GetBuffer<Bone>(e)[i].Entity);
                // bone.transform.name = GetBuffer<Bone>(e)[i].Name.ToString();
                // bones[i] = bone.transform;
                // bones[i].localPosition = boneLocalToWorld.Position;
                // bones[i].localRotation = boneLocalToWorld.Rotation;
                if(GetBuffer<Bone>(e)[i].ParentIndex == 0){
                   boneLocalToWorld.Transform.parent = animator.transform;
                } else {
                    // bones[i].parent = bones[GetBuffer<Bone>(e)[i].ParentIndex];
                }

                // EntityManager.AddComponentObject(GetBuffer<Bone>(e)[i].Entity,bones[i]);
            }
            // var animatedBlendShapes = EntityManager.AddBuffer<AnimatedBlendShape>(e);
            // animatedBlendShapes.ResizeUninitialized(renderMesh.mesh.blendShapeCount);
            // for (int i = 0; i < renderMesh.mesh.blendShapeCount; i++)
            // {
            //     var name = renderMesh.mesh.GetBlendShapeName(i);
            //     var animatedBlendShape = new AnimatedBlendShape
            //     {
            //         PropertyStream = instance.BindStreamProperty(renderer.transform, typeof(SkinnedMeshRenderer), $"blendShape.{name}"),
            //     };
            //     animatedBlendShapes[i] = animatedBlendShape;
            // }
            EntityManager.AddComponentObject(e,instance);
            EntityManager.AddComponentObject(e,instance.transform);
            EntityManager.AddComponent<CopyTransformToGameObject>(e);
            EntityManager.RemoveComponent<NotInitialized>(e);
            instance.OpenAnimationStream(ref playableStream.Stream);
            // Object.Destroy(renderer.gameObject);
        }).WithStructuralChanges().Run();
     
        entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
