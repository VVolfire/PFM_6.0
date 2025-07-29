using Scellecs.Morpeh;
using UnityEngine;

[System.Serializable]
public struct ComponentMapNodeId : IComponent
{
    public byte node_id;
    //public MAP_NODE_STATES node_state;
}

//public enum MAP_NODE_STATES
//{
//    MAP_NODE_CURRENT,   // this means that node is the current node
//    MAP_NODE_CHOOSABLE, // this means that node can be chosen in this moment
//    MAP_NODE_BEHIND,    // this means that node is behind or IS the current collumn (probably make sprite darker)
//    MAP_NODE_INFRONT    // this means that node is in front of current collumn
//}
