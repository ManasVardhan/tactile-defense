using UnityEngine;
using UnityEngine.SceneManagement;

namespace TactileWars.Core
{
    /// <summary>Start scene: loads the battle directly for milestone one (menus come later).</summary>
    public class BootLoader : MonoBehaviour
    {
        public string firstScene = "Battle";

        void Start()
        {
            Application.targetFrameRate = 60;
            SceneManager.LoadScene(firstScene);
        }
    }
}
