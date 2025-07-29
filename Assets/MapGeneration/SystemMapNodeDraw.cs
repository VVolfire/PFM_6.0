using Scellecs.Morpeh;
using UnityEngine;

public sealed class SystemMapNodeDraw : ISystem
{
    public World World { get; set; }

    private Filter filter;
    private Stash<ComponentMapNodePosition> nodeStash;


    public void OnAwake()
    {

        Debug.Log("NodeDrawSys is Awake");

        this.filter = this.World.Filter.With<ComponentMapNodePosition>().Build();
        this.nodeStash = this.World.GetStash<ComponentMapNodePosition>();

    }

    public void OnUpdate(float deltaTime)
    {
        Debug.Log("NodeDrawSys is Updating");
        foreach (var entity in this.filter)
        {
            ref var nodeComponent = ref nodeStash.Get(entity);
            Debug.Log(nodeComponent.node_x);
            Debug.Log(nodeComponent.node_y);
        }
    }

    public void Dispose()
    {
        Debug.Log("NodeDrawSys is Disposing");
        throw new System.NotImplementedException();
    }

}
