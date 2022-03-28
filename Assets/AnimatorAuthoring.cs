

using UnityEngine;
using Unity.Entities;
using UnityEngine.Animations;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Animations.Rigging;


public struct Bone : IBufferElementData
{
    public Entity Entity;
    public int Index;

    public float4x4 BindPose;

    public ReadOnlyTransformHandle TransformHandle;


}
public struct BoneTransform : IComponentData
{
    public Entity Stream;
    public int Index;
}
public struct AnimatedBlendShape : IBufferElementData
{
    public PropertyStreamHandle PropertyStream;
    public int Index;
}

public struct PlayableStream : IComponentData
{
    public AnimationStream Stream;
}
public partial class CloneSystem : SystemBase
{
    EntityCommandBufferSystem entityCommandBufferSystem;
    protected override void OnCreate()
    {
        base.OnCreate();
        entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override void OnUpdate()
    {
        var cb = entityCommandBufferSystem.CreateCommandBuffer();
        var cbp = cb.AsParallelWriter();
        var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));

        Entities.ForEach((int entityInQueryIndex, ref Clone clone) =>
        {
            if (clone.CurrentNumber <= clone.MaxNumber)
            {
                clone.CurrentNumber += 1;
                var cloneEntity = cbp.Instantiate(entityInQueryIndex,clone.Prefab);
                var position = random.NextFloat3(-2, 2);
                cbp.AddComponent(entityInQueryIndex,cloneEntity, new Translation { Value = position });

            }

        }).ScheduleParallel();
        entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
public struct NotInitialized : IComponentData
{
    
}
public class DestroyWithEntity : MonoBehaviour{
    public Entity entity;

    void Update(){
        if(!World.DefaultGameObjectInjectionWorld.EntityManager.Exists(entity)){
            Destroy(gameObject);
        }
    }
}
public struct AnimatorRef: IComponentData{
    public Entity Value;
}
// [WorldSystemFilter(WorldSystemFilterFlags.HybridGameObjectConversion)]
public class AnimatorAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Animator Animator;
    public SkinnedMeshRenderer Renderer;

    public Transform Rig;
   
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        
        // Debug.Log("Here");
        // var conversionSettings = GameObjectConversionSettings
        // .FromWorld(dstManager.World, conversionSystem.BlobAssetStore);
        // conversionSettings.PrefabRoot = gameObject;
        // // conversionSettings.ConversionFlags = GameObjectConversionUtility.ConversionFlags.AssignName | GameObjectConversionUtility.ConversionFlags.SceneViewLiveConversion;
        // conversionSettings.ConversionWorldPreDispose += (World world) =>
        // {
            // var dstManager  = conversionSettings.DestinationWorld.EntityManager;
            // var conversionSystem = world.GetOrCreateSystem<GameObjectConversionSystem>();
            // conversionSystem.AddTypeToCompanionWhiteList(typeof(SkinnedMeshRenderer));
            // conversionSystem.AddTypeToCompanionWhiteList(typeof(Animator));
            dstManager.AddComponentObject(entity, Animator);
        
            // dstManager.AddComponentObject(entity,GetComponent<BoxCollider>());
            // dstManager.AddComponentObject(entity, Renderer);
         
            conversionSystem.DeclareDependency(gameObject, Renderer);
            
            var playableStream = new PlayableStream();
            var rendererEntity = conversionSystem.GetPrimaryEntity(Renderer);
            dstManager.AddComponentData(rendererEntity,new AnimatorRef{Value = entity});
            // dstManager.AddComponentObject(rendererEntity, Renderer);
            dstManager.AddComponentData(rendererEntity, playableStream);
            var skeleton = Renderer.bones;
            var bones = dstManager.AddBuffer<Bone>(rendererEntity);
            bones.ResizeUninitialized(skeleton.Length);
            conversionSystem.DeclareLinkedEntityGroup(gameObject);
            conversionSystem.DeclareLinkedEntityGroup(Renderer.rootBone.parent.gameObject);
            for (int i = 0; i < skeleton.Length; i++)
            {
                var bone = skeleton[i];
                var boneEntity = conversionSystem.GetPrimaryEntity(bone);
                bones = dstManager.GetBuffer<Bone>(rendererEntity);
                var boneComponent = new Bone
                {
                    Entity = boneEntity,
                    BindPose = Renderer.sharedMesh.bindposes[i],
                    TransformHandle = ReadOnlyTransformHandle.Bind(Animator, bone),
                };
                bones[i] = boneComponent;
                dstManager.SetName(boneEntity, bone.name);
                dstManager.AddComponent<CopyInitialTransformFromGameObject>(boneEntity);
                dstManager.AddComponent<CopyTransformFromGameObject>(boneEntity);
                dstManager.AddComponentData(boneEntity, new BoneTransform { Index = i, Stream = rendererEntity });
                // dstManager.AddComponentObject(boneEntity, bone);

            }
           
            var animatedBlendShapes = dstManager.AddBuffer<AnimatedBlendShape>(rendererEntity);
            // animatedBlendShapes.ResizeUninitialized(Renderer.sharedMesh.blendShapeCount);
            // for (int i = 0; i < Renderer.sharedMesh.blendShapeCount; i++)
            // {
            //     var name = Renderer.sharedMesh.GetBlendShapeName(i);
            //     var animatedBlendShape = new AnimatedBlendShape
            //     {
            //         PropertyStream = Animator.BindStreamProperty(Renderer.transform, typeof(SkinnedMeshRenderer), $"blendShape.{name}"),
            //     };
            //     animatedBlendShapes[i] = animatedBlendShape;
            // }
            dstManager.AddComponent<NotInitialized>(rendererEntity);
        // };
        // var animatorEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(Animator.gameObject, conversionSettings);
        // ChangeConversionMode(gameObject);
        // ChangeConversionMode(Renderer.gameObject);
        // var chunk =  dstManager.GetChunk(rendererEntity);
        // var typeHandle = dstManager.GetEntityTypeHandle();
        // var componentHandle = dstManager.GetComponentTypeHandle<PlayableStream>(false);
        // var components = chunk.GetNativeArray(componentHandle);
        // var entities = chunk.GetNativeArray(typeHandle);
        // for(int i = 0; i < entities.Length; i++){
        //     var chunkEntity = entities[i];
        //     Debug.Log($"Here {chunkEntity.Index}");
        //     if(chunkEntity.Index == rendererEntity.Index){

        //         var currentStream = components[i];
        //         Animator.OpenAnimationStream(ref currentStream.Stream);
        //         components[i] = currentStream;
        //         Debug.Log($"Founded {chunkEntity.Index}");
        //         break;
        //     }
        // }
        // var clipPlayable = AnimationClipPlayable.Create(graph, clip);
        // var clone = new Clone{MaxNumber = 5,CurrentNumber = 0, Prefab = entity};
        // dstManager.AddComponentData(entity,clone);
    }

}
[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]

public class AnimatorConversionSystem : GameObjectConversionSystem
{

    protected override void OnUpdate()
    {
        Entities.ForEach((AnimatorAuthoring animatorAuthoring) =>{
            var entity = GetPrimaryEntity(animatorAuthoring);
            if(animatorAuthoring.gameObject.activeInHierarchy){
                var instance = Object.Instantiate(animatorAuthoring.Animator);
                DstEntityManager.AddComponentObject(entity,instance);
            }
        });
    }
}