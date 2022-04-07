using Unity.Animation;
using Unity.Entities;
using UnityEngine;
using RPG.Core;
using Unity.Mathematics;


namespace RPG.Animation
{
    struct ChangeAttackAnimation : IComponentData
    {
        public BlobAssetReference<Clip> Animation;
    }
    public class ClipPlayerConversionSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ClipPlayer cp) =>
            {
                if (cp.Clip == null)
                {
                    return;
                }
                var entity = GetPrimaryEntity(cp);
                DynamicBuffer<AnimationClips> buffer;
                if(!DstEntityManager.HasComponent<AnimationClips>(entity)){
                    buffer = DstEntityManager.AddBuffer<AnimationClips>(entity);
                } else {
                    buffer =  DstEntityManager.GetBuffer<AnimationClips>(entity, false);;
                }
                DeclareAssetDependency(cp.gameObject, cp.Clip);
                buffer.Add(new AnimationClips
                {
                    Clip = cp.Clip.GetClip()
                });
                DstEntityManager.AddComponentData(entity, new PlayClip() { Index = buffer.Length - 1 });
                DstEntityManager.AddComponent<DeltaTime>(entity);
            });
        }
    }

    public class ClipPlayer : MonoBehaviour
    {

        public ClipAsset Clip;
    }

    public struct PlayClip : IComponentData
    {
        public int Index;
    }

    public partial class PlayClipSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var time = UnityEngine.Time.time;
            Entities.ForEach((
            ref AnimationStreamComponent streamComponent,
            in DynamicBuffer<AnimationClips> animationClips,
            in PlayClip playClip) =>
            {
                if (animationClips[playClip.Index].ClipInstance.IsCreated)
                {
                    ref var stream = ref streamComponent.Value;
                    var invLength = animationClips[playClip.Index].Clip.Value.Duration;
                    float normalizedT = time * invLength;
                    normalizedT -= math.floor(normalizedT);
                    Unity.Animation.Core.EvaluateClip(animationClips[playClip.Index].ClipInstance, normalizedT, ref stream, 0);
                }

            }).Run();
        }
    }

}
