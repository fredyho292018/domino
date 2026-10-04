using System;
using Domino.Player;

namespace Domino.UI.AppShell
{
    // Projects confirmed Player preference into the existing locale owner; never owns locale state.
    public sealed class PlayerLocaleBinding : IDisposable
    {
        readonly PlayerService player;
        readonly Func<bool> ready;
        readonly Action<string> select;
        string applied;
        bool disposed;
        public PlayerLocaleBinding(PlayerService player, Func<bool> ready, Action<string> select)
        {
            this.player=player; this.ready=ready; this.select=select;
            player.SnapshotChanged+=Snapshot;
            player.SyncStateChanged+=State;
            Refresh();
        }
        void Snapshot(PlayerSnapshot ignored)=>Refresh();
        void State(PlayerSyncState ignored)=>Refresh();
        public void Refresh()
        {
            if(disposed || !player.IsCurrentSession || !player.IsFresh || !ready())return;
            var code=player.PreferredLocale;
            if((code!="en" && code!="es") || code==applied)return;
            select(code); applied=code;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true; player.SnapshotChanged-=Snapshot; player.SyncStateChanged-=State;
        }
    }
}
