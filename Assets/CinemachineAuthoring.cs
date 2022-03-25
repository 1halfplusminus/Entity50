
using Cinemachine;
using Unity.Entities;
using UnityEngine;

public class CinemachineAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var virtualCamera = GetComponent<CinemachineVirtualCamera>();
        // conversionSystem.DeclareDependency(gameObject,virtualCamera.m_Follow.gameObject);
        conversionSystem.DeclareDependency(gameObject,virtualCamera.m_Follow);
        conversionSystem.DeclareDependency(gameObject,virtualCamera.m_Follow);
        dstManager.AddComponentObject(entity,virtualCamera);
        dstManager.AddComponentObject(entity,virtualCamera.m_Follow);
    }
}