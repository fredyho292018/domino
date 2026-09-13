#if UNITY_EDITOR
using Domino.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Domino.Catalog.Editor
{
    public static class GameCatalogMenu
    {
        [MenuItem("Domino/Game Catalog/Show Status")]
        static void Status()
        {
            var service=ApplicationServices.GameCatalog;
            Debug.Log(service==null?"[GAME-CATALOG] not initialized":service.ResolveMatch().Diagnostic);
        }
        [MenuItem("Domino/Game Catalog/Refresh")]
        static void Refresh() { _=ApplicationServices.RefreshGameCatalogAsync(true); }
    }
}
#endif
