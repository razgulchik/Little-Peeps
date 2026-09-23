using UnityEngine;
using UnityEngine.SceneManagement;
namespace Water2D
{
    public class SceneCycleLoader : MonoBehaviour
    {
        static SceneCycleLoader instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                LoadNextScene();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                LoadPreviousScene();
            }
        }

        void LoadNextScene()
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            int totalScenes = SceneManager.sceneCountInBuildSettings;

            int nextIndex = (currentIndex + 1) % totalScenes;
            SceneManager.LoadScene(nextIndex);
        }

        void LoadPreviousScene()
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            int totalScenes = SceneManager.sceneCountInBuildSettings;

            int previousIndex = (currentIndex - 1 + totalScenes) % totalScenes;
            SceneManager.LoadScene(previousIndex);
        }
    }
}