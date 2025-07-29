using Scellecs.Morpeh;
using UnityEngine;

[System.Serializable]
public struct ComponentMapNodePosition : IComponent
{
    public int node_x;
    public int node_y;
    public int node_x_offset;
    public int node_y_offset;
}