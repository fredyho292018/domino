using Domino.Infrastructure;
using UnityEngine;

namespace Domino.Realtime
{
    public sealed class RealtimeLifecycle : MonoBehaviour
    {
        void OnApplicationPause(bool paused) => ApplicationServices.Realtime?.SetBackground(paused);
    }
}
