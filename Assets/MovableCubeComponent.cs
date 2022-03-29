

using Unity.Entities;
using Unity.NetCode;

[GenerateAuthoringComponent]
public struct MouvableCubeComponent: IComponentData{
    [GhostField]
    public int ExampleValue;
}