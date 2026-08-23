using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>
    /// Decides what slides in next and puts it there. Owns the type lottery, the opening
    /// grace period, the no-two-specials-in-a-row rule and the forced Standard that follows
    /// a failed special.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private int standardOnlyBlocks = 12;
        // A special is an event, not a rhythm: this many plain blocks must pass between them.
        [SerializeField] private int specialCooldown = 9;
        [SerializeField] private float travelRange = 2f;
        [SerializeField] private float minTravelRange = 1.2f;
        [SerializeField] private float glitchJump = 0.9f;
        [SerializeField] private float hueStart = 0.55f;
        [SerializeField] private float hueStep = 0.028f;
        [SerializeField, Range(0f, 1f)] private float saturation = 0.55f;
        [SerializeField, Range(0f, 1f)] private float brightness = 0.95f;

        private BlockPool _pool;
        private bool _forceStandard;
        private int _sinceSpecial;
        private int _spawnCount;

        public float TravelRange => travelRange;

        public void Bind(BlockPool pool)
        {
            _pool = pool;
        }

        public void ResetRun()
        {
            _forceStandard = false;
            _sinceSpecial = 0;
            _spawnCount = 0;
        }

        /// <summary>After a special is fumbled the rhythm resets with a plain block.</summary>
        public void ForceStandardNext()
        {
            _forceStandard = true;
        }

        public MovingBlock Spawn(Vector3 topPosition, Vector2 size, float layerHeight, bool axisX, int paletteIndex)
        {
            BlockType type = PickType();

            BlockDefinition definition = BlockCatalogue.Get(type);
            Vector3 scale = new Vector3(size.x, layerHeight, size.y);
            Vector3 position = new Vector3(topPosition.x, topPosition.y + layerHeight, topPosition.z);

            float side = (_spawnCount & 1) == 0 ? -1f : 1f;
            float pivot = axisX ? topPosition.x : topPosition.z;

            // The swing scales with the tower: a sliver of a platform does not deserve a
            // full-width fly-in, and the block has to stay inside the frame at both ends.
            float range = Mathf.Clamp(Mathf.Max(size.x, size.y) * 0.8f, minTravelRange, travelRange);

            if (axisX)
            {
                position.x = pivot + side * range;
            }
            else
            {
                position.z = pivot + side * range;
            }

            MovingBlock block = (MovingBlock)_pool.Spawn(position, scale, Quaternion.identity);
            block.SetTint(definition.UsesPalette ? PaletteColor(paletteIndex) : definition.Tint);
            block.Configure(axisX, pivot, range, -side, type, definition.SpeedMultiplier, type == BlockType.Glitch ? glitchJump : 0f);

            _spawnCount++;
            _forceStandard = false;

            if (BlockCatalogue.IsSpecial(type))
            {
                _sinceSpecial = 0;
            }
            else
            {
                _sinceSpecial++;
            }

            return block;
        }

        private BlockType PickType()
        {
            if (_forceStandard || _spawnCount < standardOnlyBlocks || _sinceSpecial < specialCooldown)
            {
                return BlockType.Standard;
            }

            float total = 0f;
            for (int i = 0; i < BlockCatalogue.Count; i++)
            {
                total += BlockCatalogue.At(i).SpawnWeight;
            }

            float roll = Random.value * total;

            for (int i = 0; i < BlockCatalogue.Count; i++)
            {
                BlockDefinition definition = BlockCatalogue.At(i);
                roll -= definition.SpawnWeight;

                if (roll <= 0f)
                {
                    return definition.Type;
                }
            }

            return BlockType.Standard;
        }

        private Color PaletteColor(int index)
        {
            return Color.HSVToRGB(Mathf.Repeat(hueStart + index * hueStep, 1f), saturation, brightness);
        }
    }
}
