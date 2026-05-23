namespace TMBS.Core.Input
{
    public interface ITickableInputAdapter : IBuildInputAdapter
    {
        void Tick(float deltaTime);
    }
}