namespace AlynxSceneSystem.Runtime
{
    public interface ISceneLoadingBlocker
    {
        public bool IsBlockingSceneLoading();

        public void OnStartedBlockingSceneLoading();
    }
}