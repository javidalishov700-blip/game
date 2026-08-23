using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>
    /// Decides what slides in next and puts it there. Owns the type lottery, the opening
    /// grace period, the random gap between specials and the forced Standard that follows
    /// a failed special.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private int standardOnlyBlocks = 12;
        // A special is an event, not a rhythm: this many plain blocks pass between them, and
        // the exact number is rolled fresh every time so the pattern is never learnable.
        [SerializeField] private int minSpecialGap = 12;
        [SerializeField] private int maxSpecialGap = 22;
        [SerializeField] private float travelRange = 2f;
        [SerializeField] private float minTravelRange = 1.2f;
        [SerializeField] private float hueStart = 0.45f;
        [SerializeField] private float hueSpan = 0.4f;
        [SerializeField] private float hueStep = 0.035f;
        [SerializeField, Range(0f, 1f)] private float saturation = 0.55f;
        [SerializeField, Range(0f, 1f)] private float brightness = 0.95f;

        private BlockPool _pool;
        private bool _forceStandard;
        private int _sinceSpecial;
        private int _gap;
        private int _spawnCount;
        private float _lastNeonHue = -1f;

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
            _lastNeonHue = -1f;
            RollGap();
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
            block.SetTint(TintFor(type, definition, paletteIndex));
            block.Configure(axisX, pivot, range, -side, type, definition.SpeedMultiplier);

            _spawnCount++;
            _forceStandard = false;

            if (BlockCatalogue.IsSpecial(type))
            {
                _sinceSpecial = 0;
                RollGap();
            }
            else
            {
                _sinceSpecial++;
            }

            return block;
        }

        private Color TintFor(BlockType type, BlockDefinition definition, int paletteIndex)
        {
            if (type == BlockType.Neon)
            {
                return RollNeon();
            }

            return definition.UsesPalette ? PaletteColor(paletteIndex) : definition.Tint;
        }

        /// <summary>
        /// A neon block has no signature colour — every one of them is a different hue, and
        /// never one close to the last. Full saturation at full value is what makes it neon.
        /// </summary>
        private Color RollNeon()
        {
            float hue = Random.value;

            for (int attempt = 0; attempt < 8 && _lastNeonHue >= 0f; attempt++)
            {
                float distance = Mathf.Abs(Mathf.DeltaAngle(hue * 360f, _lastNeonHue * 360f)) / 360f;

                if (distance >= 0.15f)
                {
                    break;
                }

                hue = Random.value;
            }

            _lastNeonHue = hue;
            return Color.HSVToRGB(hue, 0.85f, 1f);
        }

        private void RollGap()
        {
            int low = Mathf.Max(1, minSpecialGap);
            _gap = Random.Range(low, Mathf.Max(low, maxSpecialGap) + 1);
        }

        private BlockType PickType()
        {
            if (_forceStandard || _spawnCount < standardOnlyBlocks || _sinceSpecial < _gap)
            {
                return BlockType.Standard;
            }

            // The gap has run out, so this one is a special: Standard never competes in the
            // lottery, it just fills the space in between.
            float total = 0f;

            for (int i = BlockCatalogue.FirstSpecial; i < BlockCatalogue.Count; i++)
            {
                total += BlockCatalogue.At(i).SpawnWeight;
            }

            float roll = Random.value * total;

            for (int i = BlockCatalogue.FirstSpecial; i < BlockCatalogue.Count; i++)
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

        /// <summary>
        /// Ordinary blocks ping-pong through cyan → blue → violet. Bounded on purpose: no
        /// plain block should ever drift into a neon hue and be mistaken for a special.
        /// </summary>
        private Color PaletteColor(int index)
        {
            float hue = hueStart + Mathf.PingPong(index * hueStep, hueSpan);
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, brightness);
        }
    }
}
