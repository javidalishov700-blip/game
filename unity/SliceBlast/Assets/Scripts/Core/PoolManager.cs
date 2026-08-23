using System.Collections.Generic;
using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>
    /// Registry over the individual pools. Everything spawned during a run — blocks, sliced
    /// offcuts, shatter debris, sparks, shockwaves — comes from here, so a long session never
    /// allocates and never hands the collector anything to do.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolManager : MonoBehaviour
    {
        public const string Blocks = "blocks";
        public const string Debris = "debris";
        public const string Sparks = "sparks";
        public const string Shockwaves = "shockwaves";

        public static PoolManager Instance { get; private set; }

        private readonly Dictionary<string, BlockPool> _pools = new Dictionary<string, BlockPool>(8);
        private readonly List<BlockPool> _ticking = new List<BlockPool>(8);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Register(string key, BlockPool pool, bool tick)
        {
            if (pool == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            _pools[key] = pool;

            if (tick && !_ticking.Contains(pool))
            {
                _ticking.Add(pool);
            }
        }

        public BlockPool Get(string key)
        {
            return _pools.TryGetValue(key, out BlockPool pool) ? pool : null;
        }

        public PooledObject Spawn(string key, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            BlockPool pool = Get(key);
            return pool != null ? pool.Spawn(position, scale, rotation) : null;
        }

        /// <summary>Driven from one place so the whole game runs on a single Update.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _ticking.Count; i++)
            {
                _ticking[i].Tick(deltaTime);
            }
        }
    }
}
