

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Animations.Rigging;
using System.Dynamic;
using System.Collections.Generic;
using System;
using UnityEditor;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Animation;
using Unity.Animation.Hybrid;
public struct PropertyStreamHandleJob : IAnimationJob
{

    public PropertyStreamHandle handleR;
    public PropertyStreamHandle handleG;
    public PropertyStreamHandle handleB;
    public Color color;

    public void ProcessRootMotion(UnityEngine.Animations.AnimationStream stream)
    {
    }

    public void ProcessAnimation(UnityEngine.Animations.AnimationStream stream)
    {


        var value = handleR.GetFloat(stream);
        // UnityEngine.Debug.Log(value);
        // Set the new light color.
        // handleR.SetFloat(stream, color.r);
        // handleG.SetFloat(stream, color.g);
        // handleB.SetFloat(stream, color.b);
    }
}

public class PropertyStreamHandleExample : MonoBehaviour, IConvertGameObjectToEntity
{

    public float test;
    public Color color = Color.white;

    PlayableGraph m_Graph;
    AnimationScriptPlayable m_AnimationScriptPlayable;
    public static string ConstructCustomPropertyName(Component component, string property)
    {
        return component.transform.GetInstanceID() + "/" + component.GetType() + "/" + property;
    }
    void Start()
    {


    }

    void Update()
    {
        // var layerCount = GetComponent<Animator>().layerCount;
        // for (int i = 0; i < layerCount; i++)
        // {
        //     var currentPlayingClips = GetComponent<Animator>().GetCurrentAnimatorClipInfo(i);
        //     var currentAnimatorStateInfo = GetComponent<Animator>().GetCurrentAnimatorStateInfo(i);
        //     for (int j = 0; j < currentPlayingClips.Length; j++)
        //     {
        //         var clipInfo = currentPlayingClips[j];
        //         var curveBindings = AnimationUtility.GetCurveBindings(clipInfo.clip);
        //         UnityEngine.Debug.Log($"Playing clip {clipInfo.clip.name}");
        //         for (var k = 0; k < curveBindings.Length; k++)
        //         {
        //             var curveBinding = curveBindings[k];
        //             if (curveBinding.path == "Bone" && curveBinding.propertyName == "localEulerAnglesRaw.y")
        //             {
        //                 UnityEngine.Debug.Log($"{curveBinding.path}.{curveBinding.propertyName}");

        //                 var animationCurve = AnimationUtility.GetEditorCurve(clipInfo.clip, curveBinding);
        //                 var invLength = animationCurve[animationCurve.length - 1].time;
        //                 float normalizedT = Time.time * invLength;
        //                 normalizedT -= math.floor(normalizedT);
        //                 var rotationY = animationCurve.Evaluate(normalizedT);
        //                 var r = quaternion.RotateY(math.radians(rotationY));
        //                 transform.GetChild(0).rotation = r;
        //                 // curveBinding.path = "Bones.test";
        //                 // curveBinding.propertyName = "";   
        //                 // curveBinding.type = typeof(PropertyStreamHandle);
        //                 // AnimationUtility.SetEditorCurve(animationClip,curveBinding,animationCurve);
        //             }

        //         }
        //     }
        // }
        // var animationJob = m_AnimationScriptPlayable.GetJobData<PropertyStreamHandleJob>();
        // animationJob.color = color;
        // m_AnimationScriptPlayable.SetJobData(animationJob);
    }

    void OnDisable()
    {
        m_Graph.Destroy();
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // var child = gameObject.transform.GetChild(0);
        var bindings = UnityEditor.AnimationUtility.GetAnimatableBindings(gameObject, gameObject);
        // for (int i = 0; i < bindings.Length; i++)
        // {
        //     var binding = bindings[i];
        //     Debug.Log($"{binding.path}.{binding.propertyName}");
        // }
        var animator = GetComponent<Animator>();
        m_Graph = PlayableGraph.Create("PropertyStreamHandleExample");
        var output = AnimationPlayableOutput.Create(m_Graph, "output", animator);

        var animationJob = new PropertyStreamHandleJob();

        // animationJob.transformStreamHandle = animator.BindSceneTransform(child);
        animationJob.handleR = animator.BindCustomStreamProperty(
            ConstructCustomPropertyName(this, "test"),
            CustomStreamPropertyType.Float
        );
        // animationJob.handleG = animator.BindStreamProperty(child, typeof(Vector3), "m_LocalPosition.y");
        // animationJob.handleB = animator.BindStreamProperty(child, typeof(Vector3), "m_LocalPosition.z");
        m_AnimationScriptPlayable = AnimationScriptPlayable.Create(m_Graph, animationJob);
        output.SetSourcePlayable(m_AnimationScriptPlayable);
        m_Graph.Play();
        dstManager.AddComponentData(entity, new PlayableAnimator
        {
            Value =  AnimatorControllerPlayable.Create(m_Graph, GetComponent<Animator>().runtimeAnimatorController)
        });
        dstManager.AddBuffer<CurrentlyPlayingClip>(entity);
        var clipsBuffer =  dstManager.AddBuffer<AnimationClip>(entity);
        var nbClip = animator.runtimeAnimatorController.animationClips.Length;
        var hashMap = new UnsafeHashMap<FixedString32Bytes,int>(nbClip,Allocator.Persistent);
        for(int i =0 ; i < nbClip; i++){
            var clip = animator.runtimeAnimatorController.animationClips[i];
            hashMap.Add(clip.name,i);
            var clipBlob = conversionSystem.BlobAssetStore.GetClip(clip);
            clipsBuffer.Add(new AnimationClip {Clip = clipBlob});
        }

        dstManager.AddComponentData(entity, new AnimationClips{
            HashMap = hashMap,
        });
        // Destroy(child.gameObject);
    }
}

public struct PlayableAnimator : IComponentData
{
    public AnimatorControllerPlayable Value;
}
public struct AnimationClip: IBufferElementData{
    public BlobAssetReference<Clip> Clip;
    public BlobAssetReference<ClipInstance> ClipInstance;
}
public struct AnimationClips: IComponentData{
    public UnsafeHashMap<FixedString32Bytes,int> HashMap;
}

public struct CurrentlyPlayingClip : IBufferElementData{
    public FixedString32Bytes Name;
    public float Weight;
}
public partial class AnimationStateSystem : SystemBase
{
    EntityQuery query;
    protected override void OnCreate()
    {
        base.OnCreate();
        query = GetEntityQuery(typeof(Unity.Animation.Rig),typeof(AnimationClip));
        RequireForUpdate(query);
    }
    protected override void OnUpdate()
    {

        var entities = query.ToEntityArray(Allocator.Temp);
        var rigs = query.ToComponentDataArray<Unity.Animation.Rig>(Allocator.Temp);
        for(int i =0; i < entities.Length; i++){
            var clips = EntityManager.GetBuffer<AnimationClip>(entities[i], false);
            for(int j =0; j < clips.Length; j++){
                var clip = clips[j];
                if(!clip.ClipInstance.IsCreated){
                    var clipInstance = ClipInstanceBuilder.Create(rigs[i].Value,clip.Clip);
                    clip.ClipInstance = clipInstance;
                    clips[j] = clip;
                }
                
            }
        }
        entities.Dispose();
        rigs.Dispose();
        // Entities.ForEach(( Animator animator,ref DynamicBuffer<CurrentlyPlayingClip> currentlyPlayingClipsInfo)=>{
        //     var layerCount = animator.layerCount;
        //     currentlyPlayingClipsInfo.ResizeUninitialized(0);
        //     var capacity = 0;
        //     for (int i = 0; i < layerCount; i++)
        //     {
        //         var currentPlayingClips = animator.GetCurrentAnimatorClipInfo(i);
        //         capacity += currentPlayingClips.Length;
        //         currentlyPlayingClipsInfo.EnsureCapacity(capacity);
        //         for (int j = 0; j < currentPlayingClips.Length; j++)
        //         {
        //             var clipInfo = currentPlayingClips[j];
        //             currentlyPlayingClipsInfo.Add(new CurrentlyPlayingClip{
        //                 Name = clipInfo.clip.name
        //             });
        //         }
        //     }
        // }).WithoutBurst().Run();
    }
}

public partial struct TestSystemBase : ISystem
{
    EntityCommandBufferSystem entityCommandBufferSystem;
    public void OnCreate(ref SystemState state)
    {
        entityCommandBufferSystem = state.World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Entities.ForEach((ref AnimationClips animationClips)=>{
            animationClips.HashMap.Dispose();
        }).Run();
    }

    public void OnUpdate(ref SystemState state)
    {
        var time = Time.time;
        var cb = entityCommandBufferSystem.CreateCommandBuffer();
       
        state.Entities.ForEach((
            Unity.Animation.Rig RigRef,
            DynamicBuffer<AnimatedData> animatedDatas,
            in DynamicBuffer<AnimationClip> animationClips,
            in DynamicBuffer<Child> childs)=>{
            // RigRef.Value.Value.Bindings;
            // var 
           var stream = Unity.Animation.AnimationStream.Create(RigRef.Value,animatedDatas.AsNativeArray());
           if(animationClips[0].ClipInstance.IsCreated){
                var invLength = animationClips[0].Clip.Value.Duration;
                float normalizedT =time * invLength;
                normalizedT -= math.floor(normalizedT);
                Core.EvaluateClip(animationClips[0].ClipInstance,normalizedT,ref stream,0);
                for(int i = 0; i < childs.Length; i++){
                    var rotation = stream.GetLocalToParentRotation(i+1).value;
                    cb.SetComponent(childs[i].Value,new Rotation{ Value =rotation });
                }
        
               
           }
         
        }).Run();
        entityCommandBufferSystem.AddJobHandleForProducer(state.Dependency);
    }
}