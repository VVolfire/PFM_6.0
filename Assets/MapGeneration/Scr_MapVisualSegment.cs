using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Scr_MapVisualSegment : MonoBehaviour
{
    [SerializeField]
    public List<Sprite> sprites;

    public void SpriteUpdate(byte id)
    {
        this.GetComponent<SpriteRenderer>().sprite = sprites[id];
    }
}
