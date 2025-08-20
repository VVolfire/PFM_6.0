using Scellecs.Morpeh;
using UnityEngine;

[System.Serializable]
public struct MapNodePositionComponent : IComponent
{
    public int node_x;
    public int node_y;

    public int node_x_offset;
    public int node_y_offset;

    public int node_collumn;
    public int node_row;
}