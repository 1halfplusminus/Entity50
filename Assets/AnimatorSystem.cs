using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Unity.Collections;

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class AnimatorSystem : SystemBase
{
    EntityCommandBufferSystem entityCommandBufferSystem;
    protected override void OnCreate()
    {
        base.OnCreate();
        entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
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
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var destroyWithEntity = instance.gameObject.AddComponent<DestroyWithEntity>();
            destroyWithEntity.entity = e;
            for(int i = 0; i < GetBuffer<Bone>(e).Length; i++){
                EntityManager.AddComponentObject(GetBuffer<Bone>(e)[i].Entity,renderer.bones[i]);
            }
            var animatedBlendShapes = EntityManager.AddBuffer<AnimatedBlendShape>(e);
            animatedBlendShapes.ResizeUninitialized(renderer.sharedMesh.blendShapeCount);
            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
            {
                var name = renderer.sharedMesh.GetBlendShapeName(i);
                var animatedBlendShape = new AnimatedBlendShape
                {
                    PropertyStream = instance.BindStreamProperty(renderer.transform, typeof(SkinnedMeshRenderer), $"blendShape.{name}"),
                };
                animatedBlendShapes[i] = animatedBlendShape;
            }
            EntityManager.AddComponentObject(e,instance);
            EntityManager.AddComponentObject(e,instance.transform);
            EntityManager.AddComponent<CopyTransformToGameObject>(e);
            EntityManager.RemoveComponent<NotInitialized>(e);
            instance.OpenAnimationStream(ref playableStream.Stream);
            Object.Destroy(renderer.gameObject);
        }).WithStructuralChanges().Run();
     
        entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
