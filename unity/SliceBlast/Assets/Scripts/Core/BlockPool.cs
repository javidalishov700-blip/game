using System.Collections.Generic;
using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>Pre-warmed, allocation-free pool. One instance per prefab (blocks, debris).</summary>
    [DisallowMultipleComponent]
    public sealed class BlockPool : MonoBehaviour
    {
        [SerializeField] private PooledObject prefab;
        [SerializeField] private int prewarmCount = 32;
        [SerializeField] private int hardCap = 160;

        private readonly Stack<PooledObject> _idle = new Stack<PooledObject>(64);
        private readonly List<PooledObject> _ticking = new List<PooledObject>(64);

        private Transform _root;
        private int _created;

        /// <summary>Runtime wiring. Call on an inactive object so it lands before Awake.</summary>
        public void Configure(PooledObject poolPrefab, int prewarm, int cap)
        {
            prefab = poolPrefab;
            prewarmCount = prewarm;
            hardCap = Mathf.Max(cap, prewarm);
        }

        private void Awake()
        {
            _root = transform;

            for (int i = 0; i < prewarmCount; i++)
            {
                PooledObject instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _idle.Push(instance);
            }
        }

        private PooledObject CreateInstance()
        {
            PooledObject instance = Instantiate(prefab, _root);
            instance.InitPooled(this);
            _created++;
            return instance;
        }

        public PooledObject Spawn(Vector3 position, Vector3 scale, Quaternion rotation)
        {
            PooledObject instance;

            if (_idle.Count > 0)
            {
                instance = _idle.Pop();
            }
            else if (_created < hardCap)
            {
                instance = CreateInstance();
            }
            else
            {
                instance = RecycleOldest();
            }

            Transform t = instance.CachedTransform;
            t.SetPositionAndRotation(position, rotation);
            t.localScale = scale;

            instance.gameObject.SetActive(true);
            instance.OnSpawned();

            if (instance.RequiresTick)
            {
                _ticking.Add(instance);
            }

            return instance;
        }

        private PooledObject RecycleOldest()
        {
            if (_ticking.Count > 0)
            {
                Release(_ticking[0]);
                return _idle.Pop();
            }

            return CreateInstance();
        }

        public void Release(PooledObject instance)
        {
            if (instance == null || !instance.gameObject.activeSelf)
            {
                return;
            }

            int index = _ticking.IndexOf(instance);
            if (index >= 0)
            {
                int last = _ticking.Count - 1;
                _ticking[index] = _ticking[last];
                _ticking.RemoveAt(last);
            }

            instance.OnDespawned();
            instance.gameObject.SetActive(false);
            _idle.Push(instance);
        }

        /// <summary>Driven from GameFlowManager so the whole game runs on a single Update.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = _ticking.Count - 1; i >= 0; i--)
            {
                if (i >= _ticking.Count)
                {
                    continue;
                }

                PooledObject instance = _ticking[i];
                if (!instance.Tick(deltaTime))
                {
                    Release(instance);
                }
            }
        }
    }
}
