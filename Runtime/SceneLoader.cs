using System;
using System.Collections;
using System.Collections.Generic;
using AlynxServicesManager.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlynxSceneSystem.Runtime
{
    public class SceneLoader : GameManager
    {
        private static readonly int Loading = Animator.StringToHash("Loading");

        // Object to enable when the scene will be loaded
        public GameObject loadingScreen;

        //Animation to play when the scene will be loaded
        public Animator loadingScreenAnimator;

        //Fake loading time
        public float loadingTime = 3f;

        //Time of fade out animation
        public float fadeTime = 1f;

        private List<ISceneLoadingBlocker> _sceneLoadingBlockers = new();

        public Scene CurrentScene => SceneManager.GetActiveScene();

        public bool IsLoading { get; private set; }
        public static bool IsFading { get; private set; }

        public event Action OnSceneChanged;
        public event Action OnFaderFinished;

        protected override void Initialize()
        {
            base.Initialize();

            //Disable the loading screen
            loadingScreen.SetActive(false);
        }


        public void LoadScene(string sceneName, string sceneToUnload = null,
            LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool setActive = true)
        {
            if (IsLoading || IsFading)
            {
                Debug.LogWarning("SceneLoader: Scene is already loading");
                return;
            }

            //Start the coroutine
            StartCoroutine(LoadSceneAsync(sceneName, sceneToUnload, loadSceneMode, setActive));
        }

        //Load Scene with array of scenes to unload
        public void LoadScene(string[] scenesName, string sceneToUnload = null, bool setActive = true,
            string activeScene = null)
        {
            if (IsLoading || IsFading) return;

            //Start the coroutine
            StartCoroutine(LoadScenesAsync(scenesName, sceneToUnload, activeScene));
        }

        //Load Scene with array of scenes to unload
        private IEnumerator LoadScenesAsync(string[] scenesName, string sceneToUnload, string activeScene = null)
        {
            IsLoading = true;
            IsFading = true;

            //Check if scene name is valid
            if (scenesName.Length == 0)
            {
                Debug.LogError("Scene name is invalid");
                yield break;
            }

            // Enable the loading screen
            loadingScreen.SetActive(true);
            loadingScreenAnimator.SetBool(Loading, true);

            // Wait for the animation to finish
            yield return new WaitForSeconds(fadeTime);

            //Load the first scene
            var firstScene = true;

            //Load all the scenes
            foreach (var sceneName in scenesName)
            {
                yield return LoadSceneWorker(sceneName, firstScene ? LoadSceneMode.Single : LoadSceneMode.Additive);

                firstScene = false;
            }


            if (!string.IsNullOrEmpty(sceneToUnload)) yield return UnloadSceneAsync(sceneToUnload);

            // Wait for the fake loading time
            yield return new WaitForSeconds(loadingTime);


            //Set the scene active
            if (!string.IsNullOrEmpty(activeScene))
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(activeScene));

            //Check if there is a blocker that blocks the scene loading
            foreach (var blocker in _sceneLoadingBlockers)
            {
                blocker.OnStartedBlockingSceneLoading();
                yield return new WaitUntil(() => !blocker.IsBlockingSceneLoading());
            }

            OnSceneChanged?.Invoke();
            loadingScreenAnimator.SetBool(Loading, false);
            IsLoading = false;
            yield return new WaitForSeconds(fadeTime);
            loadingScreen.SetActive(false);
            OnFaderFinished?.Invoke();
            IsFading = false;
        }

        private IEnumerator LoadSceneWorker(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive)
        {
            //Check if scene name is valid
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("Scene name is invalid");
                yield break;
            }

            //Check if there is a valid scene to load
            if (SceneUtility.GetBuildIndexByScenePath(sceneName) == -1)
            {
                Debug.LogError("Scene name " + sceneName + " is invalid");
                yield break;
            }

            //Check if the scene is already loaded
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                //Set the scene active
                if (loadSceneMode == LoadSceneMode.Single)
                    SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

                yield break;
            }

            //If we are on webgl, we need to load the scene synchronously
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                SceneManager.LoadScene(sceneName, loadSceneMode);
            }
            else
            {
                // Load the scene asynchronously
                var loadOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);

                if (loadOperation == null)
                {
                    Debug.LogError("SceneLoader: Load operation is null");
                    yield break;
                }

                loadOperation.allowSceneActivation = false;

                // Wait for the scene to be loaded
                while (!loadOperation.isDone)
                    // Check if the scene is loaded
                    if (loadOperation.progress >= 0.9f)
                    {
                        // Activate the scene
                        loadOperation.allowSceneActivation = true;
                        //Wait for the scene to be activated
                        while (!loadOperation.isDone) yield return null;
                    }
            }

            //Little delay to avoid bugs
            yield return new WaitForSeconds(0.1f);
        }


        private IEnumerator LoadSceneAsync(string sceneName, string sceneToUnload,
            LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool setActive = true)
        {
            IsLoading = true;
            IsFading = true;

            //Check if scene name is valid
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("Scene name is invalid");
                yield break;
            }

            // Enable the loading screen
            loadingScreen.SetActive(true);
            loadingScreenAnimator.SetBool(Loading, true);

            // Wait for the animation to finish
            yield return new WaitForSeconds(fadeTime);

            if (!string.IsNullOrEmpty(sceneToUnload))
                //Unload the scene
                yield return UnloadSceneAsync(sceneToUnload);

            yield return LoadSceneWorker(sceneName, loadSceneMode);

            // Wait for the fake loading time
            yield return new WaitForSeconds(loadingTime);

            if (setActive)
                //Set the scene active
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

            //Check if there is a blocker that blocks the scene loading
            foreach (var blocker in _sceneLoadingBlockers)
            {
                blocker.OnStartedBlockingSceneLoading();
                yield return new WaitUntil(() => !blocker.IsBlockingSceneLoading());
            }

            OnSceneChanged?.Invoke();


            loadingScreenAnimator.SetBool(Loading, false);
            IsLoading = false;
            yield return new WaitForSeconds(fadeTime);
            loadingScreen.SetActive(false);
            OnFaderFinished?.Invoke();
            IsFading = false;
        }

        private IEnumerator UnloadSceneAsync(string sceneName)
        {
            //Check if scene name is valid
            if (string.IsNullOrEmpty(sceneName)) yield break;

            //Check if the scene is loaded
            if (!SceneManager.GetSceneByName(sceneName).isLoaded) yield break;

            // Unload the scene asynchronously
            var operation = SceneManager.UnloadSceneAsync(sceneName);

            // Wait for the scene to be unloaded
            while (operation is not { isDone: true }) yield return null;
        }

        //Add blocker to list
        public void AddSceneLoadingBlocker(ISceneLoadingBlocker sceneLoadingBlocker)
        {
            //Check if blocker is null
            if (sceneLoadingBlocker == null)
            {
                Debug.LogWarning("SceneLoader: Blocker is null");
                return;
            }

            //Check if blocker is already in list
            foreach (var blocker in _sceneLoadingBlockers)
                if (blocker == sceneLoadingBlocker)
                {
                    Debug.LogWarning("SceneLoader: Blocker is already in list");
                    return;
                }

            //Add blocker to list   
            _sceneLoadingBlockers.Add(sceneLoadingBlocker);
        }
    }
}