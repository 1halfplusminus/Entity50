
using UnityEngine;
using Unity.Entities;
using UnityEngine.Animations;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine.Playables;
using Unity.Jobs;
using Unity.Deformations;
using UnityEngine.Jobs;
using Unity.Burst;



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

    public float4x4 Value;
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
        this.AddTypeToCompanionWhiteList(typeof(SkinnedMeshRenderer));
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
            DstEntityManager.AddComponent<NotInitialized>(rendererEntity);
        });
    }
}



[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class InitializationAnimatorSystem : SystemBase{
    protected override void OnUpdate()
    {
        
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

            var animationControllerPlayable = AnimationPlayableUtilities.PlayAnimatorController(instance, instance.runtimeAnimatorController, out playableGraph.Graph);
          
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
            EntityManager.AddComponentObject(animatorRef.Value,instance);
            EntityManager.AddComponentObject(animatorRef.Value,instance.transform);

            var nbBones = GetBuffer<Bone>(e).Length;
            for (int i = 0; i < GetBuffer<Bone>(e).Length; i++)
            {
                var bone = GetBuffer<Bone>(e, true)[i];
                var boneEntity = GetBuffer<Bone>(e, true)[i].Entity;
                var parentIndex = GetBuffer<Bone>(e, true)[i].ParentIndex;
                var name = GetBuffer<Bone>(e, true)[i].Name.ToString();
                var boneCompanionLink = EntityManager.GetComponentObject<CompanionLink>(boneEntity);
                var boneGo = boneCompanionLink.Companion;
                boneGo.transform.name = name;
                if (i == 0)
                {
                    var armatureEntity = EntityManager.GetComponentData<Parent>(boneEntity).Value;
                    var armature = EntityManager.GetComponentData<Armature>(armatureEntity);
                    var armatureGO =   EntityManager.GetComponentObject<CompanionLink>(armatureEntity).Companion;
                    
                    armatureGO.name = armature.Name.ToString();
                    armatureGO.transform.parent = instance.transform;
                    // armatureGO.SetActive(true);
                    EntityManager.AddComponentObject(armatureEntity,armatureGO.transform);
                    boneGo.transform.parent = armatureGO.transform;
                    // EntityManager.RemoveComponent<Translation>(armatureEntity);
                    // EntityManager.RemoveComponent<Rotation>(armatureEntity);
                }
                else
                {
                    var parentEntity = GetBuffer<Bone>(e, true)[parentIndex].Entity;
                    var parentTransform = EntityManager.GetComponentObject<Transform>(parentEntity);
                    boneGo.transform.parent = parentTransform;
                    // EntityManager.RemoveComponent<LocalToWorld>(boneEntity);
                }
                EntityManager.RemoveComponent<Translation>(boneEntity);
                EntityManager.RemoveComponent<Rotation>(boneEntity);
                EntityManager.AddComponentObject(boneEntity, boneGo.transform);
             
                // boneCompanionLink.Companion.SetActive(true);
                instance.BindStreamTransform(boneGo.transform);
                // EntityManager.AddComponent<CopyInitialTransformFromGameObject>(boneEntity);

                // EntityManager.AddComponent<CopyInitialTransformFromGameObject>(boneEntity);
                // EntityManager.AddComponent<CopyTransformFromGameObject>(boneEntity);
            }
            // renderer.transform.parent = instance.transform;
            // var animatedBlendShapes = EntityManager.AddBuffer<AnimatedBlendShape>(e);
            // animatedBlendShapes.ResizeUninitialized(renderer.sharedMesh.blendShapeCount);
            // for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
            // {
            //     renderer.transform.name = renderer.transform.name.Replace("(Clone)","").Trim();;
            //     var name = renderer.sharedMesh.GetBlendShapeName(i);
            //     var animatedBlendShape = new AnimatedBlendShape
            //     {
            //         PropertyStream = instance.BindStreamProperty(renderer.transform, typeof(SkinnedMeshRenderer), $"blendShape.{name}"),
            //     };
            //     animatedBlendShapes[i] = animatedBlendShape;
            // }
            instance.gameObject.SetActive(true);
            EntityManager.RemoveComponent<NotInitialized>(e);
            // EntityManager.RemoveComponent<CompanionLink>(e);
            // EntityManager.RemoveComponent<SkinnedMeshRenderer>(e);
            instance.OpenAnimationStream(ref playableStream.Stream);
            // Object.Destroy(renderer.gameObject);
        }).WithStructuralChanges().Run();

    }
}
[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(CompanionGameObjectUpdateTransformSystem))]
public partial class CopyBoneTransformSystem : SystemBase
{
    struct TransformStash
    {
        public float3 localPosition;
        public quaternion localRotation;
       
        public float3 position;
        public quaternion rotation;
        public float3 localScale;

        public float4x4 localToWorld;

        public float4x4 worldToLocal;

    }

    [BurstCompile]
    struct StashTransforms : IJobParallelForTransform
    {
        public NativeArray<TransformStash> transformStashes;

        public void Execute(int index, TransformAccess transform)
        {
            var rotation =(quaternion) transform.rotation;
            var position = (float3) transform.position;
            var localToWorld = float4x4.TRS(position,rotation,new float3(1.0f,1.0f,1.0f));
            // transform.localScale = new float3(1.0f,1.0f,1.0f);
            transformStashes[index] = new TransformStash
            {
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = new float3(1.0f,1.0f,1.0f),
                rotation = transform.rotation,
                position = transform.position,
                localToWorld = transform.localToWorldMatrix,
                worldToLocal = transform.worldToLocalMatrix
            };
        }
    }
    [BurstCompile]
    partial struct CopyStash : IJobEntity
    {
        [DeallocateOnJobCompletion]
        public NativeArray<TransformStash> transformStashes;

        public void Execute([EntityInQueryIndex]int entityInQueryIndex, ref BoneTransform b, ref LocalToWorld localToWorld)
        {
            var transform = transformStashes[entityInQueryIndex];
            var rotation =transform.rotation;
            var position =transform.position;
            var test = float4x4.TRS(position,rotation,new float3(1.0f,1.0f,1.0f));
            localToWorld.Value = test;
            b.Value =  transformStashes[entityInQueryIndex].localToWorld;
        }
    }
    EntityQuery transformQuery;
    protected override void OnCreate(){
        base.OnCreate();
        transformQuery = GetEntityQuery(
            typeof(Transform),
            ComponentType.ReadWrite<BoneTransform>(),
            ComponentType.ReadWrite<LocalToWorld>()
        );
    }
    protected override void OnUpdate()
    {
        var transformArrayAccess = transformQuery.GetTransformAccessArray();
        var transformStashes = new NativeArray<TransformStash>(transformQuery.CalculateEntityCount(),Allocator.TempJob);
        var stashTransformJob = new StashTransforms{
            transformStashes = transformStashes
        };
        Dependency = JobHandle.CombineDependencies(stashTransformJob.Schedule(transformArrayAccess),Dependency);
        Dependency = new CopyStash{
            transformStashes = transformStashes
        }.Schedule(transformQuery,Dependency);
    //     var localToWorlds = GetComponentDataFromEntity<LocalToWorld>(true);
    //     Entities
    //     .WithReadOnly(localToWorlds)
    //     .WithReadOnly(transformStashes)
    //     .WithDisposeOnCompletion(transformStashes)
    //    .ForEach((
    //    int entityInQueryIndex,
    //    Entity e,
    //    ref BoneTransform bone,
    //    ref LocalToWorld localToWorld,
    //    ref Translation t,
    //    ref Rotation r,
    //    ref LocalToParent localToParent
    //    ) =>
    //    {

    //     // //    t.Value =  transformStashes[entityInQueryIndex].localPosition;
    //     // //    r.Value = transformStashes[entityInQueryIndex].localRotation;
    //     localToParent.Value = float4x4.TRS(transformStashes[entityInQueryIndex].localPosition,transformStashes[entityInQueryIndex].localRotation, transformStashes[entityInQueryIndex].localScale);
    //    }).WithoutBurst().Run();
    }
}
[UpdateAfter(typeof(CopyBoneTransformSystem))]

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


        Entities
        .WithNone<NotInitialized>()
        .ForEach((ref DynamicBuffer<SkinMatrix> skinMatrices, in DynamicBuffer<Bone> bones, in LocalToWorld localToWorld, in PlayableStream stream) =>
        {
            var root = GetComponent<BoneTransform>(bones[0].Entity).Value;
            var rootMatrixInv = math.inverse(root);
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                var bindPose = bone.BindPose;
                var boneLocalToWorld = GetComponent<BoneTransform>(bone.Entity).Value;
                var boneMatRootSpace = math.mul(rootMatrixInv,boneLocalToWorld);
                var skinMatRootSpace = math.mul(boneMatRootSpace, bindPose);

                skinMatrices[i] = new SkinMatrix { Value = new float3x4(skinMatRootSpace.c0.xyz, skinMatRootSpace.c1.xyz, skinMatRootSpace.c2.xyz, skinMatRootSpace.c3.xyz) };

            }

        }).ScheduleParallel();
        entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}