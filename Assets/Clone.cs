using Unity.Entities;

[UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
public partial class CloneAuthoringDependency : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((CloneAuthoring cloneAuthoring) =>
        {
           DeclareReferencedPrefab(cloneAuthoring.Prefab);
        });
    }
}
[GenerateAuthoringComponent]
public struct Clone : IComponentData{
    public int MaxNumber;
    public int CurrentNumber;

    public Entity Prefab;
}
