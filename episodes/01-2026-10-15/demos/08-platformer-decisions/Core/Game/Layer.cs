using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Platformer2D.Core
{
    /// <summary>
    /// Represents a parallax scrolling layer in the game, typically used for background elements.
    /// </summary>
    internal class Layer
    {
        private Texture2D[] textures;
        private float scrollSpeed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Layer"/> class.
        /// </summary>
        /// <param name="textures">An array of textures representing the segments of the layer.</param>
        /// <param name="scrollSpeed">The speed at which this layer scrolls relative to the camera.</param>
        public Layer(Texture2D[] textures, float scrollSpeed)
        {
            this.textures = textures;
            this.scrollSpeed = scrollSpeed;
        }

        /// <summary>
        /// Draws the layer, scrolling it based on the camera's position.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values. This parameter is not currently used.</param>
        /// <param name="spriteBatch">The SpriteBatch used to draw the layer.</param>
        /// <param name="cameraPosition">The current position of the camera in world space.</param>
        internal void Draw(GameTime gameTime, SpriteBatch spriteBatch, float cameraPosition, Vector2 viewportSize)
        {
            // Fit the original 480px artwork to the logical scene, not just the backbuffer.
            float segmentWidth = textures[0].Width * viewportSize.Y / textures[0].Height;
            float x = cameraPosition * scrollSpeed;
            int leftSegment = (int)Math.Floor(x / segmentWidth);
            float offset = leftSegment * segmentWidth - x;
            for (int segment = leftSegment; offset < viewportSize.X; segment++, offset += segmentWidth)
            {
                int left = (int)Math.Round(offset);
                int right = (int)Math.Round(offset + segmentWidth);
                int index = (segment % textures.Length + textures.Length) % textures.Length;
                spriteBatch.Draw(textures[index],
                    new Rectangle(left, 0, right - left, (int)Math.Ceiling(viewportSize.Y)), Color.White);
            }
        }
    }
}