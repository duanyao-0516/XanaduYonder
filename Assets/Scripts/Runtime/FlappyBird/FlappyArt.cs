using System;
using UnityEngine;
using UnityEngine.U2D;

namespace XanaduYonder.Runtime.FlappyBird
{
    internal static class FlappyArt
    {
        private static SpriteAtlas atlas;

        // SpriteAtlas.GetSprite returns a clone. Each caller owns and destroys
        // that Sprite, while the four packed regions share the loaded texture.
        internal static Sprite LoadSprite(string name)
        {
            if(atlas==null) atlas=Resources.Load<SpriteAtlas>("FlappyBird/Atlas");
            if(atlas==null) throw new InvalidOperationException("Missing Flappy Bird sprite atlas.");
            var sprite=atlas.GetSprite(name);
            if(sprite==null) throw new InvalidOperationException("Missing Flappy Bird sprite: "+name);
            return sprite;
        }
    }
}
