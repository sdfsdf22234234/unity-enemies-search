using GPUAnimation.Runtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GPUAnimation.Runtime
{
    internal class AnimTexData
    {
        public Color[] Colors { get; private set; }
        public Vector2Int TexSize { get; private set; }

        public AnimTexData(Texture2D animTex)
        {
            TexSize = new Vector2Int(animTex.width, animTex.height);
            Colors = animTex.GetPixels();
        }

        public Color GetPixel(int x, int y)
        {
            int index = GPUAnimationUtility.PixelCoord2Index(TexSize, x, y);
            if (index >= 0 && index < Colors.Length)
            {
                return Colors[index];
            }

            return Color.black;
        }
    }

}