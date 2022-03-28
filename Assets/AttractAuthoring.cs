

using Unity.Entities;
using UnityEngine;

public class AttractAuthoring: MonoBehaviour,IConvertGameObjectToEntity{
    public float maxDistance = 3;
    public float strengh = 1;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var attractionData = new AttractorData{
            MaxDistanceSqrd = maxDistance * maxDistance,
            Strengh= strengh
        };
        dstManager.AddComponentData(entity,attractionData);
    }
}
  