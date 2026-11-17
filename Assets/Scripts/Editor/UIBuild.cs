using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>Phase 6 build entry: assets, fonts, UI art (shapes, icons, model renders), meta data, then all
    /// scenes. Command line: -executeMethod TactileWars.EditorTools.UIBuild.BuildAll</summary>
    public static class UIBuild
    {
        [MenuItem("TactileWars/Build UI and Scenes")]
        public static void BuildAll()
        {
            AssetBuilder.BuildAll();
            var fonts = FontBuilder.Build();
            UIArtBuilder.Build(fonts);
            ProgressionBuilder.Build();
            SceneBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] done");
        }

        /// <summary>Assets, meta data (maps, catalog, bases) and scenes, without re-rendering the UI art.</summary>
        [MenuItem("TactileWars/Build Data and Scenes")]
        public static void BuildDataAndScenes()
        {
            AssetBuilder.BuildAll();
            ProgressionBuilder.Build();
            SceneBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] data and scenes done");
        }

        /// <summary>Rebuilds only the UI art (faster iteration on icons and shapes).</summary>
        [MenuItem("TactileWars/Build UI Art Only")]
        public static void BuildArtOnly()
        {
            var fonts = FontBuilder.Build();
            UIArtBuilder.Build(fonts);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] art done");
        }
    }
}
