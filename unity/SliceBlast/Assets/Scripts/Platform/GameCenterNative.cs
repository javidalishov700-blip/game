// The C# side of Assets/Plugins/iOS/SliceBlastGameCenter.mm: loading the leaderboard and
// posting a score through GameKit's current API, with GameKit's own error code when it
// fails. Off an iPhone (the editor, the Linux capture player) there is nothing to call, and
// every request answers "unsupported" so the caller falls back to Unity's Social API.
using System;
using System.Collections.Generic;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif
using UnityEngine;

namespace SliceBlast.Platform
{
    [Serializable]
    public sealed class NativeBoardEntry
    {
        public int rank;
        public string name;
        public long score;
        public string id;
        public bool local;
    }

    /// <summary>One answer from the plugin. Public fields so JsonUtility can fill them.</summary>
    [Serializable]
    public sealed class NativeBoardResult
    {
        public bool ok;

        /// <summary>Empty on success; otherwise where it failed: not-signed-in, find, not-found, entries, submit.</summary>
        public string error;

        /// <summary>GameKit's GKErrorCode (15 is "game unrecognised"), 0 when there was none.</summary>
        public int code;

        public NativeBoardEntry[] entries;
        public bool hasLocal;
        public int localRank;
        public long localScore;

        public static NativeBoardResult Failure(string error)
        {
            return new NativeBoardResult { ok = false, error = error, entries = new NativeBoardEntry[0] };
        }

        /// <summary>"entries:15" — the stage and GameKit's code, short enough to print under an error.</summary>
        public string Describe()
        {
            if (ok)
            {
                return "ok";
            }

            string stage = string.IsNullOrEmpty(error) ? "unknown" : error;
            return code != 0 ? stage + ":" + code : stage;
        }
    }

    internal static class GameCenterNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        public static bool Available => true;

        private delegate void NativeCallback(string json);

        [DllImport("__Internal")]
        private static extern void SliceBlastGC_LoadTop(string leaderboardId, int count, NativeCallback callback);

        [DllImport("__Internal")]
        private static extern void SliceBlastGC_Submit(string leaderboardId, long score, NativeCallback callback);

        // The plugin answers on the main thread, in the order it was asked.
        private static readonly Queue<Action<NativeBoardResult>> LoadHandlers = new Queue<Action<NativeBoardResult>>();
        private static readonly Queue<Action<NativeBoardResult>> SubmitHandlers = new Queue<Action<NativeBoardResult>>();

        public static void LoadTop(string leaderboardId, int count, Action<NativeBoardResult> done)
        {
            LoadHandlers.Enqueue(done);
            SliceBlastGC_LoadTop(leaderboardId, count, OnLoaded);
        }

        public static void Submit(string leaderboardId, long score, Action<NativeBoardResult> done)
        {
            SubmitHandlers.Enqueue(done);
            SliceBlastGC_Submit(leaderboardId, score, OnSubmitted);
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnLoaded(string json)
        {
            Deliver(LoadHandlers, json);
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnSubmitted(string json)
        {
            Deliver(SubmitHandlers, json);
        }

        private static void Deliver(Queue<Action<NativeBoardResult>> queue, string json)
        {
            if (queue.Count == 0)
            {
                return;
            }

            Action<NativeBoardResult> handler = queue.Dequeue();
            NativeBoardResult result = null;

            try
            {
                result = JsonUtility.FromJson<NativeBoardResult>(json);
            }
            catch (Exception)
            {
                // Falls through to the failure below.
            }

            handler?.Invoke(result ?? NativeBoardResult.Failure("json"));
        }
#else
        public static bool Available => false;

        public static void LoadTop(string leaderboardId, int count, Action<NativeBoardResult> done)
        {
            done?.Invoke(NativeBoardResult.Failure("unsupported"));
        }

        public static void Submit(string leaderboardId, long score, Action<NativeBoardResult> done)
        {
            done?.Invoke(NativeBoardResult.Failure("unsupported"));
        }
#endif
    }
}
