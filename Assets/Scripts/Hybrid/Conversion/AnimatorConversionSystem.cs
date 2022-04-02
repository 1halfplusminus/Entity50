
using UnityEngine;
using Unity.Entities;
using UnityEngine.Animations;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine.Playables;
using Unity.Jobs;
using Unity.Deformations;

public struct Armature : IComponentData
{
    public FixedString32Bytes Name;
}
public struct Bone : IBufferElementData
{
    public FixedString32Bytes Name;
    public Entity Entity;
    public float4x4 BindPose;
    public int ParentIndex;
}
public struct BoneTransform : IComponentData
{
    public Entity Stream;
    public int Index;
    public float4x4 Transform;
}
public struct AnimatedBlendShape : IBufferElementData
{
    public PropertyStreamHandle PropertyStream;
    public int Index;
}
[ChunkSerializable]
public struct PlayableStream : IComponentData
{
    public AnimationStream Stream;
}
[ChunkSerializable]
public struct PlayableGraphComponent : IComponentData
{
    public PlayableGraph Graph;
    public AnimationScriptPlayable ScriptPlayable;
}
public struct AnimatorRef : IComponentData
{
    public Entity Value;
}
public struct NotInitialized : IComponentData
{

}
public class AnimatorConversionSystem : GameObjectConversionSystem
{
    protected override void OnCreate()
    {
        base.OnCreate();
        this.AddTypeToCompanionWhiteList(typeof(Animator));
        // this.AddTypeToCompanionWhiteList(typeof(SkinnedMeshRenderer));
    }
    protected override void OnUpdate()
    {
        Entities.ForEach((Animator animator) =>
        {
            var gameObject = animator.gameObject;
            var renderer = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null)
            {
                return;
            }
            var entity = GetPrimaryEntity(animator);
            DstEntityManager.AddComponentObject(entity, animator);
            DeclareAssetDependency(animator.gameObject, animator.runtimeAnimatorController);
            DeclareDependency(gameObject, renderer);
            var playableStream = new PlayableStream();
            var rendererEntity = GetPrimaryEntity(renderer);
            DstEntityManager.AddComponentData(rendererEntity, new AnimatorRef { Value = entity });
            DstEntityManager.AddComponentData(rendererEntity, playableStream);
            DstEntityManager.AddComponent<PlayableGraphComponent>(rendererEntity);
            // DstEntityManager.AddComponentObject(rendererEntity, renderer);

            var skeleton = renderer.bones;
            var bones = DstEntityManager.AddBuffer<Bone>(rendererEntity);
            bones.ResizeUninitialized(skeleton.Length);
            DeclareLinkedEntityGroup(gameObject);
            DeclareLinkedEntityGroup(renderer.rootBone.parent.gameObject);
            var gameObjectByIndex = new NativeHashMap<int, int>(skeleton.Length, Allocator.Temp);
            var armature = renderer.rootBone.parent;
            var armatureEntity = GetPrimaryEntity(armature);
            DstEntityManager.AddComponentObject(armatureEntity, armature);
            DstEntityManager.AddComponentData(armatureEntity, new Armature { Name = armature.name });
            for (int i = 0; i < skeleton.Length; i++)
            {

                var bone = skeleton[i];
                gameObjectByIndex.Add(bone.gameObject.GetInstanceID(), i);
                var boneEntity = GetPrimaryEntity(bone);
                bones = DstEntityManager.GetBuffer<Bone>(rendererEntity);
                var boneComponent = new Bone
                {
                    Name = bone.name,
                    Entity = boneEntity,
                    BindPose = renderer.sharedMesh.bindposes[i]
                };
                if (bone.transform.parent != null && gameObjectByIndex.ContainsKey(bone.transform.parent.gameObject.GetInstanceID()))
                {
                    boneComponent.ParentIndex = gameObjectByIndex[bone.transform.parent.gameObject.GetInstanceID()];
                }
                else
                {
                    boneComponent.ParentIndex = 0;
                }
                bones[i] = boneComponent;
                DstEntityManager.SetName(boneEntity, bone.name);
                DstEntityManager.AddComponentData(boneEntity, new BoneTransform { Index = i, Stream = rendererEntity });
                DstEntityManager.AddComponentObject(boneEntity, bone);
            }
            gameObjectByIndex.Dispose();
            var animatedBlendShapes = DstEntityManager.AddBuffer<AnimatedBlendShape>(rendererEntity);
            // animatedBlendShapes.ResizeUninitialized(renderer.sharedMesh.blendShapeCount);
            // for (int i = 0; i < Renderer.sharedMesh.blendShapeCount; i++)
            // {
            //     var name = Renderer.sharedMesh.GetBlendShapeName(i);
            //     var animatedBlendShape = new AnimatedBlendShape
            //     {
            //         PropertyStream = Animator.BindStreamProperty(Renderer.transform, typeof(SkinnedMeshRenderer), $"blendShape.{name}"),
            //     };
            //     animatedBlendShapes[i] = animatedBlendShape;
            // }
            DstEntityManager.AddComponent<NotInitialized>(rendererEntity);
        });
    }
}

// [UpdateBefore(typeof(CompanionGameObjectUpdateTransformSystem))]
// public partial class CopyBoneTransformSystem : SystemBase
// {
//     protected override void OnUpdate()
//     {
//         var allBones = GetBufferFromEntity<Bone>(true);

//         Entities
//        .WithReadOnly(allBones)
//        .ForEach((
//        int entityInQueryIndex,
//        Entity e,
//        ref Translation t,
//        ref Rotation r,
//        ref LocalToWorld localToWorld,
//        ref BoneTransform bone
//        ) =>
//        {
//            var currentBones = allBones[bone.Stream];
//            var stream = GetComponent<PlayableStream>(bone.Stream);
//            if (stream.Stream.isValid)
//            {
//                var currentStream = stream.Stream;
//                var transformHandle = currentBones[bone.Index].TransformHandle;
//                transformHandle.Resolve(currentStream);
//                var localPosition = transformHandle.GetLocalPosition(currentStream);
//                var localRotation = transformHandle.GetLocalRotation(currentStream);
//                var localScale = transformHandle.GetLocalScale(currentStream);
//                t.Value = localPosition;
//                r.Value = localRotation;
//                bone.Transform = float4x4.TRS(
//                        localPosition,
//                        localRotation,
//                        new float3(1.0f, 1.0f, 1.0f));
//                // scale.Value = localScale;
//                // localToParent.Value = math.float4x4(math.RigidTransform(localRotation, localPosition));

//                Debug.Log(currentStream.deltaTime);
//            }
//        }).WithoutBurst().Run();
//     }
// }

[UpdateAfter(typeof(CompanionGameObjectUpdateTransformSystem))]
public partial class AnimatorSystem : SystemBase
{
    public struct ProcessBoneJob : IAnimationJob
    {

        public void ProcessRootMotion(AnimationStream stream)
        {

        }

        public void ProcessAnimation(AnimationStream stream)
        {

        }
    }
    EntityCommandBufferSystem entityCommandBufferSystem;

    protected override void OnCreate()
    {
        base.OnCreate();
        entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override void OnUpdate()
    {

        // Entities
        // .WithNone<NotInitialized>()
        // .ForEach((
        //     ref DynamicBuffer<BlendShapeWeight> blendShapeWeights,
        //     in DynamicBuffer<AnimatedBlendShape> animatedBlendShapes,
        //     in PlayableStream stream) =>
        // {
        //     for (int i = 0; i < animatedBlendShapes.Length; i++)
        //     {
        //         var animatedBlendShape = animatedBlendShapes[i];
        //         if (stream.Stream.isValid)
        //         {
        //             if (animatedBlendShape.PropertyStream.IsValid(stream.Stream))
        //             {
        //                 if (!animatedBlendShape.PropertyStream.IsResolved(stream.Stream))
        //                 {
        //                     animatedBlendShape.PropertyStream.Resolve(stream.Stream);
        //                 }
        //                 var value = animatedBlendShape.PropertyStream.GetFloat(stream.Stream);
        //                 blendShapeWeights[i] = new BlendShapeWeight { Value = value };

        //             }
        //         }
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
                    rootMatrixInv = math.inverse(math.mul(localToWorld.Value, boneLocalToParent.Value));
                }
                var boneMatRootSpace = math.mul(rootMatrixInv, boneLocalToWorld.Value);
                var skinMatRootSpace = math.mul(boneMatRootSpace, bindPose);

                skinMatrices[i] = new SkinMatrix { Value = new float3x4(skinMatRootSpace.c0.xyz, skinMatRootSpace.c1.xyz, skinMatRootSpace.c2.xyz, skinMatRootSpace.c3.xyz) };

            }

        }).ScheduleParallel();
        var delta = Time.DeltaTime;

        Entities
        .WithAll<NotInitialized>()
        .ForEach((
            Entity e,
            ref PlayableGraphComponent playableGraph,
            in AnimatorRef animatorRef
            ) =>
        {
            var instance = EntityManager.GetComponentObject<Animator>(animatorRef.Value);
            var nbBones = GetBuffer<Bone>(e).Length;
            // var job = new ProcessBoneJob
            // { };
            var animationControllerPlayable = AnimationPlayableUtilities.PlayAnimatorController(instance, instance.runtimeAnimatorController, out playableGraph.Graph);
            // var customMixerPlayable = AnimationScriptPlayable.Create(playableGraph.Graph, job);
            // var output = AnimationPlayableOutput.Create(playableGraph.Graph, "output", instance);
            // output.SetSourcePlayable(customMixerPlayable);
            playableGraph.Graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playableGraph.Graph.Play();
        }).WithoutBurst().Run();

        Entities
        .WithAll<NotInitialized>()
        .ForEach((
            Entity e,
            // SkinnedMeshRenderer renderer,
            ref PlayableStream playableStream,
            in AnimatorRef animatorRef
            ) =>
        {
            var instance = EntityManager.GetComponentObject<Animator>(animatorRef.Value);
            var nbBones = GetBuffer<Bone>(e).Length;
           
            for (int i = 0; i < GetBuffer<Bone>(e).Length; i++)
            {
                var bone = GetBuffer<Bone>(e, true)[i];
                var boneEntity = GetBuffer<Bone>(e, true)[i].Entity;
                var parentIndex = GetBuffer<Bone>(e, true)[i].ParentIndex;
                var name = GetBuffer<Bone>(e, true)[i].Name.ToString();
                var localToWorld = GetComponent<LocalToWorld>(boneEntity);
                var boneCompanionLink = EntityManager.GetComponentObject<CompanionLink>(boneEntity);
                boneCompanionLink.Companion.transform.name = name;
                if (i == 0)
                {
                    var armatureEntity = EntityManager.GetComponentData<Parent>(boneEntity).Value;
                    var armature = EntityManager.GetComponentData<Armature>(armatureEntity);
                    var armatureGO = EntityManager.GetComponentObject<CompanionLink>(armatureEntity).Companion;
                    armatureGO.name = armature.Name.ToString();
                    armatureGO.transform.parent = instance.transform;
                    armatureGO.SetActive(true);
                    boneCompanionLink.Companion.transform.parent = armatureGO.transform;
                }
                else
                {
                    var parentEntity = GetBuffer<Bone>(e, true)[parentIndex].Entity;
                    var parentCompanionLink = EntityManager.GetComponentObject<CompanionLink>(parentEntity);
                    boneCompanionLink.Companion.transform.parent = parentCompanionLink.Companion.transform;
                }
                EntityManager.AddComponentObject(boneEntity, boneCompanionLink.Companion.transform);
                boneCompanionLink.Companion.SetActive(true);
                EntityManager.AddComponent<CopyTransformFromGameObject>(boneEntity);
            }
            // renderer.transform.parent = instance.transform;
            // var animatedBlendShapes = EntityManager.AddBuffer<AnimatedBlendShape>(e);
            // animatedBlendShapes.ResizeUninitialized(renderer.sharedMesh.blendShapeCount);
            // for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
            // {
            //     var name = renderer.sharedMesh.GetBlendShapeName(i);
            //     var animatedBlendShape = new AnimatedBlendShape
            //     {
            //         PropertyStream = instance.BindStreamProperty(renderer.transform, typeof(SkinnedMeshRenderer), $"blendShape.{name}"),
            //     };
            //     animatedBlendShapes[i] = animatedBlendShape;
            // }
            instance.gameObject.SetActive(true);
            EntityManager.RemoveComponent<NotInitialized>(e);
            EntityManager.RemoveComponent<CompanionLink>(e);
            instance.OpenAnimationStream(ref playableStream.Stream);
        }).WithStructuralChanges().Run();

        // Entities
        // .ForEach((
        //     Entity e,
        //     ref PlayableGraphComponent playableGraph,
        //     in AnimatorRef animatorRef
        //     ) =>
        // {
        //     var numberOfBone = GetBuffer<Bone>(e, true).Length;
        //     var mixerJob = playableGraph.ScriptPlayable.GetJobData<MixerJob>();
        //     mixerJob.bones.Dispose();
        //     mixerJob.bones =  GetBuffer<Bone>(e, true).ToNativeArray(Allocator.TempJob);
        //     mixerJob.positions.Dispose();
        //     mixerJob.positions = new NativeArray<float3>
        //         (numberOfBone,
        //         Allocator.TempJob,
        //         NativeArrayOptions.UninitializedMemory);
        //     playableGraph.ScriptPlayable.SetJobData(mixerJob);
        // }).WithoutBurst().Run();
        entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}